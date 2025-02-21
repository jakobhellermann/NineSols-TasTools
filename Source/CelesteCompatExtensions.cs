using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using MonoMod.Utils;
using TAS.Input;

namespace TAS;

public static class CelesteCompatExtensions {
    public static bool Has(this ExecuteTiming states, ExecuteTiming flag) => (states & flag) == flag;
    
    public static void LogException(this Exception e, string message) {
        TAS.Log.Error($"{message}: {e}");
    }

    public static void Log(this string message, LogLevel level = LogLevel.Info) {
        TAS.Log.LogMessage(message, level);
    }
    public static void Log(this Exception message, LogLevel level = LogLevel.Info) {
        TAS.Log.LogMessage(message, level);
    }

    public static string ReplaceLineEndings(this string text, string replacementText) {
        return text.Replace("\n", replacementText);
    }
}

public class UnreachableException : Exception;

internal static class ListExtensions {
    public static T? GetValueOrDefault<T>(this IList<T> list, int index, T? defaultValue = default) =>
        index >= 0 && index < list.Count ? list[index] : defaultValue;
}

internal static class HashHelper {
    public static string ComputeHash(string text) =>
        SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text)).ToHexadecimalString();
}
