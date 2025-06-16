using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using HarmonyLib;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace QuickCastHotkeys
{
    public class Config
    {
        public string Notes0 = "Each hotkey applies to a tool slot (1-based).";
        public int[] HotkeyToolIndexes { get; set; } = Enumerable.Range(1, 12).ToArray();
        public SmartCastMode Mode { get; set; } = SmartCastMode.Windows;
        public bool DebugMode { get; set; } = false;
        public KeybindList[] Hotkeys { get; set; } = new KeybindList[]
        {
        new(SButton.Space), new(SButton.V),
        new(), new(), new(),
        new(), new(), new(),
        new(), new(), new(), new()
        };
    }


    public enum SmartCastMode
    {
        Windows,
        Linux
    }


    public class ModEntry : Mod
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;


        private bool[] IsHotkeyHeld = new bool[12];
        private bool[] NeedToSwitchBack = new bool[12];
        private bool isUsingTool = false;
        private int originalToolIndex = 0;
        public static bool[] HotkeyHeld = new bool[12];
        public static bool IsHotkeyActive = false;


        public Config Config { get; private set; }
        public static ModEntry Instance { get; private set; }

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<Config>() ?? new Config();
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !Game1.player.IsLocalPlayer)
                return;

            for (int i = 0; i < 12; i++)
                HotkeyHeld[i] = Config.Hotkeys[i]?.IsDown() ?? false;

            IsHotkeyActive = HotkeyHeld.Any(x => x);

            if (Config.Mode == SmartCastMode.Windows)
                AppliesSmartCastWindows();
            else
                AppliesSmartCastLinux();


            CheckIfDoneUsingTool();
        }

        private void AppliesSmartCastWindows()
        {
            // Do nothing if player is in a menu like Generic Mod Config Menu
            if (Game1.activeClickableMenu != null)
                return;

            Farmer player = Game1.player;

            for (int i = 0; i < 12; i++)
            {
                if (Config.Hotkeys[i] == null || Config.Hotkeys[i].Keybinds.Count() == 0)
                    continue;

                bool isHotkeyPressed = Config.Hotkeys[i].IsDown();

                // Prevent multiple hotkeys at once
                for (int j = 0; j < 12; j++)
                {
                    if (i != j && NeedToSwitchBack[j])
                    {
                        isHotkeyPressed = false;
                        break;
                    }
                }

                // Hotkey just pressed
                if (isHotkeyPressed && !IsHotkeyHeld[i])
                {
                    if (!isUsingTool)
                    {
                        isUsingTool = true;

                        if (!NeedToSwitchBack[i])
                        {
                            originalToolIndex = player.CurrentToolIndex;
                            player.CurrentToolIndex = Config.HotkeyToolIndexes[i] - 1;
                        }

                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        NeedToSwitchBack[i] = true;

                        Item currentItem = player.Items[player.CurrentToolIndex];
                        if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                        {
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                        }
                    }

                    IsHotkeyHeld[i] = true;
                }

                // Hotkey just released
                if (!isHotkeyPressed && IsHotkeyHeld[i])
                {
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                    Item currentItem = player.Items[player.CurrentToolIndex];
                    if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                    {
                        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    }

                    IsHotkeyHeld[i] = false;
                }
            }
        }

        private void AppliesSmartCastLinux()
        {
            // Do nothing if player is in a menu like Generic Mod Config Menu
            if (Game1.activeClickableMenu != null)
                return;

            Farmer player = Game1.player;

            for (int i = 0; i < 12; i++)
            {
                if (Config.Hotkeys[i] == null || Config.Hotkeys[i].Keybinds.Count() == 0)
                    continue;

                bool isHotkeyPressed = Config.Hotkeys[i].IsDown();

                // Prevent multiple hotkeys at once
                for (int j = 0; j < 12; j++)
                {
                    if (i != j && NeedToSwitchBack[j])
                    {
                        isHotkeyPressed = false;
                        break;
                    }
                }

                // Hotkey just pressed
                if (isHotkeyPressed && !IsHotkeyHeld[i])
                {
                    if (!isUsingTool)
                    {
                        isUsingTool = true;

                        if (!NeedToSwitchBack[i])
                        {
                            originalToolIndex = player.CurrentToolIndex;
                            player.CurrentToolIndex = Config.HotkeyToolIndexes[i] - 1;
                        }
                        DebugLog($"Hotkey {i + 1} pressed. Applying tool at slot {Config.HotkeyToolIndexes[i]}.");
                        RunXdotool("mousedown 1");
                        //mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        NeedToSwitchBack[i] = true;

                        Item currentItem = player.Items[player.CurrentToolIndex];
                        if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                        {
                            RunXdotool("mousedown 3");
                            //mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                        }
                    }

                    IsHotkeyHeld[i] = true;
                }

                // Hotkey just released
                if (!isHotkeyPressed && IsHotkeyHeld[i])
                {
                    DebugLog($"Hotkey {i + 1} released. Releasing tool.");
                    RunXdotool("mouseup 1");
                    //mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                    Item currentItem = player.Items[player.CurrentToolIndex];
                    if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                    {
                        RunXdotool("mouseup 3");
                        //mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    }

                    IsHotkeyHeld[i] = false;
                }
            }
        }

        //runs clickevent for linux
        private void RunXdotool(string args)
        {
            DebugLog($"Running xdotool {args}");

            try
            {
                var pi = new ProcessStartInfo
                {
                    FileName = Path.Combine(Helper.DirectoryPath, "xdotool"),
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(pi)!;
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    Monitor.Log($"xdotool failed: {proc.StandardError.ReadToEnd()}", LogLevel.Warn);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to call xdotool: {ex}", LogLevel.Error);
            }
        }

        //used to handle logging
        private void DebugLog(string message)
        {
            if (Config.DebugMode)
                Monitor.Log(message, LogLevel.Info);
        }


        private void CheckIfDoneUsingTool()
        {
            if (!Game1.player.UsingTool)
            {
                isUsingTool = false;

                for (int i = 0; i < 12; i++)
                {
                    if (!IsHotkeyHeld[i] && NeedToSwitchBack[i])
                    {
                        Game1.player.CurrentToolIndex = originalToolIndex;
                        NeedToSwitchBack[i] = false;
                    }
                }
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            SetupGMCM();
        }

        private void SetupGMCM()
        {
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                Monitor.Log("GMCM not found. Skipping config menu setup.", LogLevel.Warn);
                return;
            }

            gmcm.Register(
                mod: ModManifest,
                reset: () => Config = new Config(),
                save: () => Helper.WriteConfig(Config)
            );

            gmcm.AddTextOption(
                mod: ModManifest,
                name: () => "Operating System",
                tooltip: () => "Choose which Operating System your using to set the type of code to run, may require hard reset after saved changes. " +
                "Windows setting uses code to mimic click but only works on windows, " +
                "and Linux (steamdeck) logic option uses the xdotool compiled code to handle mouse clicks in Linux but require permissin to be set by entering in Linux Terminal: \n" +
                "chmod +x mods/QuickCastHotkeys/xdotool",
                getValue: () => Config.Mode.ToString(),
                setValue: value => Config.Mode = Enum.TryParse(value, out SmartCastMode mode) ? mode : SmartCastMode.Windows,
                allowedValues: Enum.GetNames(typeof(SmartCastMode))
            );

            gmcm.AddBoolOption(
                mod: ModManifest,
                name: () => "Enable Debug Mode Logging",
                tooltip: () => "If enabled, shows logs about xdotool commands and cast actions.",
                getValue: () => Config.DebugMode,
                setValue: value => Config.DebugMode = value
            );


            for (int i = 0; i < 12; i++)
            {
                int index = i;

                gmcm.AddKeybindList(
                    mod: ModManifest,
                    name: () => $"Hotkey {index + 1}",
                    tooltip: () => "Press this to trigger the tool slot below. Supports Shift/Ctrl combos.",
                    getValue: () => Config.Hotkeys[index],
                    setValue: value => Config.Hotkeys[index] = value
                );

                gmcm.AddNumberOption(
                    mod: ModManifest,
                    name: () => $"Tool Slot for Hotkey {index + 1}",
                    tooltip: () => "Which tool slot (1–12) this hotkey activates.",
                    getValue: () => Config.HotkeyToolIndexes[index],
                    setValue: value => Config.HotkeyToolIndexes[index] = value,
                    min: 1,
                    max: 12
                );
            }
        }







    }
}
