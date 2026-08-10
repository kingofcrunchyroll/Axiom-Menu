using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Seralyth;

namespace Axiom.Managers
{
    // Who issued the blacklist entry, not a severity level
    public enum IssuerRank
    {
        None,
        Moderator,
        Owner,
        Developer,
        Server
    }

    public class BlacklistEntry
    {
        [JsonProperty("rank")]
        public string Rank { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    public class BlacklistFile
    {
        // Keyed by Photon UserId
        [JsonProperty("userIds")]
        public Dictionary<string, BlacklistEntry> UserIds { get; set; } = new Dictionary<string, BlacklistEntry>();
    }

    public static class BlacklistManager
    {
        private const string BlacklistUrl = PluginInfo.AxiomServerPath + "SuperUser/blacklist.json";

        private const float RefreshIntervalSeconds = 300f; // 5 minutes

        private static Dictionary<string, BlacklistEntry> cachedEntries = new Dictionary<string, BlacklistEntry>();
        private static bool isFetching;

        // True once the first fetch attempt has completed (success or failure)
        public static bool HasFetchedOnce { get; private set; }

        // Distinguishes "we confirmed the current server state" from "we tried and failed" -
        // don't let anything treat isFetching==done as the same thing as a trustworthy result.
        public static bool LastFetchSucceeded { get; private set; }

        public static IssuerRank GetRank(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return IssuerRank.None;

            if (cachedEntries.TryGetValue(userId, out BlacklistEntry entry))
            {
                return entry.Rank switch
                {
                    "Server" => IssuerRank.Server,
                    "Developer" => IssuerRank.Developer,
                    "Owner"     => IssuerRank.Owner,
                    "Moderator" => IssuerRank.Moderator,
                    _ => IssuerRank.None
                };
            }

            return IssuerRank.None;
        }

        public static string GetReason(string userId)
        {
            return cachedEntries.TryGetValue(userId, out BlacklistEntry entry) ? entry.Reason : null;
        }

        public static bool TryGetEntry(string userId, out IssuerRank rank, out string reason)
        {
            rank = GetRank(userId);
            reason = GetReason(userId);
            return cachedEntries.ContainsKey(userId);
        }

        // Endpoint for the Cloudflare Worker that handles authorized ban submissions.
        // The client never has GitHub write access - the worker enforces who's allowed to ban.
        private const string BanSubmitUrl = "https://axiom-ban-worker.fluxedgaming421.workers.dev/";

        // authorized list server-side, so this isn't a trust-the-client situation.
        public static IEnumerator SubmitBan(string requesterId, string targetUserId, string rank, string reason, Action<bool, string> onComplete = null)
        {
            var payload = JsonConvert.SerializeObject(new
            {
                requesterId,
                targetUserId,
                rank,
                reason
            });

            using UnityWebRequest request = new UnityWebRequest(BanSubmitUrl, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(payload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            onComplete?.Invoke(success, success ? null : request.error);

            if (success)
            {
                // Pull the fresh list immediately rather than waiting for the next poll tick
                yield return FetchOnce();
            }
        }

        // Call once on startup (e.g. alongside RoleManager.StartPolling)
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

        public static IEnumerator FetchOnce()
        {
            if (isFetching)
                yield break;

            isFetching = true;

            using UnityWebRequest request = UnityWebRequest.Get(BlacklistUrl);
            request.SetRequestHeader("Cache-Control", "no-cache");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<BlacklistFile>(request.downloadHandler.text);
                    cachedEntries = parsed?.UserIds ?? new Dictionary<string, BlacklistEntry>();
                    LastFetchSucceeded = true;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[BlacklistManager] Failed to parse blacklist.json: {e}");
                    LastFetchSucceeded = false;
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"[BlacklistManager] Blacklist fetch failed: {request.error}");
                LastFetchSucceeded = false;
            }

            isFetching = false;
            HasFetchedOnce = true;
        }
    }
}