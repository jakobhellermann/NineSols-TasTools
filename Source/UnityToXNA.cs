using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using UnityEngine;

namespace TAS {
    public static class UnityToXNA {
        private static readonly Dictionary<KeyCode, Keys> UnityToXnaMap = new() {
            { KeyCode.None, Keys.None },
            { KeyCode.Backspace, Keys.Back },
            { KeyCode.Tab, Keys.Tab },
            { KeyCode.Return, Keys.Enter },
            { KeyCode.Pause, Keys.Pause },
            { KeyCode.Escape, Keys.Escape },
            { KeyCode.Space, Keys.Space },
            { KeyCode.DoubleQuote, Keys.OemQuotes },
            { KeyCode.Quote, Keys.OemQuotes },
            { KeyCode.Asterisk, Keys.Multiply },
            { KeyCode.Plus, Keys.Add },
            { KeyCode.Comma, Keys.OemComma },
            { KeyCode.Minus, Keys.OemMinus },
            { KeyCode.Period, Keys.OemPeriod },
            { KeyCode.Slash, Keys.OemQuestion },
            { KeyCode.Alpha0, Keys.D0 },
            { KeyCode.Alpha1, Keys.D1 },
            { KeyCode.Alpha2, Keys.D2 },
            { KeyCode.Alpha3, Keys.D3 },
            { KeyCode.Alpha4, Keys.D4 },
            { KeyCode.Alpha5, Keys.D5 },
            { KeyCode.Alpha6, Keys.D6 },
            { KeyCode.Alpha7, Keys.D7 },
            { KeyCode.Alpha8, Keys.D8 },
            { KeyCode.Alpha9, Keys.D9 },
            { KeyCode.Semicolon, Keys.OemSemicolon },
            { KeyCode.Less, Keys.OemComma },
            { KeyCode.Equals, Keys.OemPlus },
            { KeyCode.Greater, Keys.OemPeriod },
            { KeyCode.Question, Keys.OemQuestion },
            { KeyCode.LeftBracket, Keys.OemOpenBrackets },
            { KeyCode.RightBracket, Keys.OemCloseBrackets },
            { KeyCode.BackQuote, Keys.OemTilde },
            { KeyCode.A, Keys.A },
            { KeyCode.B, Keys.B },
            { KeyCode.C, Keys.C },
            { KeyCode.D, Keys.D },
            { KeyCode.E, Keys.E },
            { KeyCode.F, Keys.F },
            { KeyCode.G, Keys.G },
            { KeyCode.H, Keys.H },
            { KeyCode.I, Keys.I },
            { KeyCode.J, Keys.J },
            { KeyCode.K, Keys.K },
            { KeyCode.L, Keys.L },
            { KeyCode.M, Keys.M },
            { KeyCode.N, Keys.N },
            { KeyCode.O, Keys.O },
            { KeyCode.P, Keys.P },
            { KeyCode.Q, Keys.Q },
            { KeyCode.R, Keys.R },
            { KeyCode.S, Keys.S },
            { KeyCode.T, Keys.T },
            { KeyCode.U, Keys.U },
            { KeyCode.V, Keys.V },
            { KeyCode.W, Keys.W },
            { KeyCode.X, Keys.X },
            { KeyCode.Y, Keys.Y },
            { KeyCode.Z, Keys.Z },
            { KeyCode.Underscore, Keys.OemMinus }, // Assuming shift + minus
            { KeyCode.Tilde, Keys.Oem8 },
            { KeyCode.Delete, Keys.Delete },
            { KeyCode.Keypad0, Keys.NumPad0 },
            { KeyCode.Keypad1, Keys.NumPad1 },
            { KeyCode.Keypad2, Keys.NumPad2 },
            { KeyCode.Keypad3, Keys.NumPad3 },
            { KeyCode.Keypad4, Keys.NumPad4 },
            { KeyCode.Keypad5, Keys.NumPad5 },
            { KeyCode.Keypad6, Keys.NumPad6 },
            { KeyCode.Keypad7, Keys.NumPad7 },
            { KeyCode.Keypad8, Keys.NumPad8 },
            { KeyCode.Keypad9, Keys.NumPad9 },
            { KeyCode.KeypadPeriod, Keys.Decimal },
            { KeyCode.KeypadDivide, Keys.Divide },
            { KeyCode.KeypadMultiply, Keys.Multiply },
            { KeyCode.KeypadMinus, Keys.Subtract },
            { KeyCode.KeypadPlus, Keys.Add },
            { KeyCode.KeypadEnter, Keys.Enter },
            { KeyCode.KeypadEquals, Keys.OemPlus },
            { KeyCode.UpArrow, Keys.Up },
            { KeyCode.DownArrow, Keys.Down },
            { KeyCode.RightArrow, Keys.Right },
            { KeyCode.LeftArrow, Keys.Left },
            { KeyCode.Insert, Keys.Insert },
            { KeyCode.Home, Keys.Home },
            { KeyCode.End, Keys.End },
            { KeyCode.PageUp, Keys.PageUp },
            { KeyCode.PageDown, Keys.PageDown },
            { KeyCode.F1, Keys.F1 },
            { KeyCode.F2, Keys.F2 },
            { KeyCode.F3, Keys.F3 },
            { KeyCode.F4, Keys.F4 },
            { KeyCode.F5, Keys.F5 },
            { KeyCode.F6, Keys.F6 },
            { KeyCode.F7, Keys.F7 },
            { KeyCode.F8, Keys.F8 },
            { KeyCode.F9, Keys.F9 },
            { KeyCode.F10, Keys.F10 },
            { KeyCode.F11, Keys.F11 },
            { KeyCode.F12, Keys.F12 },
            { KeyCode.F13, Keys.F13 },
            { KeyCode.F14, Keys.F14 },
            { KeyCode.F15, Keys.F15 },
            { KeyCode.Numlock, Keys.NumLock },
            { KeyCode.CapsLock, Keys.CapsLock },
            { KeyCode.ScrollLock, Keys.Scroll },
            { KeyCode.RightShift, Keys.RightShift },
            { KeyCode.LeftShift, Keys.LeftShift },
            { KeyCode.RightControl, Keys.RightControl },
            { KeyCode.LeftControl, Keys.LeftControl },
            { KeyCode.RightAlt, Keys.RightAlt },
            { KeyCode.LeftAlt, Keys.LeftAlt },
            { KeyCode.RightApple, Keys.RightWindows },
            { KeyCode.RightCommand, Keys.RightWindows },
            { KeyCode.RightMeta, Keys.RightWindows },
            { KeyCode.LeftApple, Keys.LeftWindows },
            { KeyCode.LeftCommand, Keys.LeftWindows },
            { KeyCode.LeftMeta, Keys.LeftWindows },
            { KeyCode.LeftWindows, Keys.LeftWindows },
            { KeyCode.RightWindows, Keys.RightWindows },
            { KeyCode.Pipe, Keys.OemPipe },
            { KeyCode.Help, Keys.Help },
            { KeyCode.Print, Keys.PrintScreen },
            { KeyCode.SysReq, Keys.PrintScreen },
            { KeyCode.Break, Keys.Pause },
            { KeyCode.Menu, Keys.Apps },

            /*{ KeyCode.Clear, Keys.Clear },
            { KeyCode.AltGr, Keys.None }, // No direct match
            { KeyCode.Hash, Keys.None },
            { KeyCode.Exclaim, Keys.Oem8 }, // No direct match in XNA, map to a special key
            { KeyCode.Dollar, Keys.D5 }, // Assuming shift + 4
            { KeyCode.Percent, Keys.D5 }, // Assuming shift + 5
            { KeyCode.Ampersand, Keys.D7 }, // Assuming shift + 7
            { KeyCode.LeftParen, Keys.D9 }, // Assuming shift + 9
            { KeyCode.RightParen, Keys.D0 }, // Assuming shift + 0
            { KeyCode.Colon, Keys.Oem1 },
            { KeyCode.At, Keys.Oem7 },
            { KeyCode.Backslash, Keys.Oem5 },
            { KeyCode.Caret, Keys.Oem6 },
            { KeyCode.LeftCurlyBracket, Keys.Oem4 },
            { KeyCode.RightCurlyBracket, Keys.Oem6 },*/
        };

        public static Keys MapKeyCodeToXna(KeyCode keyCode) => UnityToXnaMap.GetValueOrDefault(keyCode, Keys.None);
    }
}

namespace Microsoft.Xna.Framework.Input {
    public enum Keys {
        None = 0,
        Back = 8,
        Tab = 9,
        Enter = 13, // 0x0000000D
        Pause = 19, // 0x00000013
        CapsLock = 20, // 0x00000014
        Kana = 21, // 0x00000015
        Kanji = 25, // 0x00000019
        Escape = 27, // 0x0000001B
        ImeConvert = 28, // 0x0000001C
        ImeNoConvert = 29, // 0x0000001D
        Space = 32, // 0x00000020
        PageUp = 33, // 0x00000021
        PageDown = 34, // 0x00000022
        End = 35, // 0x00000023
        Home = 36, // 0x00000024
        Left = 37, // 0x00000025
        Up = 38, // 0x00000026
        Right = 39, // 0x00000027
        Down = 40, // 0x00000028
        Select = 41, // 0x00000029
        Print = 42, // 0x0000002A
        Execute = 43, // 0x0000002B
        PrintScreen = 44, // 0x0000002C
        Insert = 45, // 0x0000002D
        Delete = 46, // 0x0000002E
        Help = 47, // 0x0000002F
        D0 = 48, // 0x00000030
        D1 = 49, // 0x00000031
        D2 = 50, // 0x00000032
        D3 = 51, // 0x00000033
        D4 = 52, // 0x00000034
        D5 = 53, // 0x00000035
        D6 = 54, // 0x00000036
        D7 = 55, // 0x00000037
        D8 = 56, // 0x00000038
        D9 = 57, // 0x00000039
        A = 65, // 0x00000041
        B = 66, // 0x00000042
        C = 67, // 0x00000043
        D = 68, // 0x00000044
        E = 69, // 0x00000045
        F = 70, // 0x00000046
        G = 71, // 0x00000047
        H = 72, // 0x00000048
        I = 73, // 0x00000049
        J = 74, // 0x0000004A
        K = 75, // 0x0000004B
        L = 76, // 0x0000004C
        M = 77, // 0x0000004D
        N = 78, // 0x0000004E
        O = 79, // 0x0000004F
        P = 80, // 0x00000050
        Q = 81, // 0x00000051
        R = 82, // 0x00000052
        S = 83, // 0x00000053
        T = 84, // 0x00000054
        U = 85, // 0x00000055
        V = 86, // 0x00000056
        W = 87, // 0x00000057
        X = 88, // 0x00000058
        Y = 89, // 0x00000059
        Z = 90, // 0x0000005A
        LeftWindows = 91, // 0x0000005B
        RightWindows = 92, // 0x0000005C
        Apps = 93, // 0x0000005D
        Sleep = 95, // 0x0000005F
        NumPad0 = 96, // 0x00000060
        NumPad1 = 97, // 0x00000061
        NumPad2 = 98, // 0x00000062
        NumPad3 = 99, // 0x00000063
        NumPad4 = 100, // 0x00000064
        NumPad5 = 101, // 0x00000065
        NumPad6 = 102, // 0x00000066
        NumPad7 = 103, // 0x00000067
        NumPad8 = 104, // 0x00000068
        NumPad9 = 105, // 0x00000069
        Multiply = 106, // 0x0000006A
        Add = 107, // 0x0000006B
        Separator = 108, // 0x0000006C
        Subtract = 109, // 0x0000006D
        Decimal = 110, // 0x0000006E
        Divide = 111, // 0x0000006F
        F1 = 112, // 0x00000070
        F2 = 113, // 0x00000071
        F3 = 114, // 0x00000072
        F4 = 115, // 0x00000073
        F5 = 116, // 0x00000074
        F6 = 117, // 0x00000075
        F7 = 118, // 0x00000076
        F8 = 119, // 0x00000077
        F9 = 120, // 0x00000078
        F10 = 121, // 0x00000079
        F11 = 122, // 0x0000007A
        F12 = 123, // 0x0000007B
        F13 = 124, // 0x0000007C
        F14 = 125, // 0x0000007D
        F15 = 126, // 0x0000007E
        F16 = 127, // 0x0000007F
        F17 = 128, // 0x00000080
        F18 = 129, // 0x00000081
        F19 = 130, // 0x00000082
        F20 = 131, // 0x00000083
        F21 = 132, // 0x00000084
        F22 = 133, // 0x00000085
        F23 = 134, // 0x00000086
        F24 = 135, // 0x00000087
        NumLock = 144, // 0x00000090
        Scroll = 145, // 0x00000091
        LeftShift = 160, // 0x000000A0
        RightShift = 161, // 0x000000A1
        LeftControl = 162, // 0x000000A2
        RightControl = 163, // 0x000000A3
        LeftAlt = 164, // 0x000000A4
        RightAlt = 165, // 0x000000A5
        BrowserBack = 166, // 0x000000A6
        BrowserForward = 167, // 0x000000A7
        BrowserRefresh = 168, // 0x000000A8
        BrowserStop = 169, // 0x000000A9
        BrowserSearch = 170, // 0x000000AA
        BrowserFavorites = 171, // 0x000000AB
        BrowserHome = 172, // 0x000000AC
        VolumeMute = 173, // 0x000000AD
        VolumeDown = 174, // 0x000000AE
        VolumeUp = 175, // 0x000000AF
        MediaNextTrack = 176, // 0x000000B0
        MediaPreviousTrack = 177, // 0x000000B1
        MediaStop = 178, // 0x000000B2
        MediaPlayPause = 179, // 0x000000B3
        LaunchMail = 180, // 0x000000B4
        SelectMedia = 181, // 0x000000B5
        LaunchApplication1 = 182, // 0x000000B6
        LaunchApplication2 = 183, // 0x000000B7
        OemSemicolon = 186, // 0x000000BA
        OemPlus = 187, // 0x000000BB
        OemComma = 188, // 0x000000BC
        OemMinus = 189, // 0x000000BD
        OemPeriod = 190, // 0x000000BE
        OemQuestion = 191, // 0x000000BF
        OemTilde = 192, // 0x000000C0
        ChatPadGreen = 202, // 0x000000CA
        ChatPadOrange = 203, // 0x000000CB
        OemOpenBrackets = 219, // 0x000000DB
        OemPipe = 220, // 0x000000DC
        OemCloseBrackets = 221, // 0x000000DD
        OemQuotes = 222, // 0x000000DE
        Oem8 = 223, // 0x000000DF
        OemBackslash = 226, // 0x000000E2
        ProcessKey = 229, // 0x000000E5
        OemCopy = 242, // 0x000000F2
        OemAuto = 243, // 0x000000F3
        OemEnlW = 244, // 0x000000F4
        Attn = 246, // 0x000000F6
        Crsel = 247, // 0x000000F7
        Exsel = 248, // 0x000000F8
        EraseEof = 249, // 0x000000F9
        Play = 250, // 0x000000FA
        Zoom = 251, // 0x000000FB
        Pa1 = 253, // 0x000000FD
        OemClear = 254, // 0x000000FE
    }
}