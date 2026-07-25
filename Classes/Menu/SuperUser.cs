using Axiom.Managers;
using Seralyth.Menu;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Seralyth.Classes.Menu;
using Seralyth.Managers;

namespace Axiom.SuperUsers
{
    public static class SuperUser
    {
        // Fire-and-forget entry point - kicks off the coroutine that actually does the check
        // once RoleManager has real data to check against.
        public static void GetSuperTools(string userId)
        {
            if (CoroutineManager.instance != null)
                CoroutineManager.instance.StartCoroutine(GetSuperToolsRoutine(userId));
        }

        private static IEnumerator GetSuperToolsRoutine(string userId)
        {
            // Don't evaluate the role check against an empty cache - wait for the first
            // real fetch to land (RoleManager.StartPolling kicks this off in Bootstrapper,
            // but it's async and hasn't necessarily completed by the time OnLaunch runs).
            while (!RoleManager.HasFetchedOnce)
                yield return null;

            RoleTier tier = RoleManager.GetRoleTier(userId);
            if (tier != RoleTier.Developer && tier != RoleTier.SuperUser)
                yield break;

            List<ButtonInfo> buttons = Buttons.buttons[Buttons.GetCategory("Main")].ToList();
            buttons.Add(new ButtonInfo
            {
                buttonText = "Developer Tools",
                method = () => Buttons.CurrentCategoryName = "SuperUser Tools",
                isTogglable = false,
                toolTip = "Opens the Super User Tools."
            });
            Buttons.buttons[Buttons.GetCategory("Main")] = buttons.ToArray();

            string roleLabel = tier == RoleTier.Developer ? "DEVELOPER" : "MODERATOR";
            string toolsLabel = tier == RoleTier.Developer ? "Developer Tools" : "Moderation Tools";
            NotificationManager.SendNotification(
                $"<color=grey>[</color><color=#FF5AA1>{roleLabel}</color><color=grey>]</color> Welcome, {RoleManager.GetDisplayName(userId)}! {toolsLabel} have been enabled.",
                10000);
        }
    }
}