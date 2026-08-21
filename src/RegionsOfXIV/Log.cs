using System;

namespace RegionsOfXIV;

internal static class Log
{
    public static void Debug(string message) => Plugin.Log?.Debug(message);

    public static void Information(string message) => Plugin.Log?.Information(message);

    public static void Warning(string message) => Plugin.Log?.Warning(message);

    public static void Warning(Exception error, string message) => Plugin.Log?.Warning(error, message);

    public static void Error(string message) => Plugin.Log?.Error(message);

    public static void Error(Exception error, string message) => Plugin.Log?.Error(error, message);

    public static void Debug(Exception error, string message) => Plugin.Log?.Debug(error, message);
}
