using System;
using System.Collections;
using System.IO;
using BepInEx;
using UnityEngine;
using Seralyth.Menu;

namespace Axiom.Managers
{
    public static class MenuLockManager
    {
        public static bool IsLocked { get; private set; }
        public static string LockReason { get; private set; }
        public static IssuerRank LockIssuer { get; private set; }

        private static string LockFilePath =>
            Path.Combine(Paths.ConfigPath, "Axiom", "axiom.lock");

        // Call this once, after BlacklistManager has done its first fetch (see notes below)
        public static IEnumerator CheckAndEnforce(MonoBehaviour host, string localUserId)
        {
            // If a lock file already exists from a previous session, honor it immediately -
            // don't even wait on network before disabling the menu.
            if (File.Exists(LockFilePath))
            {
                string[] cached = File.ReadAllLines(LockFilePath);
                LockReason = cached.Length > 0 ? cached[0] : "Unknown";
                Enum.TryParse(cached.Length > 1 ? cached[1] : "None", out IssuerRank rank);
                LockIssuer = rank;
                Lock(LockReason, LockIssuer);
                yield break;
            }

            // Wait for the blacklist to actually be populated before trusting a "clean" result -
            // otherwise a slow/failed fetch on first launch could look like "not blacklisted"
            // when really we just don't know yet.
            yield return BlacklistManager.FetchOnce();

            if (BlacklistManager.TryGetEntry(localUserId, out IssuerRank issuer, out string reason))
            {
                LockReason = reason;
                LockIssuer = issuer;
                PersistLock(reason, issuer);
                Lock(reason, issuer);
            }
        }

        private static void PersistLock(string reason, IssuerRank issuer)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LockFilePath));
                File.WriteAllLines(LockFilePath, new[] { reason, issuer.ToString() });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[MenuLockManager] Failed to persist lock: {e}");
            }
        }

        private static void Lock(string reason, IssuerRank issuer)
        {
            IsLocked = true;
            UnityEngine.Debug.LogWarning($"[Axiom] Menu locked. Issued by: {LockIssuer}. Reason: {LockReason}");
            Main.BannedPrompt(issuer.ToString(), reason);
            // Hook into your menu's own visibility/toggle system here, e.g.:
            // Main.MenuEnabled = false;
            // Main.ForceHidden = true;
        }
    }
}