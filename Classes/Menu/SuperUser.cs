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

        private static IEnumerator GetSuperToolsRoutine()
        {
            // Wait for BOTH: RoleManager to have real data, AND Photon to have actually
            // assigned a UserId. Either one being "not ready yet" would otherwise silently
            // produce a false RoleTier.None with no error.
            while (!RoleManager.HasFetchedOnce || PhotonNetwork.LocalPlayer == null || string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId))
                yield return null;

            string userId = PhotonNetwork.LocalPlayer.UserId;
            RoleTier tier = RoleManager.GetRoleTier(userId);
            if (tier != RoleTier.Developer && tier != RoleTier.SuperUser)
                yield break;

            string toolsLabel = tier == RoleTier.Developer ? "Developer Tools" : "Moderation Tools";
            string specialColor = "#FF5AA1";
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
                    new ButtonInfo { buttonText = "Set Name To FluxedGaming", method = () => Main.ChangeName("FluxedGaming"), isTogglable = false, toolTip = $"Set's your name to <color={specialColor}>FluxedGaming</color>."},
                    new ButtonInfo { buttonText = "Set Name To JesterDev", method = () => Main.ChangeName("JesterDev"), isTogglable = false, toolTip = $"Set's your name to <color={specialColor}>JesterDev</color>."},
                };
                Buttons.buttons[Buttons.GetCategory("SuperUser Tools")] = Buttons.buttons[Buttons.GetCategory("SuperUser Tools")].Concat(newButtons).ToArray();
            }
        }
    }
}