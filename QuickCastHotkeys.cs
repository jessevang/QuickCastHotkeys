using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using System.Reflection;
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

        private static readonly FieldInfo CurrentGamepadStateField =
            AccessTools.Field(typeof(InputState), "_currentGamepadState");

        private readonly bool[] IsHotkeyHeld = new bool[HotkeyCount];
        private readonly bool[] NeedToSwitchBack = new bool[HotkeyCount];

        private bool isUsingTool = false;
        private int originalToolIndex = 0;
        private GamePadState rawGamePadState;

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
                original: AccessTools.Method(typeof(InputState), nameof(InputState.UpdateStates)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(After_UpdateStates))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(InputState), nameof(InputState.GetMouseState)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(After_GetMouseState))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(InputState), nameof(InputState.GetGamePadState)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(After_GetGamePadState))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.isOneOfTheseKeysDown)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Before_IsOneOfTheseKeysDown))
            );

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        }

        public static void After_UpdateStates(InputState __instance)
        {
            if (Instance == null)
                return;

            if (CurrentGamepadStateField?.GetValue(__instance) is GamePadState state)
                Instance.rawGamePadState = state;
            else
                Instance.rawGamePadState = default;
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

        public static void After_GetGamePadState(ref GamePadState __result)
        {
            if (Instance == null)
                return;

            if (!Context.IsWorldReady || Game1.player == null || !Game1.player.IsLocalPlayer)
                return;

            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is not StardewValley.Menus.BobberBar)
                return;

            Buttons suppressedButtons = Instance.GetSuppressedControllerButtonsFromRawState();
            bool suppressLeftTrigger = Instance.ShouldSuppressRawControllerButton(SButton.LeftTrigger);
            bool suppressRightTrigger = Instance.ShouldSuppressRawControllerButton(SButton.RightTrigger);

            if (suppressedButtons == 0 && !suppressLeftTrigger && !suppressRightTrigger)
                return;

            Buttons buttons = Instance.GetButtonsFlags(__result);
            buttons &= ~suppressedButtons;

            GamePadTriggers triggers = __result.Triggers;
            if (suppressLeftTrigger || suppressRightTrigger)
            {
                triggers = new GamePadTriggers(
                    suppressLeftTrigger ? 0f : triggers.Left,
                    suppressRightTrigger ? 0f : triggers.Right
                );
            }

            GamePadDPad dpad = new GamePadDPad(
                suppressedButtons.HasFlag(Buttons.DPadUp) ? ButtonState.Released : __result.DPad.Up,
                suppressedButtons.HasFlag(Buttons.DPadDown) ? ButtonState.Released : __result.DPad.Down,
                suppressedButtons.HasFlag(Buttons.DPadLeft) ? ButtonState.Released : __result.DPad.Left,
                suppressedButtons.HasFlag(Buttons.DPadRight) ? ButtonState.Released : __result.DPad.Right
            );

            __result = new GamePadState(
                __result.ThumbSticks,
                triggers,
                new GamePadButtons(buttons),
                dpad
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

        private static bool IsControllerButton(SButton button)
        {
            return button == SButton.ControllerA
                || button == SButton.ControllerB
                || button == SButton.ControllerX
                || button == SButton.ControllerY
                || button == SButton.ControllerBack
                || button == SButton.ControllerStart
                || button == SButton.LeftShoulder
                || button == SButton.RightShoulder
                || button == SButton.LeftTrigger
                || button == SButton.RightTrigger
                || button == SButton.LeftStick
                || button == SButton.RightStick
                || button == SButton.DPadUp
                || button == SButton.DPadDown
                || button == SButton.DPadLeft
                || button == SButton.DPadRight;
        }

        private bool IsRawControllerButtonDown(SButton button)
        {
            if (button == SButton.LeftTrigger)
                return rawGamePadState.Triggers.Left > 0f;

            if (button == SButton.RightTrigger)
                return rawGamePadState.Triggers.Right > 0f;

            return TryConvertSButtonToButtons(button, out Buttons controllerButton)
                && rawGamePadState.IsButtonDown(controllerButton);
        }

        private bool IsHotkeyPressedForMod(int index)
        {
            KeybindList hotkey = Config.Hotkeys[index];
            if (hotkey == null || !hotkey.Keybinds.Any())
                return false;

            foreach (Keybind keybind in hotkey.Keybinds)
            {
                bool allButtonsDown = true;

                foreach (SButton button in keybind.Buttons)
                {
                    if (IsControllerButton(button))
                    {
                        if (!IsRawControllerButtonDown(button))
                        {
                            allButtonsDown = false;
                            break;
                        }
                    }
                    else
                    {
                        if (!Helper.Input.IsDown(button))
                        {
                            allButtonsDown = false;
                            break;
                        }
                    }
                }

                if (allButtonsDown)
                    return true;
            }

            return false;
        }

        private bool ShouldSuppressRawControllerButton(SButton button)
        {
            if (!IsRawControllerButtonDown(button))
                return false;

            return IsButtonBoundInThisMod(button);
        }

        private Buttons GetSuppressedControllerButtonsFromRawState()
        {
            Buttons result = 0;

            SButton[] candidates = new[]
            {
                SButton.ControllerA,
                SButton.ControllerB,
                SButton.ControllerX,
                SButton.ControllerY,
                SButton.ControllerBack,
                SButton.ControllerStart,
                SButton.LeftShoulder,
                SButton.RightShoulder,
                SButton.LeftStick,
                SButton.RightStick,
                SButton.DPadUp,
                SButton.DPadDown,
                SButton.DPadLeft,
                SButton.DPadRight
            };

            foreach (SButton button in candidates)
            {
                if (!ShouldSuppressRawControllerButton(button))
                    continue;

                if (TryConvertSButtonToButtons(button, out Buttons controllerButton))
                    result |= controllerButton;
            }

            return result;
        }

        private static bool TryConvertSButtonToButtons(SButton button, out Buttons result)
        {
            result = 0;

            switch (button)
            {
                case SButton.ControllerA:
                    result = Buttons.A;
                    return true;
                case SButton.ControllerB:
                    result = Buttons.B;
                    return true;
                case SButton.ControllerX:
                    result = Buttons.X;
                    return true;
                case SButton.ControllerY:
                    result = Buttons.Y;
                    return true;
                case SButton.ControllerBack:
                    result = Buttons.Back;
                    return true;
                case SButton.ControllerStart:
                    result = Buttons.Start;
                    return true;
                case SButton.LeftShoulder:
                    result = Buttons.LeftShoulder;
                    return true;
                case SButton.RightShoulder:
                    result = Buttons.RightShoulder;
                    return true;
                case SButton.LeftStick:
                    result = Buttons.LeftStick;
                    return true;
                case SButton.RightStick:
                    result = Buttons.RightStick;
                    return true;
                case SButton.DPadUp:
                    result = Buttons.DPadUp;
                    return true;
                case SButton.DPadDown:
                    result = Buttons.DPadDown;
                    return true;
                case SButton.DPadLeft:
                    result = Buttons.DPadLeft;
                    return true;
                case SButton.DPadRight:
                    result = Buttons.DPadRight;
                    return true;
                default:
                    return false;
            }
        }

        private Buttons GetButtonsFlags(GamePadState state)
        {
            Buttons result = 0;

            if (state.IsButtonDown(Buttons.A))
                result |= Buttons.A;
            if (state.IsButtonDown(Buttons.B))
                result |= Buttons.B;
            if (state.IsButtonDown(Buttons.X))
                result |= Buttons.X;
            if (state.IsButtonDown(Buttons.Y))
                result |= Buttons.Y;

            if (state.IsButtonDown(Buttons.Back))
                result |= Buttons.Back;
            if (state.IsButtonDown(Buttons.Start))
                result |= Buttons.Start;

            if (state.IsButtonDown(Buttons.LeftShoulder))
                result |= Buttons.LeftShoulder;
            if (state.IsButtonDown(Buttons.RightShoulder))
                result |= Buttons.RightShoulder;

            if (state.IsButtonDown(Buttons.LeftStick))
                result |= Buttons.LeftStick;
            if (state.IsButtonDown(Buttons.RightStick))
                result |= Buttons.RightStick;

            if (state.IsButtonDown(Buttons.DPadUp))
                result |= Buttons.DPadUp;
            if (state.IsButtonDown(Buttons.DPadDown))
                result |= Buttons.DPadDown;
            if (state.IsButtonDown(Buttons.DPadLeft))
                result |= Buttons.DPadLeft;
            if (state.IsButtonDown(Buttons.DPadRight))
                result |= Buttons.DPadRight;

            return result;
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
                if (Config.Hotkeys[i] == null || !Config.Hotkeys[i].Keybinds.Any())
                    continue;

                bool isHotkeyPressed = IsHotkeyPressedForMod(i);

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
            rawGamePadState = default;

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