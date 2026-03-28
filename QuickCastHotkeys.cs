using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using GenericModConfigMenu;
using HarmonyLib;

namespace QuickCastHotkeys
{
    public class Config
    {
        public string Notes0 = "Each hotkey applies to a tool slot (1-based).";
        public int[] HotkeyToolIndexes { get; set; } = Enumerable.Range(1, 10).ToArray();

        public KeybindList[] Hotkeys { get; set; } = new KeybindList[]
        {
            new(SButton.Space), new(SButton.V),
            new(), new(), new(),
            new(), new(), new(),
            new(), new()
        };

        public bool EnableDebugLogging { get; set; } = false;
    }

    internal sealed class ModEntry : Mod
    {
        private const int HotkeyCount = 10;

        private readonly bool[] IsHotkeyHeld = new bool[HotkeyCount];
        private readonly bool[] NeedToSwitchBack = new bool[HotkeyCount];
        private bool isUsingTool = false;
        private int originalToolIndex = 0;

        private Harmony harmony = null!;

        public Config Config { get; private set; } = null!;
        public static ModEntry Instance { get; private set; } = null!;

        internal static bool ForceLeftHeld;
        internal static bool ForceRightHeld;
        internal static bool ForceLeftPressFrame;
        internal static bool ForceLeftReleaseFrame;
        internal static bool ForceRightPressFrame;
        internal static bool ForceRightReleaseFrame;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<Config>() ?? new Config();

            harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(InputState), nameof(InputState.GetMouseState)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(After_GetMouseState))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.isOneOfTheseKeysDown)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Before_IsOneOfTheseKeysDown))
            );

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        }

        public static void After_GetMouseState(ref MouseState __result)
        {
            if (Instance == null)
                return;

            if (!Context.IsWorldReady || Game1.player == null || !Game1.player.IsLocalPlayer)
                return;

            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is not StardewValley.Menus.BobberBar)
                return;

            ButtonState left = __result.LeftButton;
            ButtonState right = __result.RightButton;
            bool changed = false;

            if (ForceLeftPressFrame)
            {
                left = ButtonState.Pressed;
                changed = true;
            }
            else if (ForceLeftHeld)
            {
                left = ButtonState.Pressed;
                changed = true;
            }
            else if (ForceLeftReleaseFrame)
            {
                left = ButtonState.Released;
                changed = true;
            }

            if (ForceRightPressFrame)
            {
                right = ButtonState.Pressed;
                changed = true;
            }
            else if (ForceRightHeld)
            {
                right = ButtonState.Pressed;
                changed = true;
            }
            else if (ForceRightReleaseFrame)
            {
                right = ButtonState.Released;
                changed = true;
            }

            if (!changed)
                return;

            __result = new MouseState(
                __result.X,
                __result.Y,
                __result.ScrollWheelValue,
                left,
                __result.MiddleButton,
                right,
                __result.XButton1,
                __result.XButton2
            );
        }

        public static bool Before_IsOneOfTheseKeysDown(ref bool __result, InputButton[] keys)
        {
            if (Instance == null)
                return true;

            if (!Context.IsWorldReady || Game1.player == null || !Game1.player.IsLocalPlayer)
                return true;

            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is not StardewValley.Menus.BobberBar)
                return true;

            if (keys == null || keys.Length == 0)
                return true;

            foreach (InputButton key in keys)
            {
                if (!TryConvertInputButtonToSButton(key, out SButton sButton))
                    continue;

                if (Instance.IsButtonBoundInThisMod(sButton))
                {
                    __result = false;
                    return false;
                }
            }

            return true;
        }

        private static bool TryConvertInputButtonToSButton(InputButton inputButton, out SButton result)
        {
            result = SButton.None;

            string name = inputButton.ToString();
            return Enum.TryParse(name, ignoreCase: true, result: out result);
        }

        private bool IsButtonBoundInThisMod(SButton button)
        {
            for (int i = 0; i < HotkeyCount; i++)
            {
                KeybindList hotkey = Config.Hotkeys[i];
                if (hotkey == null || !hotkey.Keybinds.Any())
                    continue;

                foreach (Keybind keybind in hotkey.Keybinds)
                {
                    if (keybind.Buttons.Contains(button))
                        return true;
                }
            }

            return false;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !Game1.player.IsLocalPlayer)
                return;

            AppliesSmartCast();
            CheckIfDoneUsingTool();

            if (ForceLeftPressFrame || ForceLeftReleaseFrame || ForceRightPressFrame || ForceRightReleaseFrame)
            {
                ForceLeftPressFrame = false;
                ForceLeftReleaseFrame = false;
                ForceRightPressFrame = false;
                ForceRightReleaseFrame = false;
            }
        }

        private void AppliesSmartCast()
        {
            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is not StardewValley.Menus.BobberBar)
                return;

            Farmer player = Game1.player;

            for (int i = 0; i < HotkeyCount; i++)
            {
                if (Config.Hotkeys[i] == null || Config.Hotkeys[i].Keybinds.Count() == 0)
                    continue;

                bool isHotkeyPressed = Config.Hotkeys[i].IsDown();

                for (int j = 0; j < HotkeyCount; j++)
                {
                    if (i != j && NeedToSwitchBack[j])
                    {
                        isHotkeyPressed = false;
                        break;
                    }
                }

                if (isHotkeyPressed && !IsHotkeyHeld[i])
                {
                    bool allowWhileFishing =
                        Game1.activeClickableMenu is StardewValley.Menus.BobberBar
                        || player.CurrentTool is StardewValley.Tools.FishingRod;

                    if (!isUsingTool || allowWhileFishing)
                    {
                        if (!isUsingTool)
                        {
                            isUsingTool = true;

                            if (!NeedToSwitchBack[i])
                            {
                                originalToolIndex = player.CurrentToolIndex;
                                player.CurrentToolIndex = Config.HotkeyToolIndexes[i] - 1;
                            }

                            NeedToSwitchBack[i] = true;
                        }

                        BeginForcedLeftClick();

                        Item currentItem = player.Items[player.CurrentToolIndex];
                        if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                        {
                            BeginForcedRightClick();
                        }
                    }

                    IsHotkeyHeld[i] = true;
                }

                if (!isHotkeyPressed && IsHotkeyHeld[i])
                {
                    EndForcedLeftClick();

                    Item currentItem = player.Items[player.CurrentToolIndex];
                    if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                    {
                        EndForcedRightClick();
                    }

                    IsHotkeyHeld[i] = false;
                }
            }
        }

        private void BeginForcedLeftClick()
        {
            ForceLeftPressFrame = true;
            ForceLeftHeld = true;
            ForceLeftReleaseFrame = false;
        }

        private void EndForcedLeftClick()
        {
            ForceLeftHeld = false;
            ForceLeftReleaseFrame = true;
        }

        private void BeginForcedRightClick()
        {
            ForceRightPressFrame = true;
            ForceRightHeld = true;
            ForceRightReleaseFrame = false;
        }

        private void EndForcedRightClick()
        {
            ForceRightHeld = false;
            ForceRightReleaseFrame = true;
        }

        private void CheckIfDoneUsingTool()
        {
            if (Game1.activeClickableMenu is StardewValley.Menus.BobberBar)
                return;

            if (!Game1.player.UsingTool)
            {
                isUsingTool = false;

                for (int i = 0; i < HotkeyCount; i++)
                {
                    if (!IsHotkeyHeld[i] && NeedToSwitchBack[i])
                    {
                        Game1.player.CurrentToolIndex = originalToolIndex;
                        NeedToSwitchBack[i] = false;
                    }
                }
            }
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            ForceLeftHeld = false;
            ForceRightHeld = false;
            ForceLeftPressFrame = false;
            ForceLeftReleaseFrame = false;
            ForceRightPressFrame = false;
            ForceRightReleaseFrame = false;
            isUsingTool = false;
            originalToolIndex = 0;

            for (int i = 0; i < HotkeyCount; i++)
            {
                IsHotkeyHeld[i] = false;
                NeedToSwitchBack[i] = false;
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

            for (int i = 0; i < HotkeyCount; i++)
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