using System;
using System.Collections;
using System.IO;
using BepInEx;
using UnityEngine;
using Seralyth.Menu;
using Seralyth.Managers;

namespace Axiom.Managers
{
    public static class MenuLockManager
    {
        public static bool IsLocked { get; private set; }
        public static string LockReason { get; private set; }
        public static IssuerRank LockIssuer { get; private set; }

        private static string LockFilePath =>
            Path.Combine(Paths.ConfigPath, "Axiom", "axiom.lock");

        // Call this wrapped in StartCoroutine (this method IS the coroutine).
        public static IEnumerator CheckAndEnforce(MonoBehaviour host)
        {
            bool hadCachedLock = File.Exists(LockFilePath);

            if (hadCachedLock)
            {
                // Show the lock immediately from the cached file so there's no gap where
                // the menu is briefly usable while we wait on network - we'll re-verify
                // against the live blacklist below and clear it if it's genuinely gone.
                string[] cached = File.ReadAllLines(LockFilePath);
                LockReason = cached.Length > 0 ? cached[0] : "Unknown";
                Enum.TryParse(cached.Length > 1 ? cached[1] : "None", out IssuerRank cachedRank);
                LockIssuer = cachedRank;
                Lock(LockReason, LockIssuer);
            }

            // Wait for the blacklist to actually be populated, AND for Photon to have actually
            // assigned a UserId - either being "not ready yet" would otherwise silently produce
            // a false "not blacklisted" result.
            while (!BlacklistManager.HasFetchedOnce || Photon.Pun.PhotonNetwork.LocalPlayer == null || string.IsNullOrEmpty(Photon.Pun.PhotonNetwork.LocalPlayer.UserId))
            {
                if (!BlacklistManager.HasFetchedOnce)
                    yield return BlacklistManager.FetchOnce();
                else
                    yield return null;
            }

            string localUserId = Photon.Pun.PhotonNetwork.LocalPlayer.UserId;

            if (BlacklistManager.TryGetEntry(localUserId, out IssuerRank issuer, out string reason))
            {
                // Still blacklisted (or newly blacklisted) - (re)persist and lock.
                LockReason = reason;
                LockIssuer = issuer;
                PersistLock(reason, issuer);
                Lock(reason, issuer);
            }
            else if (hadCachedLock)
            {
                // Only clear the lock on a CONFIRMED clean result. A failed/timed-out fetch
                // must never be treated as "not blacklisted" - that'd let someone dodge their
                // own ban just by blocking network access at the right moment.
                if (BlacklistManager.LastFetchSucceeded)
                    Unlock();
                else
                    UnityEngine.Debug.LogWarning("[MenuLockManager] Blacklist fetch failed - keeping existing lock in place until a successful check confirms it's clear.");
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
            UnityEngine.Debug.LogWarning($"[Axiom] Menu locked. Issued by: {issuer}. Reason: {reason}");
            Main.BannedPrompt(issuer.ToString(), reason);
        }

        private static void Unlock()
        {
            IsLocked = false;
            LockReason = null;
            LockIssuer = IssuerRank.None;

            try
            {
                if (File.Exists(LockFilePath))
                    File.Delete(LockFilePath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[MenuLockManager] Failed to remove lock file: {e}");
            }

            UnityEngine.Debug.Log("[Axiom] Menu unlocked - no longer found on the blacklist.");
            NotificationManager.SendNotification("<color=green>Your blacklist entry has been cleared. Welcome back.</color>", 8000);
            Main.Lockdown = false;
        }
    }
}