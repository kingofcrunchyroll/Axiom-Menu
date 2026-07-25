using Axiom.Managers;
using Seralyth.Menu;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using Seralyth.Classes.Menu;
using Seralyth.Managers;
using System.Linq;

namespace Axiom.SuperUsers
{
    public static class SuperUser
    {
        public static void GetSuperTools(string userId)
        {
            if (RoleManager.GetRoleTier(userId) == RoleTier.Developer || RoleManager.GetRoleTier(userId) == RoleTier.SuperUser)
            {
                List<ButtonInfo> buttons = Buttons.buttons[Buttons.GetCategory("Main")].ToList();
                buttons.Add(new ButtonInfo { buttonText = "Developer Tools", method = () => Buttons.CurrentCategoryName = "SuperUser Tools", isTogglable = false, toolTip = "Opens the Super User Tools." });
                Buttons.buttons[Buttons.GetCategory("Main")] = buttons.ToArray();
                NotificationManager.SendNotification($"<color=grey>[</color><color=#FF5AA1>{(RoleManager.GetRoleTier(userId) == RoleTier.Developer ? "DEVELOPER" : "MODERATOR")}</color><color=grey>]</color> Welcome, {RoleManager.GetDisplayName(userId)}! {(RoleManager.GetRoleTier(userId) == RoleTier.Developer ? "Developer Tools" : "Moderation Tools")} have been enabled.", 10000);
            }
        }
    }
}