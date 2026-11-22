using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>KSP.log lines tagged [FFW], so problems can be found in a player's log.</summary>
    internal static class Log
    {
        const string Tag = "[FFW] ";
        public static void Info(string msg) => Debug.Log(Tag + msg);
        public static void Warn(string msg) => Debug.LogWarning(Tag + msg);
        public static void Error(string msg) => Debug.LogError(Tag + msg);
    }
}
