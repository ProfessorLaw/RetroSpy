﻿using System;

namespace RetroSpy.Readers
{
    public static class C64mini
    {
        private const int PACKET_SIZE = 58;
        private const int POLISHED_PACKET_SIZE = 28;

        private static readonly string?[] BUTTONS = {
            null, null, null, null, "1", "2", "tl", "tr", "a", "b", "c", "menu", null, null, null, null
        };

        public static ControllerStateEventArgs? ReadFromPacket(byte[]? packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Length < PACKET_SIZE)
            {
                return null;
            }

            byte[] polishedPacket = new byte[POLISHED_PACKET_SIZE];

            for (int i = 40; i < 56; ++i)
            {
                polishedPacket[i - 40] = (byte)((packet[i] == 0x31) ? 1 : 0);
            }

            for (int i = 0; i < 2; ++i)
            {
                polishedPacket[16 + i] = 0;
                for (byte j = 0; j < 8; ++j)
                {
                    polishedPacket[16 + i] |= (byte)((packet[0 + (i * 8) + j] == 0x30 ? 0 : 1) << j);
                }
            }

            ControllerStateBuilder outState = new();

            for (int i = 0; i < BUTTONS.Length; ++i)
            {
                if (string.IsNullOrEmpty(BUTTONS[i]))
                {
                    continue;
                }

                outState.SetButton(BUTTONS[i], polishedPacket[i] != 0x00);
            }

            outState.SetButton("left", polishedPacket[16] < 0x7f);
            outState.SetButton("right", polishedPacket[16] > 0x7f);
            outState.SetButton("up", polishedPacket[17] < 0x7f);
            outState.SetButton("down", polishedPacket[17] > 0x7f);

            float x = 0;
            float y = 0;

            if (polishedPacket[16] > 0x7f)
            {
                x = 1;
            }
            else if (polishedPacket[16] < 0x7f)
            {
                x = -1;
            }

            if (polishedPacket[17] > 0x7f)
            {
                y = -1;
            }
            else if (polishedPacket[17] < 0x7f)
            {
                y = 1;
            }

            if (y != 0 || x != 0)
            {
                // point on the unit circle at the same angle
                double radian = Math.Atan2(y, x);
                float x1 = (float)Math.Cos(radian);
                float y1 = (float)Math.Sin(radian);

                // Don't let magnitude exceed the unit circle
                if (Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2)) > 1.0)
                {
                    x = x1;
                    y = y1;
                }
            }

            outState.SetAnalog("x", x, polishedPacket[16]);
            outState.SetAnalog("y", y, polishedPacket[17]);

            return outState.Build();
        }
    }
}