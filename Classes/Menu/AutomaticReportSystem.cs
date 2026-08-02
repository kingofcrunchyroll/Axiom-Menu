using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Seralyth;
using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using static Seralyth.Mods.Settings;
using Photon.Pun;
using Seralyth.Managers;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using Seralyth.Patches.Safety;
using static Seralyth.Utilities.RigUtilities;

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
        }

        #endregion

        #region Automatic Report System

        private static void OnJoinRoom()
        {
            CheckAllNames();
            foreach (VRRig vrrig in VRRigCache.ActiveRigs)
            {
                vrrig.OnPlayerNameVisibleChanged += () => NameChange(vrrig);
                if (!hookedRigs.Contains(vrrig))
                hookedRigs.Add(vrrig);
            }
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
        }

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

        private static void OnPlayerLeave(NetPlayer player)
        {
            VRRig playerRig = GetVRRigFromPlayer(player);
            playerRig.OnPlayerNameVisibleChanged -= () => NameChange(playerRig);
            if (hookedRigs.Contains(playerRig))
                hookedRigs.Remove(playerRig);
        }

        /* TODO:
        private static IEnumerator DetectSpamReport()
        {
            while (true)
            {
                yield return null;

                foreach (GorillaPlayerScoreboardLine line in GorillaScoreboardTotalUpdater.allScoreboardLines)
                {
                    foreach (var vrrig in from vrrig in VRRigCache.ActiveRigs where !vrrig.isLocal where Safety.OverlappingButton(vrrig, report.position))
                    {

                    }
                }
            }
        }
        */
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
            NotificationManager.SendNotification($"<color=grey>[</color><color=orange>ARS</color><color=grey>]</color> Reported {player.NickName} for {reason}.");
        }

        #endregion
    }
}