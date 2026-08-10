using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using Seralyth;
using Seralyth.Extensions;
using Seralyth.Managers;
using Seralyth.Mods;
using Seralyth.Patches.Safety;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;
using static Seralyth.Mods.Settings;
using static Seralyth.Utilities.RigUtilities;
using static Seralyth.Managers.NotificationManager;

namespace Axiom.ARS
{
    public static class AutomaticReportSystem
    {
        #region Bad Name Cache

        private static string[] exactCache;
        private static string[] containsCache;
        private static bool HasInvalidCharacters(this string text)
        {
            return text.IndexOfAny(new[] { ' ', '_', '!', '@' }) >= 0;
        }
        private static bool ContainsBadWords(this string text)
        {
            return containsCache.Any(word => text.Contains(word));
        }
        private static bool MatchesExact(this string text)
        {
            return exactCache.Any(x => x == text);
        }

        public static IEnumerator LoadNames()
        {
            using UnityWebRequest request = UnityWebRequest.Get($"{PluginInfo.AxiomServerPath}/BadNames.json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Console.WriteLine($"Failed to download name filter: {request.error}");
                yield break;
            }

            JObject data = JsonConvert.DeserializeObject<JObject>(request.downloadHandler.text);


            exactCache = data["exactNames"]?.ToObject<string[]>() ?? new string[0];
            containsCache = data["contains"]?.ToObject<string[]>() ?? new string[0];

            Console.WriteLine($"Loaded {exactCache.Length} exact names and {containsCache.Length} contains filters.");
        }

        #endregion

        #region Public Functions

        public static void EnableARS()
        {
            NetworkSystem.Instance.OnJoinedRoomEvent += OnJoinRoom;
            NetworkSystem.Instance.OnPlayerJoined += OnPlayerJoin;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += OnLeftRoom;
            AntiCheatPatches.SendReportPatch.OnAntiCheatTrigger += OnAntiCheatTriggered;
            NetworkSystem.Instance.OnPlayerLeft += OnPlayerLeave;
            CoroutineManager.instance.StartCoroutine(DetectSpamReport());
            CoroutineManager.instance.StartCoroutine(TryDetectADBCheat());
        }

        private static List<String> alreadyReported = new List<string>();
        private static HashSet<VRRig> hookedRigs = new HashSet<VRRig>();
        public static void DisableARS()
        {
            NetworkSystem.Instance.OnJoinedRoomEvent -= OnJoinRoom;
            NetworkSystem.Instance.OnPlayerJoined -= OnPlayerJoin;
            NetworkSystem.Instance.OnReturnedToSinglePlayer -= OnLeftRoom;
            AntiCheatPatches.SendReportPatch.OnAntiCheatTrigger -= OnAntiCheatTriggered;
            NetworkSystem.Instance.OnPlayerLeft -= OnPlayerLeave;
            alreadyReported.Clear();
            hookedRigs.Clear();
            CoroutineManager.instance.StopCoroutine(DetectSpamReport());
            reportAttempts.Clear();
            CoroutineManager.instance.StopCoroutine(TryDetectADBCheat());
        }

        #endregion

        #region Networking Hooks
        private static void OnJoinRoom()
        {
            CheckAllNames();
            foreach (VRRig vrrig in VRRigCache.ActiveRigs)
            {
                vrrig.OnPlayerNameVisibleChanged += () => NameChange(vrrig);
                if (!hookedRigs.Contains(vrrig))
                hookedRigs.Add(vrrig);
            }
            //CoroutineManager.instance.StartCoroutine(TryDetectADBCheat());
        }

        private static void OnPlayerJoin(NetPlayer player)
        {
            CheckName(player);
            VRRig playerRig = GetVRRigFromPlayer(player);
            playerRig.OnPlayerNameVisibleChanged += () => NameChange(playerRig);
            if (!hookedRigs.Contains(playerRig))
                hookedRigs.Add(playerRig);
        }

        private static void OnLeftRoom()
        {
            alreadyReported.Clear();
            foreach (VRRig vrrig in VRRigCache.ActiveRigs)
            {
                vrrig.OnPlayerNameVisibleChanged -= () => NameChange(vrrig);
                hookedRigs.Clear();
            }
            reportAttempts.Clear();
        }

        private static void OnPlayerLeave(NetPlayer player)
        {
            VRRig playerRig = GetVRRigFromPlayer(player);
            playerRig.OnPlayerNameVisibleChanged -= () => NameChange(playerRig);
            if (hookedRigs.Contains(playerRig))
                hookedRigs.Remove(playerRig);
            //CoroutineManager.instance.StopCoroutine(TryDetectADBCheat());
        }

        #endregion

        #region Name Checking
        private static void CheckAllNames()
        {
            foreach (NetPlayer player in PhotonNetwork.PlayerList)
            {
                CheckName(player); // Debloated to just this
            }
            foreach (GorillaPlayerScoreboardLine line in GorillaScoreboardTotalUpdater.allScoreboardLines)
            {
                if (line.playerName.text == ("BADGORILLA"))
                {
                    if (line.linePlayer.NickName != "BADGORILLA")
                        ReportUser(line.linePlayer, "Triggering Name Sanitization");
                }
            }
        }

        /// <summary>
        /// Check's a specific player's name for any Profanity, Sexual or Racist words.
        /// </summary>
        /// <param name="player"></param>
        private static void CheckName(NetPlayer player)
        {
            string suspectName = player.NickName.ToUpperInvariant();
            string suspectID = player.UserId;

            if (alreadyReported.Contains(suspectID)) return;

            if (suspectName.HasInvalidCharacters())
            {
                ReportUser(player, "having an Invalid Name");
            }
            else if (suspectName.MatchesExact() || suspectName.ContainsBadWords())
            {
                ReportUser(player, "having a Racist or Inappropriate Name");
            }
        }

        #endregion

        #region Automatic Report System
        /// <summary>
        /// If someone triggers the anti-cheat, it will automatically report them. (Will add Checks later)
        /// </summary>
        /// <param name="suspectID"></param>
        /// <param name="suspectName"></param>
        /// <param name="reason"></param>
        private static void OnAntiCheatTriggered(string suspectID, string suspectName, string reason)
        {
            if (alreadyReported.Contains(suspectID)) return;

            NetPlayer suspect = GetPlayerFromID(suspectID);
            if (suspect != null)
            {
                ReportUser(suspect, reason);
            }
        }

        /// <summary>
        ///  Detects if the player has changed their name, if so check it.
        /// </summary>
        /// <param name="rig"></param>
        private static void NameChange(VRRig rig)
        {
            NetPlayer suspect = GetPlayerFromVRRig(rig);
            if (suspect != null)
            {
                CheckName(suspect);
            }
        }

        private const float SpamWindowSeconds = 3.5f;
        private const int SpamThreshold = 12;
        private const float handDistance = .25f;
        private static Dictionary<string, List<(string targetId, float time)>> reportAttempts = new Dictionary<string, List<(string, float)>>();

        private static bool OverlappingButton(VRRig vrrig, Transform button) =>
            new[] {
                vrrig.rightHandTransform.position,
                vrrig.leftHandTransform.position,
                vrrig.rightHand.syncPos,
                vrrig.leftHand.syncPos
            }.Any(handPos => Vector3.Distance(handPos, button.position) < handDistance * button.localScale.z);

        /// <summary>
        /// Checks if a player is spam reporting the whole lobby.
        /// </summary>
        /// <returns></returns>
        private static IEnumerator DetectSpamReport()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.25f);

                foreach (GorillaPlayerScoreboardLine line in GorillaScoreboardTotalUpdater.allScoreboardLines)
                {
                    if (line.reportButton == null || line.linePlayer == null)
                        continue;

                    foreach (VRRig vrrig in VRRigCache.ActiveRigs)
                    {
                        if (vrrig.isLocal)
                            continue;

                        if (!OverlappingButton(vrrig, line.reportButton.transform))
                            continue;

                        NetPlayer suspect = GetPlayerFromVRRig(vrrig);
                        NetPlayer target = line.linePlayer;

                        if (suspect == null || target == null || suspect == target)
                            continue;

                        RecordReportAttempt(suspect, target);
                    }
                }
            }
        }

        private static float DetectionDistance = 0.35f;

        public static bool VisualizeAntiADB = true;
        private static IEnumerator TryDetectADBCheat()
        {
            while (true)
            {
                foreach (VRRig rig in  VRRigCache.ActiveRigs)
                {
                    if (rig.isLocal)
                        continue;
                    bool HandsBehindRig(VRRig vrrig) =>
                    new[] {
                        vrrig.rightHandTransform.position,
                        vrrig.leftHandTransform.position,
                        vrrig.rightHand.syncPos,
                        vrrig.leftHand.syncPos
                    }.Any(handPos => Vector3.Distance(handPos, vrrig.transform.Find("rig").position + vrrig.transform.Find("rig").TransformDirection(new Vector3(-0.1975f, 0.775f, -1.5f))) < DetectionDistance * vrrig.scaleFactor);

                    if (VisualizeAntiADB)
                        Visuals.VisualizeAura(rig.transform.Find("rig").position + rig.transform.Find("rig").TransformDirection(new Vector3(-0.1975f, 0.775f, -1.5f)), DetectionDistance * rig.scaleFactor, Color.red);

                    if (HandsBehindRig(rig))
                    {
                        SendNotification($"<color=grey>[</color><color=orange>ARS</color><color=grey>]</color> Detected {GetPlayerFromVRRig(rig).NickName} for ADB Cheat.");
                        //ReportUser(GetPlayerFromVRRig(rig), "Abusing ADB Exploit");
                    }
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private static void RecordReportAttempt(NetPlayer suspect, NetPlayer target)
        {
            if (!reportAttempts.TryGetValue(suspect.UserId, out List<(string targetId, float time)> attempts))
            {
                attempts = new List<(string, float)>();
                reportAttempts[suspect.UserId] = attempts;
            }

            // Don't re-count the same target repeatedly while a hand just sits near one button -
            // only counts as a new "attempt" once per target per window.
            if (attempts.Any(a => a.targetId == target.UserId && Time.time - a.time < SpamWindowSeconds))
                return;

            attempts.Add((target.UserId, Time.time));
            attempts.RemoveAll(a => Time.time - a.time > SpamWindowSeconds); // prune anything outside the window

            int distinctTargets = attempts.Select(a => a.targetId).Distinct().Count();
            if (distinctTargets >= SpamThreshold)
            {
                ReportUser(suspect, "Mass Reporting Players", 1);
                attempts.Clear(); // reset so it doesn't immediately re-trigger next check
            }
        }

        ///<summary>
        /// Buttons:
        /// HateSpeech = 0
        /// Toxicity   = 1
        /// Cheating   = 2
        ///</summary>
        private static void ReportUser(NetPlayer player, string reason, int button = 2)
        {
            ReportPlayerFor(player, button, true);
            alreadyReported.Add(player.UserId);
            SendNotification($"<color=grey>[</color><color=orange>ARS</color><color=grey>]</color> Reported {player.NickName} for {reason}.");
        }

        #endregion
    }
}