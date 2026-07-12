using System;
using System.Collections.Generic;
using System.Linq;
using Seralyth.Classes.Menu;   // for ButtonInfo, Buttons
using Seralyth.Utilities;      // for LogManager
using Seralyth.Managers;       // for NotificationManager
using Seralyth.Menu;           // for Main
using GorillaNetworking;      // for GorillaGameManager
using GorillaLocomotion;
using Photon.Pun;             // for PhotonNetwork

namespace Seralyth.Mods
{
    public static class GameModeLock
    {
        private static List<string> preLockEnabledMods = new List<string>();
        private static bool wasLocked;

        public static bool IsCasual()
        {
            // TODO: confirm the real accessor
             return GorillaGameManager.instance.GameType().ToString().ToUpperInvariant().Contains("CASUAL");
        }

        public static bool IsPrivateOrModded()
        {
            return GorillaGameManager.instance.GameType().ToString().ToUpperInvariant().Contains("MODDED") || !PhotonNetwork.CurrentRoom.IsVisible;
        }

        public static void CheckAndUpdate()
        {
            bool shouldLock = !IsCasual() || !IsPrivateOrModded();

            if (shouldLock && !wasLocked)
                LockDown();
            else if (!shouldLock && wasLocked)
                Unlock();

            wasLocked = shouldLock;
        }

        private static void LockDown()
        {
            preLockEnabledMods.Clear();

            foreach (ButtonInfo button in Buttons.buttons.SelectMany(list => list))
            {
                if (!button.enabled || !button.isTogglable) continue;

                preLockEnabledMods.Add(button.buttonText);

                button.enabled = false;
                try { button.disableMethod?.Invoke(); }
                catch (Exception exc) { LogManager.LogError($"Error disabling {button.buttonText}: {exc.Message}"); }
            }

            Main.Lockdown = true;

            if (Main.menu != null)
                Main.CloseMenu();

            NotificationManager.SendNotification(
                "<color=grey>[</color><color=red>LOCKED</color><color=grey>]</color> Axiom is disabled outside Casual gamemodes.",
                4000
            );
        }

        private static void Unlock()
        {
            Main.Lockdown = false;

            foreach (string modName in preLockEnabledMods)
            {
                ButtonInfo button = Buttons.GetIndex(modName);
                if (button == null) continue;

                button.enabled = true;
                try { button.enableMethod?.Invoke(); }
                catch (Exception exc) { LogManager.LogError($"Error re-enabling {button.buttonText}: {exc.Message}"); }
            }

            preLockEnabledMods.Clear();

            NotificationManager.SendNotification(
                "<color=grey>[</color><color=green>UNLOCKED</color><color=grey>]</color> Axiom is available again.",
                4000
            );
        }
    }
}