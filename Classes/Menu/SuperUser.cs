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
using System.Reflection.Emit;
using GorillaNetworking;
using Seralyth.Patches.Menu;
using Axiom.ARS;

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

        private static void Cosmetic(string argument)
        {
            var item = CosmeticsController.instance.GetItemFromDict(CosmeticsController.instance.GetItemNameFromDisplayName(argument.ToUpper()));
            try
            {
                CosmeticsController.instance.ApplyCosmeticItemToSet(
                    CosmeticsController.instance.currentWornSet,
                    item,
                    false, // isLeftHand - only relevant for hand-specific items
                    true   // applyToPlayerPrefs
                );
                CosmeticsController.instance.UpdateWornCosmetics(true, false); // sync, playfx
                CosmeticsController.instance.UpdateWardrobeModelsAndButtons();
                CosmeticsController.instance.OnCosmeticsUpdated?.Invoke();
                string s = item.displayName.ToString();
                NotificationManager.SendNotification($"enabled {s}");
            }
            catch (Exception ex)
            { NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Failed to get Cosmetic: {ex}"); }
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
            if (tier != RoleTier.Developer && tier != RoleTier.SuperUser && tier != RoleTier.Moderator)
                yield break;

            string toolsLabel = tier switch
            {
                RoleTier.Developer => "Developer Tools",
                RoleTier.Owner     => "Developer Tools",
                RoleTier.Moderator => "Moderator Tools",
                RoleTier.SuperUser => "Super User Mods",
                _ => null
            };

            List<ButtonInfo> buttons = Buttons.buttons[Buttons.GetCategory("Main")].ToList();
            if (RoleManager.IsUserStaff(userId))
            {
                buttons.Add(new ButtonInfo
                {
                    buttonText = $"{toolsLabel}",
                    method = () => Buttons.CurrentCategoryName = "Staff Tools",
                    isTogglable = false,
                    toolTip = $"Opens the {toolsLabel}."
                });
            }
            buttons.Add(new ButtonInfo
            {
                buttonText = $"Super User Mods",
                method = () => Buttons.CurrentCategoryName = "SuperUser Mods",
                isTogglable = false,
                toolTip = $"Opens the Super User Mods."
            });
            Buttons.buttons[Buttons.GetCategory("Main")] = buttons.ToArray();

            string roleLabel = tier switch
            {
                RoleTier.Developer => "DEVELOPER",
                RoleTier.Owner     => "OWNER",
                RoleTier.Moderator => "MODERATOR",
                RoleTier.SuperUser => "SUPER USER",
                _ => null
            };
            NotificationManager.SendNotification(
                $"<color=grey>[</color><color={specialColor}>{roleLabel}</color><color=grey>]</color> Welcome, {RoleManager.GetDisplayName(userId)}! {toolsLabel} have been enabled.",
                10000);

            List<ButtonInfo> superButtons = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Hide Special Icon", enableMethod =() => NetworkedIconManager.SetHideSelfIcon(true), disableMethod =() => NetworkedIconManager.SetHideSelfIcon(false), toolTip = "Hides your icon from other players."},
                new ButtonInfo { buttonText = "Unlock Fan Club Subscription", aliases = new[] { "Unlock VIM", "Unlock Very Cool Monke" }, enableMethod =() => SubscriptionPatches.enabled = true, disableMethod =() => SubscriptionPatches.enabled = false, toolTip = "Unlocks the Gorilla Tag fan club subscription. This mod is client-sided." },
            };
            Buttons.buttons[Buttons.GetCategory("SuperUser Mods")] = Buttons.buttons[Buttons.GetCategory("SuperUser Mods")].Concat(superButtons).ToArray();

            switch (tier)
            {
                case RoleTier.Owner:
                case RoleTier.Developer:
                    {
                        List<ButtonInfo> newButtons = new List<ButtonInfo>
                    {
                        new ButtonInfo { buttonText = "=== Developer Names ===", label = true},
                        new ButtonInfo { buttonText = "Set Name To FluxedGaming", method = () => Main.ChangeName("FluxedGaming"), isTogglable = false, toolTip = $"Set's your name to <color={specialColor}>FluxedGaming</color>."},
                        new ButtonInfo { buttonText = "Set Name To JesterDev", method = () => Main.ChangeName("JesterDev"), isTogglable = false, toolTip = $"Set's your name to <color={specialColor}>JesterDev</color>."},
                        new ButtonInfo { buttonText = "=== Owner Names ===", label = true},
                        new ButtonInfo { buttonText = "Set Name To Kotaa", method = () => Main.ChangeName("Kotaa"), isTogglable = false, toolTip = $"Set's your name to <color={specialColor}>Kotaa</color>."},

                        new ButtonInfo { buttonText = "==== Icon Buttons ====", label = true },
                        new ButtonInfo { buttonText = "Hide Developer Icon", enableMethod =() => NetworkedIconManager.SetHideSelfIcon(true), disableMethod =() => NetworkedIconManager.SetHideSelfIcon(false), toolTip = "Hides your icon from other players."},
                        new ButtonInfo { buttonText = "Show Self Icon", enableMethod =() => NetworkedIconManager.showSelfIcon = true, disableMethod =() => NetworkedIconManager.showSelfIcon = false, toolTip = "Lets you see your own icon." },

                        new ButtonInfo { buttonText = "=== Experimental Features ===", label = true},
                        new ButtonInfo { buttonText = "Automatic Report System", enableMethod = () => AutomaticReportSystem.EnableARS(), disableMethod = () => AutomaticReportSystem.DisableARS(), toolTip = "Turns on Axiom's Automatic Report System."},
                        new ButtonInfo { buttonText = "Debug Subtitles", enableMethod = () => {CoroutineManager.instance.StartCoroutine(Settings.DictationOn()); CoroutineManager.instance.StartCoroutine(Settings.Subtitles()); }, disableMethod = () => { CoroutineManager.instance.StopCoroutine(Settings.Subtitles()); Settings.DictationOff(); }, toolTip = "Enables Subtitles"},
                        new ButtonInfo { buttonText = "Test Ban Message", method = () => Main.BannedPrompt("Developer", "Testing", true), isTogglable = false},
                        new ButtonInfo { buttonText = "Test Ban Self", method = () => Main.Prompt("Are you sure?\n\nThis will Blacklist you from the menu.", () => { CoroutineManager.instance.StartCoroutine(BlacklistManager.SubmitBan(PhotonNetwork.LocalPlayer.UserId, PhotonNetwork.LocalPlayer.UserId, "Developer", "Testing", onComplete: (success, error) => { if (success) NotificationManager.SendNotification("<color=green>Blacklisted successfully.</color>", 5000); else NotificationManager.SendNotification($"<color=red>Ban failed:</color> {error}", 8000); })); Buttons.CurrentCategoryName = "SuperUser Tools"; }), isTogglable = false },

                        new ButtonInfo { buttonText = "===== idk =====", label = true},
                        new ButtonInfo { buttonText = "Stump Kick Gun", method = Overpowered.StumpKickGun, toolTip = "Kicks whoever your hand desires if they are in stump." },
                        new ButtonInfo { buttonText = "Virtual Stump Kick Gun", method = Overpowered.VirtualStumpKickGun, toolTip = "Kicks whoever your hand desires in the virtual stump."},
                        new ButtonInfo { buttonText = "Break Mod Checkers", enableMethod = Fun.BreakModCheckers, disableMethod = Safety.BypassModCheckers, toolTip = "Tells players using mod checkers that you have every mod possible."},
                        new ButtonInfo { buttonText = "Custom Mod Spoofer", method = Fun.CustomModSpoofer, isTogglable = false, toolTip = "Make mod checkers see only what you allow."},
                        new ButtonInfo { buttonText = "Vibrate Gun", method = Overpowered.VibrateGun, toolTip = "Makes whoever your hand desires' controllers vibrate." },
                        new ButtonInfo { buttonText = "Vibrate All", method = Overpowered.VibrateAll, toolTip = "Makes everyone in the the room's controllers vibrate." },
                        new ButtonInfo { buttonText = "Vibrate Aura", method = Overpowered.VibrateAura, toolTip = "Makes players nearby you controllers vibrate."},
                        new ButtonInfo { buttonText = "Vibrate On Touch", method = Overpowered.VibrateOnTouch, toolTip = "Makes whoever you touch controllers vibrate."},
                        new ButtonInfo { buttonText = "Slow Gun", method = Overpowered.SlowGun, toolTip = "Forces tag freeze on whoever your hand desires." },
                        new ButtonInfo { buttonText = "Slow Aura", method = Overpowered.SlowAura, toolTip = "Forces tag freeze on players nearby you."},
                        new ButtonInfo { buttonText = "Slow On Touch", method = Overpowered.SlowOnTouch, toolTip = "Forces tag freeze on whoever you touch."},
                        new ButtonInfo { buttonText = "Unlock Fan Club Subscription", aliases = new[] { "Unlock VIM", "Unlock Very Cool Monke" }, enableMethod =() => SubscriptionPatches.enabled = true, disableMethod =() => SubscriptionPatches.enabled = false, toolTip = "Unlocks the Gorilla Tag fan club subscription. This mod is client-sided." },

                        new ButtonInfo { buttonText = "=== Moderation Stuff ===", label = true},
                        new ButtonInfo { buttonText = "Report Gun", method = Fun.ReportGun, toolTip = "Reports whoever your hand desires for cheating."},
                    };
                        Buttons.GetIndex("Exit Staff Tools").overlapText = $"Exit {toolsLabel}";
                        Buttons.buttons[Buttons.GetCategory("Staff Tools")] = Buttons.buttons[Buttons.GetCategory("Staff Tools")].Concat(newButtons).ToArray();
                        break;
                    }
                case RoleTier.Moderator:
                    {
                        List<ButtonInfo> newButtons = new List<ButtonInfo>
                    {
                        new ButtonInfo { buttonText = "Hide Moderator Icon", enableMethod =() => NetworkedIconManager.SetHideSelfIcon(true), disableMethod =() => NetworkedIconManager.SetHideSelfIcon(false), toolTip = "Hides your icon from other players."},
                        new ButtonInfo { buttonText = "Show Self Icon", enableMethod =() => NetworkedIconManager.showSelfIcon = true, disableMethod =() => NetworkedIconManager.showSelfIcon = false, toolTip = "Lets you see your own icon." },
                        
                        new ButtonInfo { buttonText = "=== Moderation Stuff ===", label = true},
                        new ButtonInfo { buttonText = "Report Gun", method = Fun.ReportGun, toolTip = "Reports whoever your hand desires for cheating."},

                        new ButtonInfo { buttonText = "===== idk =====", label = true},
                        new ButtonInfo { buttonText = "Stump Kick Gun", method = Overpowered.StumpKickGun, toolTip = "Kicks whoever your hand desires if they are in stump." },
                        new ButtonInfo { buttonText = "Virtual Stump Kick Gun", method = Overpowered.VirtualStumpKickGun, toolTip = "Kicks whoever your hand desires in the virtual stump."},
                        new ButtonInfo { buttonText = "Break Mod Checkers", enableMethod = Fun.BreakModCheckers, disableMethod = Safety.BypassModCheckers, toolTip = "Tells players using mod checkers that you have every mod possible."},
                        new ButtonInfo { buttonText = "Custom Mod Spoofer", method = Fun.CustomModSpoofer, isTogglable = false, toolTip = "Make mod checkers see only what you allow."},
                        new ButtonInfo { buttonText = "Vibrate Gun", method = Overpowered.VibrateGun, toolTip = "Makes whoever your hand desires' controllers vibrate." },
                        new ButtonInfo { buttonText = "Vibrate All", method = Overpowered.VibrateAll, toolTip = "Makes everyone in the the room's controllers vibrate." },
                        new ButtonInfo { buttonText = "Vibrate Aura", method = Overpowered.VibrateAura, toolTip = "Makes players nearby you controllers vibrate."},
                        new ButtonInfo { buttonText = "Vibrate On Touch", method = Overpowered.VibrateOnTouch, toolTip = "Makes whoever you touch controllers vibrate."},
                        new ButtonInfo { buttonText = "Slow Gun", method = Overpowered.SlowGun, toolTip = "Forces tag freeze on whoever your hand desires." },
                        new ButtonInfo { buttonText = "Slow Aura", method = Overpowered.SlowAura, toolTip = "Forces tag freeze on players nearby you."},
                        new ButtonInfo { buttonText = "Slow On Touch", method = Overpowered.SlowOnTouch, toolTip = "Forces tag freeze on whoever you touch."},
                        new ButtonInfo { buttonText = "Unlock Fan Club Subscription", aliases = new[] { "Unlock VIM", "Unlock Very Cool Monke" }, enableMethod =() => SubscriptionPatches.enabled = true, disableMethod =() => SubscriptionPatches.enabled = false, toolTip = "Unlocks the Gorilla Tag fan club subscription. This mod is client-sided." },
                    };
                        Buttons.GetIndex("Exit Staff Tools").overlapText = $"Exit {toolsLabel}";
                        Buttons.buttons[Buttons.GetCategory("Staff Tools")] = Buttons.buttons[Buttons.GetCategory("Staff Tools")].Concat(newButtons).ToArray();
                        break;
                    }
            }
        }
    }
}