using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Axiom.Managers
{
    public enum RoleTier
    {
        None,
        MenuUser,
        SuperUser,
        Developer
    }

    public class RoleEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }
    }

    public class SuperUsersFile
    {
        // Keyed by Photon UserId
        [JsonProperty("userIds")]
        public Dictionary<string, RoleEntry> UserIds { get; set; } = new Dictionary<string, RoleEntry>();
    }

    public static class RoleManager
    {
        // Raw GitHub content URL - swap "main" for your default branch name if different
        private const string SuperUsersUrl =
            "https://raw.githubusercontent.com/FluxedGaming-git/Axiom-Server/main/SuperUsers.json";

        private const float RefreshIntervalSeconds = 300f; // 5 minutes

        private static Dictionary<string, RoleEntry> cachedRoles = new Dictionary<string, RoleEntry>();
        private static bool isFetching;

        // True once the first fetch attempt has completed (success or failure) - lets callers
        // wait for real data instead of racing the async fetch and reading an empty cache.
        public static bool HasFetchedOnce { get; private set; }

        public static RoleTier GetRoleTier(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return RoleTier.None;

            if (cachedRoles.TryGetValue(userId, out RoleEntry entry))
            {
                return entry.Tier switch
                {
                    "Developer" => RoleTier.Developer,
                    "SuperUser" => RoleTier.SuperUser,
                    "MenuUser" => RoleTier.MenuUser,
                    _ => RoleTier.None
                };
            }

            return RoleTier.None;
        }

        public static string GetDisplayName(string userId)
        {
            return cachedRoles.TryGetValue(userId, out RoleEntry entry) ? entry.Name : null;
        }

        // Call once on startup (e.g. from your BasePlugin.Load or an existing manager init)
        public static void StartPolling(MonoBehaviour host)
        {
            host.StartCoroutine(PollLoop());
        }

        private static IEnumerator PollLoop()
        {
            while (true)
            {
                yield return FetchOnce();
                yield return new WaitForSeconds(RefreshIntervalSeconds);
            }
        }

        // Exposed separately in case you want to trigger a manual refresh (e.g. a "Refresh Roles" button)
        public static IEnumerator FetchOnce()
        {
            if (isFetching)
                yield break;

            isFetching = true;

            using UnityWebRequest request = UnityWebRequest.Get(SuperUsersUrl);
            // Raw GitHub content is cached aggressively by their CDN; this shaves stale-cache time
            request.SetRequestHeader("Cache-Control", "no-cache");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<SuperUsersFile>(request.downloadHandler.text);
                    cachedRoles = parsed?.UserIds ?? new Dictionary<string, RoleEntry>();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[RoleManager] Failed to parse SuperUsers.json: {e}");
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"[RoleManager] Role fetch failed: {request.error}");
            }

            isFetching = false;
            HasFetchedOnce = true;
        }
    }
}