using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using GorillaNetworking;
using System.IO;

namespace Gemstone.Gemstone
{
    internal class Gui : MonoBehaviour
    {
        private Rect connectionWindowRect = new Rect(20, 20, 250, 160);
        private Rect modsWindowRect = new Rect(280, 20, 300, 420);

        private Vector2 modScrollPosition = Vector2.zero;

        private int currentGuiTab = 0;
        private Vector2 playerScrollPosition = Vector2.zero;
        private Vector2 soundboardScrollPosition = Vector2.zero;
        private Vector2 adminScrollPosition = Vector2.zero;

        private Player selectedPlayer = null;
        private bool inPlayerSubmenu = false;

        private string roomToJoin = "";

        private void OnGUI()
        {
            GUI.skin.window.margin = new RectOffset(5, 5, 5, 5);
            GUI.skin.window.padding = new RectOffset(10, 10, 10, 10);

            Color originalBackgroundColor = GUI.backgroundColor;
            Color originalContentColor = GUI.contentColor;
            Color originalColor = GUI.color;

            if (ModConfig.instance != null)
            {
                GUI.backgroundColor = ModConfig.Theme;
            }

            connectionWindowRect = GUI.Window(0, connectionWindowRect, DrawConnectionWindow, "");
            modsWindowRect = GUI.Window(1, modsWindowRect, DrawModsWindow, "");

            GUI.backgroundColor = originalBackgroundColor;
            GUI.contentColor = originalContentColor;
            GUI.color = originalColor;
        }

        private void DrawConnectionWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 250, 20));

            GUILayout.Space(5);

            AddButton("Disconnect", () =>
            {
                if (PhotonNetwork.InRoom)
                {
                    PhotonNetwork.Disconnect();
                }
            });
            GUILayout.Space(5);

            AddButton("Quit", () =>
            {
                Application.Quit();
            });
        }

        private void DrawModsWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 300, 20));

            GUILayout.Space(5);

            if (ModConfig.instance == null)
            {
                GUILayout.Label("ModConfig is null");
                return;
            }

            Color originalBackgroundColor = GUI.backgroundColor;
            if (ModConfig.instance != null)
            {
                GUI.backgroundColor = ModConfig.Theme;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(currentGuiTab == 0, "Mods", "Button", GUILayout.Height(25))) currentGuiTab = 0;
            if (GUILayout.Toggle(currentGuiTab == 1, "Players", "Button", GUILayout.Height(25))) currentGuiTab = 1;
            if (GUILayout.Toggle(currentGuiTab == 2, "Sounds", "Button", GUILayout.Height(25))) currentGuiTab = 2;
            if (Plugin.instance != null && Plugin.instance.IsAdmin)
            {
                if (GUILayout.Toggle(currentGuiTab == 3, "Admin", "Button", GUILayout.Height(25))) currentGuiTab = 3;
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = originalBackgroundColor;

            GUILayout.Space(5);

            switch (currentGuiTab)
            {
                case 0:
                    DrawStandardMods();
                    break;
                case 1:
                    DrawPlayerListMenu();
                    break;
                case 2:
                    DrawSoundboardMenu();
                    break;
                case 3:
                    if (Plugin.instance != null && Plugin.instance.IsAdmin)
                    {
                        DrawAdminMenu();
                    }
                    else
                    {
                        currentGuiTab = 0;
                    }
                    break;
            }
        }

        private void DrawStandardMods()
        {
            modScrollPosition = GUILayout.BeginScrollView(modScrollPosition, GUILayout.Width(280), GUILayout.Height(320));
            DrawModToggle(Localization.Get("Fly"), ModConfig.instance.FlyEnabled.Value, ModConfig.instance.FlyEnabled);
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Noclip"), ModConfig.instance.IsNoclipEnabled.Value, ModConfig.instance.IsNoclipEnabled);
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("WASD Fly"), ModConfig.instance.IsWasdFly.Value, ModConfig.instance.IsWasdFly);
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Bypass Automod"), ModConfig.instance.IsBypassAutoMod.Value, ModConfig.instance.IsBypassAutoMod);
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Box ESP"), ModConfig.instance.IsBoxEsp.Value, ModConfig.instance.IsBoxEsp, () => Mods.Mods.CleanupBoxEsp());

            GUILayout.Space(5);

            GUILayout.Label("Room Joiner:");
            roomToJoin = GUILayout.TextField(roomToJoin, GUILayout.Height(22));
            GUILayout.Space(2);
            DrawModButton("Join Room", () =>
            {
                if (!string.IsNullOrEmpty(roomToJoin))
                {
                    PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(roomToJoin.ToUpper(), JoinType.Solo);
                }
            });

            GUILayout.Space(5);
            DrawModButton(Localization.Get("Mute All"), () =>
            {
                Mods.Mods.MuteAll();
            });
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Unmute All"), () =>
            {
                Mods.Mods.UnmuteAll();
            });
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Anti Report"), ModConfig.instance.IsAntiReportEnabled.Value, ModConfig.instance.IsAntiReportEnabled);
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Full Body Tracking"), ModConfig.instance.FullBodyTracking.Value, ModConfig.instance.FullBodyTracking, () => Mods.Mods.FixRig());
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Ragdoll"), ModConfig.instance.IsRagdoll.Value, ModConfig.instance.IsRagdoll, () => Mods.Mods.FixRig());
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("WASD Walk"), ModConfig.instance.IsWasdWalk.Value, ModConfig.instance.IsWasdWalk);
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Movement Recorder"), ModConfig.instance.MovementRecorder.Value, ModConfig.instance.MovementRecorder);
            GUILayout.EndScrollView();
        }

        private void DrawPlayerListMenu()
        {
            playerScrollPosition = GUILayout.BeginScrollView(playerScrollPosition, GUILayout.Width(280), GUILayout.Height(320));

            if (!inPlayerSubmenu)
            {
                Player[] players = PhotonNetwork.PlayerList;
                if (players == null || players.Length == 0)
                {
                    GUILayout.Label("No Players In Room");
                }
                else
                {
                    foreach (Player player_ in players)
                    {
                        if (player_ == null) continue;
                        DrawModButton(player_.NickName, () =>
                        {
                            selectedPlayer = player_;
                            inPlayerSubmenu = true;
                        });
                        GUILayout.Space(5);
                    }
                }
            }
            else
            {
                if (selectedPlayer == null)
                {
                    inPlayerSubmenu = false;
                    GUILayout.EndScrollView();
                    return;
                }

                GUILayout.Label($"Selected: {selectedPlayer.NickName}");
                GUILayout.Space(5);

                DrawModButton(Localization.Get("Back"), () =>
                {
                    inPlayerSubmenu = false;
                    selectedPlayer = null;
                });
                GUILayout.Space(5);

                DrawModButton(Localization.Get("Teleport to"), () =>
                {
                    if (selectedPlayer != null && Plugin.instance != null)
                    {
                        Plugin.instance.StartCoroutine(Mods.Mods.TpToPlayer(selectedPlayer.UserId));
                    }
                });
                GUILayout.Space(5);

                DrawModButton(Localization.Get("Custom Properties"), () =>
                {
                    if (selectedPlayer == null) return;

                    string output = $"Player: {selectedPlayer.NickName}\n\nCustom Properties:\n";

                    if (selectedPlayer.CustomProperties != null && selectedPlayer.CustomProperties.Count > 0)
                    {
                        foreach (var kvp in selectedPlayer.CustomProperties)
                        {
                            output += $"{kvp.Key}: {kvp.Value}\n";
                        }
                    }
                    else
                    {
                        output += "None";
                    }

                    NotiLib.SendNotification(output, 4000f);
                });
                GUILayout.Space(5);

                DrawModButton(Localization.Get("Block Player"), () =>
                {
                    if (selectedPlayer == null) return;

                    string userId = selectedPlayer.UserId;

                    foreach (var rig in VRRigCache.ActiveRigs)
                    {
                        if (rig == null || rig.Creator == null) continue;

                        if (rig.Creator.UserId == userId && rig.Creator.UserId != PhotonNetwork.LocalPlayer.UserId)
                        {
                            rig.gameObject.SetActive(false);
                        }
                    }

                    NotiLib.SendNotification($"Blocked {selectedPlayer.NickName}", 2000f);
                });
            }

            GUILayout.EndScrollView();
        }

        private void DrawSoundboardMenu()
        {
            soundboardScrollPosition = GUILayout.BeginScrollView(soundboardScrollPosition, GUILayout.Width(280), GUILayout.Height(320));

            var reflectionFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
            var clipsField = typeof(Plugin).GetField("soundboardClips", reflectionFlags);
            var currentlyPlayingField = typeof(Plugin).GetField("currentlyPlayingClip", reflectionFlags);

            List<AudioClip> soundboardClips = clipsField?.GetValue(Plugin.instance) as List<AudioClip>;
            AudioClip currentlyPlayingClip = currentlyPlayingField?.GetValue(null) as AudioClip;

            if (soundboardClips == null || soundboardClips.Count == 0)
            {
                GUILayout.Label("No Sounds Found");
            }
            else
            {
                foreach (AudioClip selectedClip in soundboardClips)
                {
                    if (selectedClip == null) continue;
                    string btnText = (currentlyPlayingClip == selectedClip) ? $"[PLAYING] {selectedClip.name}" : selectedClip.name;

                    DrawModButton(btnText, () =>
                    {
                        Plugin.ToggleSoundboard(selectedClip);
                    });
                    GUILayout.Space(5);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawAdminMenu()
        {
            adminScrollPosition = GUILayout.BeginScrollView(adminScrollPosition, GUILayout.Width(280), GUILayout.Height(320));

            DrawModToggle(Localization.Get("Admin Laser"), ModConfig.instance.AdminLaser.Value, ModConfig.instance.AdminLaser);
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Travis Scott"), ModConfig.instance.IsTravis.Value, ModConfig.instance.IsTravis, () => Mods.Mods.NoTravis());
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Tv"), ModConfig.instance.IsTv.Value, ModConfig.instance.IsTv, () => Mods.Mods.NoTv());
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Phone"), ModConfig.instance.IsPhone.Value, ModConfig.instance.IsPhone, () => Mods.Mods.NoSamsung());
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Twerking Carti"), ModConfig.instance.IsTwerkingCarti.Value, ModConfig.instance.IsTwerkingCarti, () => Mods.Mods.NoCarti());
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Grab All"), ModConfig.instance.IsAdminGrab.Value, ModConfig.instance.IsAdminGrab);
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Roblox Sword"), ModConfig.instance.IsCoolSword.Value, ModConfig.instance.IsCoolSword, () => Mods.Mods.NoSword());
            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Kormakur Sign"), ModConfig.instance.IsKormakur.Value, ModConfig.instance.IsKormakur, () => Mods.Mods.NoSign());

            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid Hell"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/GirlHell1999.mp4");
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid OCD"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/OCD.mp4");
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid Kitty"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/Kitty.mp4");
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid AMV"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/testvid.mp4");
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid theresabarrier"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/theresabarrier.mp4");
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid Edit"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/edit.mp4");

            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Cherry bomb"), ModConfig.instance.IsCherryBomb.Value, ModConfig.instance.IsCherryBomb, () => Mods.Mods.NoCherryBomb());

            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid Zlothy"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/Zlothy.mov");
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid Barrier Remix"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/there%20is%20a%20barrier%20remix.mp4");
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid invincible wobbly edit"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/INVINCIBLEWOBBLYANIMATION.mp4");
            GUILayout.Space(5);
            DrawModButton(Localization.Get("Vid Punch Mod"), () => Mods.Mods.Video = "https://github.com/ChipLikesCereal/testvid/raw/refs/heads/main/punchmod.mp4");

            GUILayout.Space(5);
            DrawModToggle(Localization.Get("Big Assets"), ModConfig.instance.IsBigAssets.Value, ModConfig.instance.IsBigAssets);

            GUILayout.EndScrollView();
        }

        private void AddButton(string label, Action onClickAction)
        {
            Color originalBackgroundColor = GUI.backgroundColor;
            if (ModConfig.instance != null)
            {
                GUI.backgroundColor = ModConfig.Theme;
            }

            if (GUILayout.Button(label, GUILayout.Height(30)))
            {
                onClickAction?.Invoke();

                if (Plugin.instance != null)
                {
                    Plugin.instance.audioSource?.PlayOneShot(Plugin.instance.audioSource.clip);
                }
            }

            GUI.backgroundColor = originalBackgroundColor;
        }

        private void DrawModButton(string title, Action onPressedAction)
        {
            Color originalBackgroundColor = GUI.backgroundColor;
            if (ModConfig.instance != null)
            {
                GUI.backgroundColor = ModConfig.Theme;
            }

            if (GUILayout.Button(title, GUILayout.Height(25)))
            {
                onPressedAction?.Invoke();

                if (Plugin.instance != null)
                {
                    Plugin.instance.audioSource?.PlayOneShot(Plugin.instance.audioSource.clip);
                }
            }

            GUI.backgroundColor = originalBackgroundColor;
        }

        private void DrawModToggle(string title, bool enabled, BepInEx.Configuration.ConfigEntry<bool> configEntry, Action onDisable = null)
        {
            if (configEntry == null) return;

            Color originalBackgroundColor = GUI.backgroundColor;
            if (ModConfig.instance != null)
            {
                GUI.backgroundColor = enabled ? ModConfig.Theme : originalBackgroundColor;
            }

            bool newState = GUILayout.Toggle(enabled, $" {title}", GUILayout.Height(22));

            GUI.backgroundColor = originalBackgroundColor;

            if (newState != enabled)
            {
                configEntry.Value = newState;

                if (Plugin.instance != null)
                {
                    Plugin.instance.Config.Save();
                    Plugin.instance.audioSource?.PlayOneShot(Plugin.instance.audioSource.clip);
                }

                if (!newState)
                {
                    onDisable?.Invoke();
                }
            }
        }
    }
}