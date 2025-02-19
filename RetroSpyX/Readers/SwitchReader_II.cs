﻿using Avalonia.Controls;
using System;
using System.Text;
using Vortice.XInput;

namespace RetroSpy.Readers
{
    public static class SwitchReader_II
    {
        private const int PRO_PACKET_SIZE = 129;
        private const int POKKEN_PACKET_SIZE = 17;
        private const int GC_PACKET_SIZE = 75;

        private static readonly string?[] PRO_BUTTONS = {
            "y", "x", "b", "a", null, null, "r", "zr", "-", "+", "rs", "ls", "home", "capture", null, null, "down", "up", "right", "left", null, null, "l", "zl"
        };

        private static readonly uint[] PRO_BUTTONS_VJOY = {
            1, 4, 2, 3, 0, 0, 6, 8, 9, 10, 12, 11, 13, 14, 0, 0, 0, 0, 0, 0, 0, 0, 5, 7
        };

        private static readonly string?[] POKKEN_BUTTONS = {
            "y", "b", "a", "x", "l", "r", "zl", "zr", "-", "+", null, null, "home", "capture", null, null
        };

        private static readonly uint[] PRO_POKEN_VJOY = {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0, 0, 13, 14, 0, 0
        };

        private static readonly string?[] GC_BUTTONS = {
            "a", "b", "x", "y", "left", "right", "down", "up", "start", "z", "r", "l", null, null, null, null
        };

        private static float ReadStick(byte input)
        {
            return input < 127 ? (float)input / 128 : (float)(255 - input) / -128;
        }

        private static float ReadStickGC(byte input)
        {
            return (float)(input - 128) / 128;
        }

        private static float ReadTriggerGC(byte input, float maxVal = 256)
        {
            return (float)input / maxVal;
        }

        private static float ReadPokkenStick(byte input, bool invert)
        {
            return invert ? -1.0f * ((float)(input - 128) / 128) : (float)(input - 128) / 128;
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static ControllerStateEventArgs? ReadFromPacket(byte[]? packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Length < POKKEN_PACKET_SIZE)
            {
                return null;
            }

            if (packet.Length == PRO_PACKET_SIZE)
            {
                byte[] binaryPacket = StringToByteArray(Encoding.UTF8.GetString(packet, 0, packet.Length).Trim());

                if (binaryPacket[0] != 0x30)
                    return null;

                if (binaryPacket[55] == 1)
                {
                    for (int i = 0; i < 8; ++i)
                    {
                        if (i != 0 && i != 4 && binaryPacket[binaryPacket.Length - i - 1] != 0x00)
                            return null;
                    }
                }
                else
                {
                    for (int i = 0; i < 15; ++i)
                    {
                        if (binaryPacket[binaryPacket.Length - i - 1] != 0x00)
                            return null;
                    }
                }
                ControllerStateBuilder outState = new();

                for (int i = 0; i < 3; ++i)
                {
                    for (int j = 0; j < 8; ++j)
                    {
                        if (string.IsNullOrEmpty(PRO_BUTTONS[(i * 8) + j]))
                        {
                            continue;
                        }

                        outState.SetButton(PRO_BUTTONS[(i * 8) + j], (binaryPacket[i + 3] & (1 << j)) != 0x00);
                        if (PRO_BUTTONS_VJOY[(i * 8) + j] != 0)
                            vJoyInterface.SetButton(PRO_BUTTONS_VJOY[(i * 8) + j], (binaryPacket[i + 3] & (1 << j)) != 0x00);
                    }
                }

                bool up = (binaryPacket[5] & (1 << 1)) != 0x00;
                bool right = (binaryPacket[5] & (1 << 2)) != 0x00;
                bool down = (binaryPacket[5] & (1 << 0)) != 0x00;
                bool left = (binaryPacket[5] & (1 << 3)) != 0x00;

                if (up && !right && !down && !left)
                    vJoyInterface.SetPOV(0);
                else if (up && right && !down && !left)
                    vJoyInterface.SetPOV(1);
                else if (!up && right && !down && !left)
                    vJoyInterface.SetPOV(2);
                else if (!up && right && down && !left)
                    vJoyInterface.SetPOV(3);
                else if (!up && !right && down && !left)
                    vJoyInterface.SetPOV(4);
                else if (!up && !right && down && left)
                    vJoyInterface.SetPOV(5);
                else if (!up && !right && !down && left)
                    vJoyInterface.SetPOV(6);
                else if (up && !right && !down && left)
                    vJoyInterface.SetPOV(7);
                else
                    vJoyInterface.SetPOV(-1);

                outState.SetAnalog("lstick_x", ReadStick((byte)((((binaryPacket[7] & 0x0F) << 4) | ((binaryPacket[6] & 0xF0) >> 4)) + 127)), (((binaryPacket[7] & 0x0F) << 4) | ((binaryPacket[6] & 0xF0) >> 4)) + 127);
                vJoyInterface.SetAxis(vJoyAxis.X, ReadStick((byte)((((binaryPacket[7] & 0x0F) << 4) | ((binaryPacket[6] & 0xF0) >> 4)) + 127)));
                outState.SetAnalog("lstick_y", ReadStick((byte)(binaryPacket[8] + 127)), binaryPacket[8] + 127);
                vJoyInterface.SetAxis(vJoyAxis.Y, ReadStick((byte)(binaryPacket[8] + 127)));
                outState.SetAnalog("rstick_x", ReadStick((byte)((((binaryPacket[10] & 0x0F) << 4) | ((binaryPacket[9] & 0xF0) >> 4)) + 127)), (((binaryPacket[10] & 0x0F) << 4) | ((binaryPacket[9] & 0xF0) >> 4)) + 127);
                vJoyInterface.SetAxis(vJoyAxis.Z, ReadStick((byte)((((binaryPacket[10] & 0x0F) << 4) | ((binaryPacket[9] & 0xF0) >> 4)) + 127)));
                outState.SetAnalog("rstick_y", ReadStick((byte)(binaryPacket[11] + 127)), binaryPacket[11] + 127);
                vJoyInterface.SetAxis(vJoyAxis.ZR, ReadStick((byte)(binaryPacket[11] + 127)));
                if (binaryPacket[55] == 1)
                {
                    outState.SetAnalog("r2", binaryPacket[63] / 255.0f, binaryPacket[63]);
                    vJoyInterface.SetAxis(vJoyAxis.YR, binaryPacket[63] / 255.0f);
                    outState.SetAnalog("l2", binaryPacket[59] / 255.0f, binaryPacket[59]);
                    vJoyInterface.SetAxis(vJoyAxis.XR, binaryPacket[59] / 255.0f);
                }
                else
                {
                    outState.SetAnalog("r2", binaryPacket[7] != 0 ? 1.0f : 0.0f, binaryPacket[7] != 0 ? 255 : 0);
                    vJoyInterface.SetAxis(vJoyAxis.YR, binaryPacket[7] != 0 ? 1.0f : 0.0f);
                    outState.SetAnalog("l2", binaryPacket[23] != 0 ? 1.0f : 0.0f, binaryPacket[23] != 0 ? 255 : 0);
                    vJoyInterface.SetAxis(vJoyAxis.XR, binaryPacket[23] != 0 ? 1.0f : 0.0f);
                }
                return outState.Build();

            }
            else if (packet.Length == POKKEN_PACKET_SIZE)
            {
                byte[] binaryPacket = StringToByteArray(Encoding.UTF8.GetString(packet, 0, packet.Length).Trim());

                ControllerStateBuilder outState = new();

                for (int i = 0; i < 2; ++i)
                {
                    for (int j = 0; j < 8; ++j)
                    {
                        if (string.IsNullOrEmpty(POKKEN_BUTTONS[(i * 8) + j]))
                        {
                            continue;
                        }

                        outState.SetButton(POKKEN_BUTTONS[(i * 8) + j], (binaryPacket[i] & (1 << j)) != 0x00);
                        if (PRO_POKEN_VJOY[(i * 8) + j] != 0)
                            vJoyInterface.SetButton(PRO_POKEN_VJOY[(i * 8) + j], (binaryPacket[i] & (1 << j)) != 0x00);
                    }
                }

                outState.SetAnalog("lstick_x", ReadPokkenStick(binaryPacket[3], false), binaryPacket[3]);
                vJoyInterface.SetAxis(vJoyAxis.X, ReadPokkenStick(binaryPacket[3], false));
                outState.SetAnalog("lstick_y", ReadPokkenStick(binaryPacket[4], true), binaryPacket[4]);
                vJoyInterface.SetAxis(vJoyAxis.Y, ReadPokkenStick(binaryPacket[4], false));
                outState.SetAnalog("rstick_x", ReadPokkenStick(binaryPacket[5], false), binaryPacket[5]);
                vJoyInterface.SetAxis(vJoyAxis.Z, ReadPokkenStick(binaryPacket[5], false));
                outState.SetAnalog("rstick_y", ReadPokkenStick(binaryPacket[6], true), binaryPacket[6]);
                vJoyInterface.SetAxis(vJoyAxis.ZR, ReadPokkenStick(binaryPacket[6], false));

                vJoyInterface.SetPOV(binaryPacket[2]);

                switch (binaryPacket[2])
                {
                    case 0:
                        outState.SetButton("up", true);
                        outState.SetButton("down", false);
                        outState.SetButton("left", false);
                        outState.SetButton("right", false);
                        break;

                    case 1:
                        outState.SetButton("up", true);
                        outState.SetButton("right", true);
                        outState.SetButton("down", false);
                        outState.SetButton("left", false);
                        break;

                    case 2:
                        outState.SetButton("right", true);
                        outState.SetButton("down", false);
                        outState.SetButton("left", false);
                        outState.SetButton("up", false);
                        break;

                    case 3:
                        outState.SetButton("right", true);
                        outState.SetButton("down", true);
                        outState.SetButton("up", false);
                        outState.SetButton("left", false);
                        break;

                    case 4:
                        outState.SetButton("down", true);
                        outState.SetButton("up", false);
                        outState.SetButton("left", false);
                        outState.SetButton("right", false);
                        break;

                    case 5:
                        outState.SetButton("left", true);
                        outState.SetButton("down", true);
                        outState.SetButton("right", false);
                        outState.SetButton("up", false);
                        break;

                    case 6:
                        outState.SetButton("right", false);
                        outState.SetButton("down", false);
                        outState.SetButton("up", false);
                        outState.SetButton("left", true);
                        break;

                    case 7:
                        outState.SetButton("up", true);
                        outState.SetButton("left", true);
                        outState.SetButton("right", false);
                        outState.SetButton("down", false);
                        break;

                    default:
                        outState.SetButton("up", false);
                        outState.SetButton("left", false);
                        outState.SetButton("right", false);
                        outState.SetButton("down", false);
                        break;
                }

                return outState.Build();
            }
            else if (packet.Length == GC_PACKET_SIZE)
            {
                byte[] binaryPacket = StringToByteArray(Encoding.UTF8.GetString(packet, 0, packet.Length).Trim());

                ControllerStateBuilder outState = new();
                for (int i = 0; i < 2; ++i)
                {
                    for (int j = 0; j < 8; ++j)
                    {
                        if (string.IsNullOrEmpty(GC_BUTTONS[(i * 8) + j]))
                        {
                            continue;
                        }

                        outState.SetButton(GC_BUTTONS[(i * 8) + j], (binaryPacket[i+2] & (1 << j)) != 0x00);
                    }
                }

                outState.SetAnalog("lstick_x", ReadStickGC(binaryPacket[4]), binaryPacket[4]);
                outState.SetAnalog("lstick_y", ReadStickGC(binaryPacket[5]), binaryPacket[5]);
                outState.SetAnalog("cstick_x", ReadStickGC(binaryPacket[6]), binaryPacket[6]);
                outState.SetAnalog("cstick_y", ReadStickGC(binaryPacket[7]), binaryPacket[7]);

                outState.SetAnalog("trig_l", ReadStickGC(binaryPacket[8]), binaryPacket[8]);
                outState.SetAnalog("trig_r", ReadStickGC(binaryPacket[9]), binaryPacket[9]);

                return outState.Build();
            }

            return null;
        }
    }
}