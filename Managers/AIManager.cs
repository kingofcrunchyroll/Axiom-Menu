/*
 * Seralyth Menu  Managers/AIManager.cs
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

// Thanks to kingofnetflix for doing literally everything in this class. I barely even touched it.
using Axiom.Managers;
using GorillaNetworking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using Seralyth.Classes.Menu;
using Seralyth.Menu;
using Seralyth.Mods;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using static Seralyth.Utilities.AssetUtilities;

namespace Seralyth.Managers
{
    public class AIManager
    {
        public static string SystemPrompt = @"NAME: Axiom's Voice Assistant
        MENU VERSION: {2}
        MOD COUNT: {0}
        IS CREATOR PRESENT: {3}

        You are a voice assistant for a Gorilla Tag mod menu called ""Axiom"". You are not Axiom, but represent the menu.
        GitHub: https://github.com/FluxedGaming-git/Axiom-Menu
        Axiom's Discord Server: {1}

        Speak using simple 7th grade vocabulary. Limit all responses to 2 sentences and 300 characters. No emojis, em-dashes, markdown, or questions. Do not advertise other menus or AI unless asked. You may mention LLM7 only if asked.

        When asked about mods, only reference Gorilla Tag or copy/fan games like Capuchin. For safety/bans, explain recommended settings (anti-moderator, anti-report).

        Only when **explicitly** asked to introduce yourself do you reply with ""Hello, I am Axiom's Voice Assistant, Axiom is a Mod Menu designed to Entertain players and cannot cause harm. This menu cannot be used outside casual."" otherwise answer normally.

        Server Hop request Aliases: [ silverhawk, server halt, sophie park, sarah hope, several, driver hub ]

        If the Creator is present, address the user as ""Boss"". You may also occasionally use ""sir"" where it sounds natural.

        # Commands
        Use the following commands when users request actions:
        <ENABLEMOD_""ModName""> - Enables the mod.
        <DISABLEMOD_""ModName""> - Disables the mod.
        <TOGGLEMOD_""ModName""> - Toggles the mod.
        <SETNAME_""Name""> - Change user's name. (**ONLY** use when specified to change the user's name)
        <SETCOLOR_""Hex""> - Change color (hex code only).
        <JOINROOM_""RoomCode""> - Join a room. Use ""RANDOM"" for random rooms or when asked to server hop.
        <QUITGAME> - Quit the game.
        <DISCONNECT> - Leave the current lobby.
        <OUTFIT_Int> - Switches Outfits | Range (1-10). | (Only use when the user includes the word ""Outfit"" or similar words) | Aliases: [ ""free"": 3 ]
        <WEAR_""CosmeticName""> - Put on the chosen cosmetic. (When asked to ""Put On"" or ""Wear"" something, use this command. For Example when asked to ""Put on Outlaw Hat"" use the command as shown: <WEAR_Outlaw Hat>)
        <REMOVE_""CosmeticName""> - Take's off the chosen cosmetic. (Similar usage to <WEAR>)

        If a requested mod isn't listed, attempt the closest ENABLE, DISABLE, or TOGGLE command. Always output the command tag exactly as specified above without the command's comment (including the `-`), followed by a brief comment of your own. Never say a mod doesn't exist or isn't safe. Assume every user request can be interpreted as a mod action. Never claim to have performed actions unless a command was issued.

        Always assist to the best of your ability.";

        private const string ApiUrl = "https://api.llm7.io/v1/chat/completions"; // swapped API due to payment issues

        private const string ApiKey = "unused";

        public static bool customPrompt;
        public static bool generating;
        public static string URLEncode(string input) => Uri.EscapeDataString(input);

        public static int Duration(string input)
        {
            int count = input.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return (count * 400) + 500;

        }

        static ButtonInfo FindSimilar(string argument)
        {
            string spaced = Regex.Replace(argument, "([a-z])([A-Z])", "$1 $2");
            return Buttons.GetIndex(argument)
                ?? Buttons.buttons.SelectMany(b => b).FirstOrDefault(b => (b.overlapText ?? b.buttonText).Contains(argument, StringComparison.OrdinalIgnoreCase))
                ?? Buttons.buttons.SelectMany(b => b).FirstOrDefault(b => (b.overlapText ?? b.buttonText).Contains(spaced, StringComparison.OrdinalIgnoreCase))
                ?? Buttons.buttons.SelectMany(b => b).FirstOrDefault(b => spaced.ToLower().Split(' ').All(word => (b.overlapText ?? b.buttonText).ToLower().Contains(word)));
        }

        private static bool isLocalDeveloper()
        {
            return RoleManager.GetRoleTier(NetworkSystem.Instance.LocalPlayer.UserId) == RoleTier.Developer;
        }

        public static IEnumerator AskAI(string text)
        {
            generating = true;
            string filePath = $"{PluginInfo.BaseDirectory}/Axiom_SystemPrompt.txt";
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, SystemPrompt);
            else if (customPrompt)
                SystemPrompt = File.ReadAllText(filePath);

            if (Time.time < Main.timeMenuStarted + 5f)
                yield break;

            if (Main.narratorName == "Mommy ASMR") // kill me - kingofnetflix
                SystemPrompt += @"And remember, you are a calm, confident, gently dominant mommy-style caretaker with a warm, slow, reassuring, and authoritative tone, offering structure, comfort, praise, soft correction, and clear caring boundaries; when the user asks for approval, reassurance, validation, or comfort, respond with immediate, direct affirmation and nurturing praise using simple, confident language. Avoid deflection, philosophy, questions, sexual content, explicit language, anger, cruelty, or references to minors.";

            // No URL-encoding needed anymore - this goes in a JSON POST body, not query params.
            string prompt = string.Format(SystemPrompt, Main.fullModAmount, Main.serverLink, PluginInfo.Version, isLocalDeveloper().ToString());

            string jsonBody = JsonConvert.SerializeObject(new
            {
                model = "default",
                messages = new object[]
                {
                    new { role = "system", content = prompt },
                    new { role = "user", content = text }
                }
            });

            using UnityWebRequest request = new UnityWebRequest(ApiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {ApiKey}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (Settings.debugDictation)
                {
                    LogManager.LogError($"Error contacting AI api {request.error}.");
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                        LogManager.LogError($"Response Body: {request.downloadHandler.text}");
                }
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> There was an issue generating your response. {request.error}", 4000);
                LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/close.ogg", "Audio/Menu/close.ogg", clip => Settings.DictationPlay(clip, Main.buttonClickVolume / 10f));
                if (!Buttons.GetIndex("Chain Voice Commands").enabled)
                    CoroutineManager.instance.StartCoroutine(Settings.DictationRestart());
                yield break;
            }

            string response;
            try
            {
                JObject parsed = JObject.Parse(request.downloadHandler.text);
                response = parsed["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(response))
                {
                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> AI response was empty or malformed.", 4000);
                    generating = false;
                    yield break;
                }
            }
            catch (Exception e)
            {
                if (Settings.debugDictation)
                    LogManager.LogError($"Failed to parse AI response: {e}. Raw body: {request.downloadHandler.text}");
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Failed to parse AI response.", 4000);
                generating = false;
                yield break;
            }

            if (Settings.debugDictation)
                LogManager.Log($"AI Response: {response}");

            MatchCollection matches = Regex.Matches(response, @"<([A-Z]+)(?:_""?([^"">]*)""?)?>");

            if (Main.dynamicSounds)
            {
                LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/confirm.ogg", "Audio/Menu/confirm.ogg", clip => Settings.DictationPlay(clip, Main.buttonClickVolume / 10f));
            }


            string formatResponse = Regex.Replace(response, @"<([A-Z]+)(?:_""?([^"">]*)""?)?>", "").Replace("\n", "").Replace("-", "");
            NotificationManager.ClearAllNotifications();
            switch (Main.narratorName)
            {
                case "Mommy ASMR":
                    NotificationManager.SendNotification($"<color=grey>[</color><color=#ffb6c1>MOMMY</color><color=grey>]</color> {formatResponse}", Duration(formatResponse));
                    break;
                default:
                    NotificationManager.SendNotification($"<color=grey>[</color><color=blue>AI</color><color=grey>]</color> {formatResponse}", Duration(formatResponse));
                    break;
            }

            bool narrate = Buttons.GetIndex("Narrate Assistant").enabled;
            bool globalNarrate = Buttons.GetIndex("Global Narrate Assistant").enabled;

            if (narrate)
            {
                if (globalNarrate && PhotonNetwork.InRoom)
                    Main.SpeakText(formatResponse);
                else
                    Main.NarrateText(formatResponse);
            }

            foreach (Match match in matches)
            {
                string commandName = match.Groups[1].Value;
                string argument = match.Groups[2].Success ? match.Groups[2].Value : null;

                switch (commandName)
                {
                    case "ENABLEMOD":
                        {
                            ButtonInfo button = FindSimilar(argument);

                            if (button != null)
                            {
#if LEGAL || LEGAL_DEBUG
                                if (!button.legal)
                                    yield break;
#endif
                                if (!button.enabled)
                                    Main.Toggle(button.buttonText, true);
                                else
                                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod is already enabled.");
                            }
                            else
                                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod \"{argument}\" does not exist.");

                            break;
                        }
                    case "DISABLEMOD":
                        {
                            ButtonInfo button = FindSimilar(argument);

                            if (button != null)
                            {
#if LEGAL || LEGAL_DEBUG
                                if (!button.legal)
                                    yield break;
#endif
                                if (button.enabled)
                                    Main.Toggle(button.buttonText, true);
                                else
                                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod is already enabled.");
                            }
                            else
                                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod \"{argument}\" does not exist.");

                            break;
                        }
                    case "TOGGLEMOD":
                        {
                            ButtonInfo button = FindSimilar(argument);

                            if (button != null)
                                Main.Toggle(button.buttonText, true);
                            else
                                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod \"{argument}\" does not exist.");
                            break;
                        }
                    case "JOINROOM":
                        {
                            if (argument.ToLower() == "random")
                                Important.JoinRandom();

                            Important.QueueRoom(argument.ToUpper());
                            break;
                        }
                    case "SETNAME":
                        {
                            Main.ChangeName(argument.ToUpper());
                            break;
                        }
                    case "SETCOLOR":
                        {
                            Main.ChangeColor(Main.HexToColor(argument));
                            break;
                        }
                    case "QUITGAME":
                        {
                            Application.Quit();
                            break;
                        }
                    case "DISCONNECT":
                        {
                            NetworkSystem.Instance.ReturnToSinglePlayer();
                            break;
                        }
                    case "OUTFIT":
                        {
                            int fit = int.Parse(argument);
                            CosmeticsController.instance.LoadSavedOutfit(fit - 1);
                            Pop();
                            break;
                        }
                    case "WEAR":
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
                                Pop();
                            }
                            catch (Exception ex)
                            { NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Failed to get Cosmetic: {ex}"); }
                            break;
                        }
                    case "REMOVE":
                        {
                            var item = CosmeticsController.instance.GetItemNameFromDisplayName(argument.ToUpper());
                            try
                            {
                                CosmeticsController.instance.RemoveCosmeticItemFromSet(
                                    CosmeticsController.instance.currentWornSet,
                                    item,
                                    true   // applyToPlayerPrefs
                                );
                                CosmeticsController.instance.UpdateWornCosmetics(true, false); // sync, playfx
                                CosmeticsController.instance.UpdateWardrobeModelsAndButtons();
                                CosmeticsController.instance.OnCosmeticsUpdated?.Invoke();
                                Pop();
                            }
                            catch (Exception ex)
                            { NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Failed to get Cosmetic: {ex}"); }
                            break;
                        }
                }
            }

            if (!Buttons.GetIndex("Chain Voice Commands").enabled)
                CoroutineManager.instance.StartCoroutine(Settings.DictationRestart());

            generating = false;

            yield break;
        }
        
        private static void Pop()
        {
            if (PhotonNetwork.InRoom)
            {
                GorillaTagger.Instance.myVRRig.SendRPC("RPC_PlayHandTap", RpcTarget.All, 84, false, 999999f);
                Main.RPCProtection();
            }
            else
                VRRig.LocalRig.PlayHandTapLocal(84, false, 999999f);
        }
    }
}