﻿using System;

namespace RetroSpy.Readers
{
    public static class GameCube
    {
        private const int PACKET_SIZE = 64;
        private const int NICOHOOD_PACKET_SIZE = 8;

        private static readonly string?[] BUTTONS = {
            null, null, null, "start", "y", "x", "b", "a", null, "l", "r", "z", "up", "down", "right", "left"
        };

        // Button order for the Nicohood Nintendo API
        // https://github.com/NicoHood/Nintendo
        // Each byte is reverse from the buttons above
        static readonly string?[] NICOHOOD_BUTTONS = {
            "a", "b", "x", "y", "start", null, null, null, "left", "right", "down", "up", "z", "r", "l", null
        };

        private static readonly string?[] KEYS =
        {
            null, null, null, null, null, null, "Home", "End",
            "PageUp", "PageDown", null, "ScrollLock", null, null, null, null,
            "K_A", "K_B", "C", "D", "E", "F", "G", "H",
            "I", "J", "K", "K_L", "M", "N", "O", "P",

            "Q", "K_R", "S", "T", "U", "V", "W", "K_X",
            "K_Y", "K_Z", "D1", "D2", "D3", "D4", "D5", "D6",
            "D7", "D8", "D9", "D0", "Minus", "Equals", "Yen", "LeftBracket",
            "RightBracket", "Semicolon", "Apostrophe", "LeftOfReturn", "Comma", "Period", "Slash", "JpSlash",

            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8",
            "F9", "F10", "F11", "F12", "Escape", "Insert", "Delete", "Grave",
            "Back", "Tab", null, "Capital", "LeftShift", "RightShift", "LeftControl", "LeftAlt",
            "LeftWindowsKey", "Space", "RightWindowsKey", "Applications", "K_left", "K_down", "K_up", "K_right",

            null, "Return", null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
        };

        private static readonly string?[] FUNCTION_KEYS =
        {
            null, null, null, null, null, null, "Function", "Function",
            "Function", "Function", null, "Function", null, null, null, null,
            null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,

            null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,

            null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,

            null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,

        };

        private static float ReadStick(byte input)
        {
            return (float)(input - 128) / 128;
        }

        private static float ReadTrigger(byte input, float maxVal = 256)
        {
            return (float)input / maxVal;
        }

        private static readonly byte[] keyboardData = new byte[3];

        public static ControllerStateEventArgs? ReadFromSecondPacket(byte[]? packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Length == 3)
            {
                for (int i = 0; i < 3; ++i)
                {
                    keyboardData[i] = packet[i];
                }
            }

            return null;
        }

        public static ControllerStateEventArgs? ReadFromPacket(byte[]? packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Length == 3)
            {
                ControllerStateBuilder state1 = new();

                for (int i = 0; i < KEYS.Length; ++i)
                {
                    if (KEYS[i] != null)
                    {
                        state1.SetButton(KEYS[i], false);
                    }
                }

                for (int i = 0; i < packet.Length; ++i)
                {
                    if (KEYS[packet[i]] != null)
                    {
                        state1.SetButton(KEYS[packet[i]], true);
                    }
                }

                state1.SetButton("Function", false);
                for (int i = 0; i < packet.Length; ++i)
                {
                    if (FUNCTION_KEYS[packet[i]] != null)
                    {
                        state1.SetButton(FUNCTION_KEYS[packet[i]], true);
                    }
                }

                return state1.Build();
            }

            if (packet.Length == NICOHOOD_PACKET_SIZE) // Packets are written as bytes when writing from the NicoHood API, so we're looking for a packet size of 8 (interpreted as bytes)
            {

                ControllerStateBuilder stateNico = new();

                for (int i = 0; i < 16; i++) // Handles the two button bytes
                {
                    if (string.IsNullOrEmpty(NICOHOOD_BUTTONS[i])) continue;
                    int bitPacket = (packet[i / 8] >> (i % 8)) & 0x1;
                    stateNico.SetButton(NICOHOOD_BUTTONS[i], bitPacket != 0x00);
                }

                stateNico.SetAnalog("lstick_x", ReadStick(packet[2]), packet[2]);
                stateNico.SetAnalog("lstick_y", ReadStick(packet[3]), packet[3]);
                stateNico.SetAnalog("cstick_x", ReadStick(packet[4]), packet[4]);
                stateNico.SetAnalog("cstick_y", ReadStick(packet[5]), packet[5]);
                stateNico.SetAnalog("trig_l", ReadTrigger(packet[6]), packet[6]);
                stateNico.SetAnalog("trig_r", ReadTrigger(packet[7]), packet[7]);

                return stateNico.Build();
            }

            if (packet.Length != PACKET_SIZE && packet.Length != PACKET_SIZE - 8)
            {
                return null;
            }

            ControllerStateBuilder state = new();

            for (int i = 0; i < BUTTONS.Length; ++i)
            {
                if (string.IsNullOrEmpty(BUTTONS[i]))
                {
                    continue;
                }

                state.SetButton(BUTTONS[i], packet[i] != 0x00);
            }

            for (int i = 0; i < KEYS.Length; ++i)
            {
                if (KEYS[i] != null)
                {
                    state.SetButton(KEYS[i], false);
                }
            }

            for (int i = 0; i < keyboardData.Length; ++i)
            {
                if (KEYS[keyboardData[i]] != null)
                {
                    state.SetButton(KEYS[keyboardData[i]], true);
                }
            }

            state.SetButton("Function", false);
            for (int i = 0; i < keyboardData.Length; ++i)
            {
                if (FUNCTION_KEYS[keyboardData[i]] != null)
                {
                    state.SetButton(FUNCTION_KEYS[keyboardData[i]], true);
                }
            }

            state.SetAnalog("lstick_x", ReadStick(SignalTool.ReadByte(packet, BUTTONS.Length)), SignalTool.ReadByte(packet, BUTTONS.Length));
            state.SetAnalog("lstick_y", ReadStick(SignalTool.ReadByte(packet, BUTTONS.Length + 8)), SignalTool.ReadByte(packet, BUTTONS.Length + 8));
            state.SetAnalog("cstick_x", ReadStick(SignalTool.ReadByte(packet, BUTTONS.Length + 16)), SignalTool.ReadByte(packet, BUTTONS.Length + 16));
            state.SetAnalog("cstick_y", ReadStick(SignalTool.ReadByte(packet, BUTTONS.Length + 24)), SignalTool.ReadByte(packet, BUTTONS.Length + 24));
            if (packet.Length == PACKET_SIZE)
            {
                state.SetAnalog("trig_l", ReadTrigger(SignalTool.ReadByte(packet, BUTTONS.Length + 32)), SignalTool.ReadByte(packet, BUTTONS.Length + 32));
                state.SetAnalog("trig_r", ReadTrigger(SignalTool.ReadByte(packet, BUTTONS.Length + 40)), SignalTool.ReadByte(packet, BUTTONS.Length + 40));
            }
            else
            {
                state.SetAnalog("trig_l", ReadTrigger(SignalTool.ReadByte(packet, BUTTONS.Length + 32, 4), 15), SignalTool.ReadByte(packet, BUTTONS.Length + 32, 4));
                state.SetAnalog("trig_r", ReadTrigger(SignalTool.ReadByte(packet, BUTTONS.Length + 36, 4), 15), SignalTool.ReadByte(packet, BUTTONS.Length + 36, 4));
            }
            return state.Build();
        }
    }
}
