using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Runtime.InteropServices;
using System;
using System.Linq;
using GenericModConfigMenu;

namespace QuickCastHotkeys
{
    public class Config
    {
        public string Notes0 = "Each hotkey applies to a tool slot (1-based).";
        public int[] HotkeyToolIndexes { get; set; } = Enumerable.Range(1, 10).ToArray();

        public SButton[] Hotkeys { get; set; } = new SButton[]
        {
            SButton.Space, SButton.V,
            SButton.None, SButton.None, SButton.None,
            SButton.None, SButton.None, SButton.None,
            SButton.None, SButton.None
        };
    }

    internal sealed class ModEntry : Mod
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        private bool[] IsHotkeyHeld = new bool[10];
        private bool[] NeedToSwitchBack = new bool[10];
        private bool isUsingTool = false;
        private int originalToolIndex = 0;

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

            AppliesSmartCast();
            CheckIfDoneUsingTool();
        }

        private void AppliesSmartCast()
        {
            //Do nothing if player is in a menu like Generic Mod Config Menu
            if (Game1.activeClickableMenu != null)
                return;


            Farmer player = Game1.player;

            for (int i = 0; i < 10; i++)
            {
                if (Config.Hotkeys[i] == SButton.None)
                    continue;

                bool isHotkeyPressed = Helper.Input.IsDown(Config.Hotkeys[i]);

                // Prevent multiple hotkeys at once
                for (int j = 0; j < 10; j++)
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

        private void CheckIfDoneUsingTool()
        {
            if (!Game1.player.UsingTool)
            {
                isUsingTool = false;

                for (int i = 0; i < 10; i++)
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

            for (int i = 0; i < 10; i++)
            {
                int index = i;

                gmcm.AddKeybind(
                    mod: ModManifest,
                    name: () => $"Hotkey {index + 1}",
                    tooltip: () => "Press this to trigger the tool slot below.",
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
