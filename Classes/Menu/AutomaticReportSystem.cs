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

namespace Axiom.ARS
{
    public static class AutomaticReportSystem
    {
        #region Bad Name Cache

        private static string[] exactCache;
        private static string[] containsCache;
        private static bool HasSpaceOrUnderscore(this string text)
        {
            return text.IndexOfAny(new[] { ' ', '_' }) >= 0;
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
        }

        private static List<String> alreadyReported = new List<string>();
        public static void DisableARS()
        {
            NetworkSystem.Instance.OnJoinedRoomEvent -= OnJoinRoom;
            NetworkSystem.Instance.OnPlayerJoined -= OnPlayerJoin;
            NetworkSystem.Instance.OnReturnedToSinglePlayer -= OnLeftRoom;
            alreadyReported.Clear();
        }

        #endregion

        #region Automatic Report System

        private static void OnJoinRoom()
        {
            CheckAllNames();
        }

        private static void OnPlayerJoin(NetPlayer player)
        {
            CheckName(player);
        }

        private static void OnLeftRoom()
        {
            alreadyReported.Clear();
        }

        private static void CheckAllNames()
        {
            foreach (NetPlayer player in PhotonNetwork.PlayerList)
            {
                string suspectName = player.NickName.ToUpper();
                string suspectID = player.UserId;

                if (alreadyReported.Contains(suspectID)) continue;

                if (suspectName.HasSpaceOrUnderscore())
                {
                    ReportUser(player, "Invalid Name");
                    alreadyReported.Add(suspectID);
                }
                else if (suspectName.MatchesExact() || suspectName.ContainsBadWords())
                {
                    ReportUser(player, "Racist or Inappropriate Name");
                    alreadyReported.Add(suspectID);
                }
            }
        }

        private static void CheckName(NetPlayer player)
        {
            string suspectName = player.NickName.ToUpper();
            string suspectID = player.UserId;

            if (alreadyReported.Contains(suspectID)) return;

            if (suspectName.HasSpaceOrUnderscore())
            {
                ReportUser(player, "Invalid Name");
                alreadyReported.Add(suspectID);
            }
            else if (suspectName.MatchesExact() || suspectName.ContainsBadWords())
            {
                ReportUser(player, "Racist or Inappropriate Name");
                alreadyReported.Add(suspectID);
            }
        }

        private static void ReportUser(NetPlayer player, string reason)
        {
            ReportPlayerFor(player, 2);
            NotificationManager.SendNotification($"<color=grey>[</color><color=orange>ARS</color><color=grey>]</color> Reported {player.NickName} for {reason}.");
        }

        #endregion
    }
}