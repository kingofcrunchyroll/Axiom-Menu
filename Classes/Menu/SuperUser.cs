using Axiom.Managers;
using Seralyth.Menu;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Seralyth.Classes.Menu;
using Seralyth.Managers;
using Seralyth.Mods;
using System;

namespace Axiom.SuperUsers
{
    public static class SuperUser
    {
        // Fire-and-forget entry point - kicks off the coroutine that actually does the check.
        // Deliberately does NOT take a userId parameter: OnLaunch() fires on frame 1, before
        // PhotonNetwork.LocalPlayer.UserId is necessarily populated yet, so capturing it early
        // (even correctly) would just wait on the wrong thing. Read it live instead.
        public static void GetSuperTools()
        {
            if (CoroutineManager.instance != null)
                CoroutineManager.instance.StartCoroutine(GetSuperToolsRoutine());
        }

        public static string specialColor = "#FF5AA1";

        private static IEnumerator GetSuperToolsRoutine()
        {
            // Wait for BOTH: RoleManager to have real data, AND Photon to have actually
            // assigned a UserId. Either one being "not ready yet" would otherwise silently
            // produce a false RoleTier.None with no error.
            while (!RoleManager.HasFetchedOnce || PhotonNetwork.LocalPlayer == null || string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId))
                yield return null;

            string userId = PhotonNetwork.LocalPlayer.UserId;
            RoleTier tier = RoleManager.GetRoleTier(userId);
            if (tier != RoleTier.Developer && tier != RoleTier.SuperUser && tier != RoleTier.Moderator)
                yield break;

            string toolsLabel = tier == RoleTier.Developer ? "Developer Tools" : "Moderation Tools";
            List<ButtonInfo> buttons = Buttons.buttons[Buttons.GetCategory("Main")].ToList();
            buttons.Add(new ButtonInfo
            {
                buttonText = $"{toolsLabel}",
                method = () => Buttons.CurrentCategoryName = "SuperUser Tools",
                isTogglable = false,
                toolTip = "Opens the Super User Tools."
            });
            Buttons.buttons[Buttons.GetCategory("Main")] = buttons.ToArray();

            string roleLabel = tier == RoleTier.Developer ? "DEVELOPER" : "MODERATOR";
            NotificationManager.SendNotification(
                $"<color=grey>[</color><color={specialColor}>{roleLabel}</color><color=grey>]</color> Welcome, {RoleManager.GetDisplayName(userId)}! {toolsLabel} have been enabled.",
                10000);

            if (tier == RoleTier.Developer)
            {
                List<ButtonInfo> newButtons = new List<ButtonInfo>
                {
                    new ButtonInfo { buttonText = "=== Developer Names ===", label = true},
                    new ButtonInfo { buttonText = "Set Name To FluxedGaming", method = () => Main.ChangeName("FluxedGaming"), isTogglable = false, toolTip = $"Set's your name to <color={specialColor}>FluxedGaming</color>."},
                    new ButtonInfo { buttonText = "Set Name To JesterDev", method = () => Main.ChangeName("JesterDev"), isTogglable = false, toolTip = $"Set's your name to <color={specialColor}>JesterDev</color>."},
                    
                    new ButtonInfo { buttonText = "================", label = true },
                    new ButtonInfo { buttonText = "Hide Developer Icon", enableMethod =() => NetworkedIconManager.SetHideSelfIcon(true), disableMethod =() => NetworkedIconManager.SetHideSelfIcon(false), toolTip = "Hides your icon from other players."},
                    new ButtonInfo { buttonText = "Virtual Stump Kick Gun", method = Overpowered.VirtualStumpKickGun, toolTip = "Kicks whoever your hand desires in the virtual stump."},

                    new ButtonInfo { buttonText = "=== Experimental Features ===", label = true},
                    new ButtonInfo { buttonText = "Test Ban Message", method = () => Main.BannedPrompt("Developer", "Testing", true), isTogglable = false},
                    new ButtonInfo { buttonText = "Test Ban Self", method = () => Main.Prompt("Are you sure?\n\nThis will Blacklist you from the menu.", () => { CoroutineManager.instance.StartCoroutine(BlacklistManager.SubmitBan(PhotonNetwork.LocalPlayer.UserId, PhotonNetwork.LocalPlayer.UserId, "Developer", "Testing", onComplete: (success, error) => { if (success) NotificationManager.SendNotification("<color=green>Blacklisted successfully.</color>", 5000); else NotificationManager.SendNotification($"<color=red>Ban failed:</color> {error}", 8000); })); Buttons.CurrentCategoryName = "SuperUser Tools"; }), isTogglable = false }
                };
                Buttons.buttons[Buttons.GetCategory("SuperUser Tools")] = Buttons.buttons[Buttons.GetCategory("SuperUser Tools")].Concat(newButtons).ToArray();
            }
        }
    }
}