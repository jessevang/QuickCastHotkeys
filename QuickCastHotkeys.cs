using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using Microsoft.Xna.Framework.Input;
using System;
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

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

            LogDebug("QuickCastHotkeys loaded. Patched InputState.GetMouseState.");
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

            Instance.LogDebug(
                $"After_GetMouseState -> Left={left}, Right={right}, " +
                $"ForceLeftHeld={ForceLeftHeld}, ForceRightHeld={ForceRightHeld}, " +
                $"ForceLeftPressFrame={ForceLeftPressFrame}, ForceLeftReleaseFrame={ForceLeftReleaseFrame}, " +
                $"ForceRightPressFrame={ForceRightPressFrame}, ForceRightReleaseFrame={ForceRightReleaseFrame}, " +
                $"Tool={Game1.player.CurrentTool?.Name ?? "null"}, UsingTool={Game1.player.UsingTool}, " +
                $"Menu={Game1.activeClickableMenu?.GetType().FullName ?? "null"}"
            );
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !Game1.player.IsLocalPlayer)
                return;

            AppliesSmartCast();
            CheckIfDoneUsingTool();

            if (ForceLeftHeld || ForceRightHeld || ForceLeftPressFrame || ForceLeftReleaseFrame || ForceRightPressFrame || ForceRightReleaseFrame)
            {
                LogDebug(
                    $"Tick {e.Ticks}: " +
                    $"ForceLeftHeld={ForceLeftHeld}, ForceRightHeld={ForceRightHeld}, " +
                    $"ForceLeftPressFrame={ForceLeftPressFrame}, ForceLeftReleaseFrame={ForceLeftReleaseFrame}, " +
                    $"ForceRightPressFrame={ForceRightPressFrame}, ForceRightReleaseFrame={ForceRightReleaseFrame}, " +
                    $"CurrentToolIndex={Game1.player.CurrentToolIndex}, Tool={Game1.player.CurrentTool?.Name ?? "null"}, " +
                    $"UsingTool={Game1.player.UsingTool}, CanMove={Game1.player.CanMove}, " +
                    $"Menu={Game1.activeClickableMenu?.GetType().FullName ?? "null"}"
                );
            }


            if (ForceLeftPressFrame || ForceLeftReleaseFrame || ForceRightPressFrame || ForceRightReleaseFrame)
            {
                LogDebug($"Clearing frame pulse flags on tick {e.Ticks}.");
                ForceLeftPressFrame = false;
                ForceLeftReleaseFrame = false;
                ForceRightPressFrame = false;
                ForceRightReleaseFrame = false;
            }
        }

        private void AppliesSmartCast()
        {

            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is not StardewValley.Menus.BobberBar)
            {
                LogDebug($"AppliesSmartCast blocked by menu: {Game1.activeClickableMenu.GetType().FullName}");
                return;
            }

            Farmer player = Game1.player;

            LogDebug(
                $"AppliesSmartCast: activeClickableMenu={(Game1.activeClickableMenu != null)}, " +
                $"currentMinigame={(Game1.currentMinigame != null)}, " +
                $"UsingTool={Game1.player.UsingTool}, isUsingTool={isUsingTool}, " +
                $"CurrentTool={Game1.player.CurrentTool?.Name ?? "null"}"
            );

            for (int i = 0; i < HotkeyCount; i++)
            {
                if (Config.Hotkeys[i] == null || Config.Hotkeys[i].Keybinds.Count() == 0)
                    continue;

                bool isHotkeyPressed = Config.Hotkeys[i].IsDown();

                LogDebug(
                    $"Hotkey {i + 1} state: isDown={isHotkeyPressed}, " +
                    $"IsHotkeyHeld={IsHotkeyHeld[i]}, NeedToSwitchBack={NeedToSwitchBack[i]}, " +
                    $"playerUsingTool={Game1.player.UsingTool}, modIsUsingTool={isUsingTool}, " +
                    $"currentMinigame={(Game1.currentMinigame != null)}, " +
                    $"menu={Game1.activeClickableMenu?.GetType().FullName ?? "null"}"
                );

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
                        LogDebug(
                            $"Hotkey {i + 1} pressed. allowWhileFishing={allowWhileFishing}. " +
                            $"Switched from slot {originalToolIndex + 1} to slot {player.CurrentToolIndex + 1}. " +
                            $"Tool={player.CurrentTool?.Name ?? "null"} Item={currentItem?.Name ?? "null"} " +
                            $"UsingTool={player.UsingTool} isUsingTool={isUsingTool}"
                        );

                        if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                        {
                            BeginForcedRightClick();
                        }
                    }
                    else
                    {
                        LogDebug(
                            $"Hotkey {i + 1} press ignored because isUsingTool={isUsingTool}, " +
                            $"allowWhileFishing={allowWhileFishing}, Tool={player.CurrentTool?.Name ?? "null"}, " +
                            $"Menu={Game1.activeClickableMenu?.GetType().FullName ?? "null"}"
                        );
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

                    LogDebug(
                        $"Hotkey {i + 1} released. Tool={player.CurrentTool?.Name ?? "null"} Item={currentItem?.Name ?? "null"}"
                    );

                    IsHotkeyHeld[i] = false;
                }
            }
        }

        private void BeginForcedLeftClick()
        {
            ForceLeftPressFrame = true;
            ForceLeftHeld = true;
            ForceLeftReleaseFrame = false;
            LogDebug("BeginForcedLeftClick()");
        }

        private void EndForcedLeftClick()
        {
            ForceLeftHeld = false;
            ForceLeftReleaseFrame = true;
            LogDebug("EndForcedLeftClick()");
        }

        private void BeginForcedRightClick()
        {
            ForceRightPressFrame = true;
            ForceRightHeld = true;
            ForceRightReleaseFrame = false;
            LogDebug("BeginForcedRightClick()");
        }

        private void EndForcedRightClick()
        {
            ForceRightHeld = false;
            ForceRightReleaseFrame = true;
            LogDebug("EndForcedRightClick()");
        }

        private void CheckIfDoneUsingTool()
        {
            if (Game1.activeClickableMenu is StardewValley.Menus.BobberBar)
            {
                LogDebug("CheckIfDoneUsingTool deferred because BobberBar is active.");
                return;
            }

            if (!Game1.player.UsingTool)
            {
                isUsingTool = false;

                for (int i = 0; i < HotkeyCount; i++)
                {
                    if (!IsHotkeyHeld[i] && NeedToSwitchBack[i])
                    {
                        LogDebug($"Switching back to original slot {originalToolIndex + 1} from slot {Game1.player.CurrentToolIndex + 1}");
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

            LogDebug("Returned to title. Cleared forced mouse state.");
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

        public void LogDebug(string message)
        {
            if (Config?.EnableDebugLogging == true)
                Monitor.Log(message, LogLevel.Debug);
        }
    }
}