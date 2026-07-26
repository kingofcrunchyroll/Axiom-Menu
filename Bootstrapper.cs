/*
 * Seralyth Menu  Bootstrapper.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Seralyth Software
 * https://github.com/Seralyth/Seralyth-Menu
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Seralyth.Classes.Menu;
using Seralyth.Managers;
using Seralyth.Menu;
using Seralyth.Patches;
using Seralyth.Patches.Menu;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Axiom.Managers;
using Photon.Pun;

namespace Seralyth
{
    internal static class Bootstrapper
    {
        private static bool initialized;
        public static bool FirstLaunch;
        public static GameObject Loader;

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            FirstLaunch = !Directory.Exists(PluginInfo.BaseDirectory);

            string[] existingDirectories =
            {
                "",
                "/Sounds",
                "/Plugins",
                "/Backups",
                "/Macros",
                "/TTS",
                "/PlayerInfo",
                "/CustomScripts",
                "/Friends",
                "/Friends/Messages",
                "/Achievements"
            };

            foreach (string dir in existingDirectories)
            {
                string target = $"{PluginInfo.BaseDirectory}{dir}";
                if (!Directory.Exists(target))
                    Directory.CreateDirectory(target);
            }

            PatchHandler.PatchAll(true);

            if (File.Exists($"{PluginInfo.BaseDirectory}/Seralyth_Preferences.txt"))
            {
                if (File.ReadAllLines($"{PluginInfo.BaseDirectory}/Seralyth_Preferences.txt")[0]
                    .Split(";;")
                    .Contains("Accept TOS"))
                {
                    TOSPatches.enabled = true;
                }
            }

            if (File.Exists($"{PluginInfo.BaseDirectory}/Seralyth_DisableTelemetry.txt"))
                ServerData.DisableTelemetry = true;

            GorillaTagger.OnPlayerSpawned(LoadMenu);
        }

        private static void LoadMenu()
        {
            PatchHandler.PatchAll();

            Loader = new GameObject("Seralyth_Loader");
            CoroutineManager coroutineManager = Loader.AddComponent<CoroutineManager>();
            Loader.AddComponent<NotificationManager>();
            Loader.AddComponent<CustomBoardManager>();
            Loader.AddComponent<UI>();
            Loader.AddComponent<NetworkedIconManager>();
            Loader.AddComponent<FriendManager>();
            UnityEngine.Object.DontDestroyOnLoad(Loader);

            coroutineManager.StartCoroutine(PatchIntegrityCheck());
            RoleManager.StartPolling(coroutineManager);
            BlacklistManager.StartPolling(coroutineManager);
            coroutineManager.StartCoroutine(MenuLockManager.CheckAndEnforce(coroutineManager));
        }

        private static IEnumerator PatchIntegrityCheck()
        {
            if (PatchHandler.instance == null)
                yield return null;

            PatchHandler.PatchIntegrityCheck();
        }
    }
}
