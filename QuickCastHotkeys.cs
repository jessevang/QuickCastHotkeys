using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;


namespace QuickCastHotkeys
{
    public class Config
    {
        public string Notes1 = "The hotkeys don't prevent keyboard hotkeys function from executing in game so best to use a hotkey that is not currently used";
        public Keys Slot1HotkeyKeyboard { get; set; } = Keys.Space;
        public Keys Slot2HotkeyKeyboard { get; set; } = Keys.V;
        public string HereIsAListOfSomeOptionsForControllers = "A,B,X,Y,Back,Start,DPadUp,DpadDown,DPadRight,LeftShoulder,RightShoulder,LeftTrigger,RightTrigger,LeftStick,RightStick,BigButton";
        public string Notes3 = "The hotkeys don't prevent keyboard hotkeys function from executing in game so best to use a hotkey that is not currently used";
        public Buttons Slot1HotkeyController { get; set; } = Buttons.LeftStick;
        public Buttons Slot2HotkeyController { get; set; } = Buttons.DPadDown;


    }

    internal sealed class ModEntry : Mod
    {

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private bool IsHotkey0Held = false;
        private bool IsHotkey1Held = false;
        private bool NeedToSwitchBackHotkey0 = false;
        private bool NeedToSwitchBackHotkey1 = false;
        private bool isUsingTool = false;
        private int originalToolIndex = 2;
        public Config Config { get; private set; }=null;
        public static ModEntry Instance { get; private set; }



        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<Config>() ?? new Config();
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;


        }



        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {

            // Only run this logic for the local player
            if (!Context.IsWorldReady || !Game1.player.IsLocalPlayer)
                return;

            AppliesSmartCast();
            checkifdoneusingtool();

        }


        private void AppliesSmartCast()
        {


            GamePadState gamePadState = GamePad.GetState(PlayerIndex.One);
            KeyboardState keyBoardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();

            bool isHotkey0Pressed = gamePadState.IsButtonDown(Config.Slot1HotkeyController) || keyBoardState.IsKeyDown(Config.Slot1HotkeyKeyboard);
            bool isHotkey1Pressed = gamePadState.IsButtonDown(Config.Slot2HotkeyController) || keyBoardState.IsKeyDown(Config.Slot2HotkeyKeyboard);
            Farmer player = Game1.player;


            //makes it so only 1 hotkey is being executed at a time
            if (NeedToSwitchBackHotkey0)
            {
                isHotkey1Pressed= false;
            }
            else if (NeedToSwitchBackHotkey1)
            {
                isHotkey0Pressed= false;
            }

    
            if (isHotkey0Pressed && !IsHotkey0Held)
            {

                if (!isUsingTool) 
                {




                    if (!NeedToSwitchBackHotkey0) {
                        originalToolIndex = player.CurrentToolIndex;
                        player.CurrentToolIndex = 0;
                    }


                    // Press Left Mouse Button
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    NeedToSwitchBackHotkey0 = true;



                    // Assuming player.Items is a list of the player's items (tools, etc.)
                    Item currentItem = Game1.player.Items[Game1.player.CurrentToolIndex];
                    if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                    {
                        // Simulate a right-click (mouse down and then mouse up)
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0); // Right mouse button down


                    }






                }

                IsHotkey0Held = true;




            }
            

            if (!isHotkey0Pressed && IsHotkey0Held)
            {
                // Release Left Mouse Button if hotkey no longer pressed

                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                Item currentItem = Game1.player.Items[Game1.player.CurrentToolIndex];
                if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                {
                    // Simulate a right-click (mouse down and then mouse up)
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0); // Right mouse button down


                }

                IsHotkey0Held = false;

            }


            if (isHotkey1Pressed && !IsHotkey1Held)
            {

                if (!isUsingTool)
                {




                    if (!NeedToSwitchBackHotkey1)
                    {
                        originalToolIndex = player.CurrentToolIndex;
                        player.CurrentToolIndex = 1;
                    }


                    // Press Left Mouse Button
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    NeedToSwitchBackHotkey1 = true;

                    // Assuming player.Items is a list of the player's items (tools, etc.)
                    Item currentItem = Game1.player.Items[Game1.player.CurrentToolIndex];
                    if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                    {
                        // Simulate a right-click (mouse down and then mouse up)
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0); // Right mouse button down


                    }



                }

                IsHotkey1Held = true;




            }

            if (!isHotkey1Pressed && IsHotkey1Held)
            {
                // Release Left Mouse Button if hotkey no longer pressed

                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                Item currentItem = Game1.player.Items[Game1.player.CurrentToolIndex];
                if (currentItem is StardewValley.Object obj && obj.Edibility >= 0)
                {
                    // Simulate a right-click (mouse down and then mouse up)
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0); // Right mouse button down


                }
                IsHotkey1Held = false;

            }




        }

        private void checkifdoneusingtool()
        {
            if (!Game1.player.UsingTool && !IsHotkey0Held)
            {
                isUsingTool = false;
                if (NeedToSwitchBackHotkey0)
                {
                    Game1.player.CurrentToolIndex = originalToolIndex;
                    NeedToSwitchBackHotkey0 = false;
                }
            }
            if (!Game1.player.UsingTool && !IsHotkey1Held)
            {
                isUsingTool = false;
                if (NeedToSwitchBackHotkey1)
                {
                    Game1.player.CurrentToolIndex = originalToolIndex;
                    NeedToSwitchBackHotkey1 = false;
                }
            }
        }





    }
}
