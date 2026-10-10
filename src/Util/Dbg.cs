using System.Collections.Generic;

namespace Nalasmalssami.Util
{
    /// <summary>
    /// F12 "디버그 로그"가 켜졌을 때만 LogOutput.log에 [디버그] 줄을 남긴다.
    /// BepInEx 기본 설정은 Debug 등급을 파일에 안 쓰므로 Info 등급으로 쓴다.
    /// 아이템 칸처럼 수백 번 반복되는 곳은 Limit()로 종류별 개수를 제한한다(디버그를 다시 켜면 초기화).
    /// </summary>
    internal static class Dbg
    {
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        public static bool On => Settings.DebugLog != null && Settings.DebugLog.Value;

        public static void Log(string area, string message)
        {
            if (On)
            {
                Plugin.Log.LogInfo($"[디버그][{area}] {message}");
            }
        }

        /// <summary>같은 key로 max번까지만 true. 마지막 한 번은 "이후 생략"을 알린다.</summary>
        public static bool Limit(string key, int max)
        {
            if (!On)
            {
                return false;
            }

            Counts.TryGetValue(key, out int n);
            Counts[key] = n + 1;
            if (n == max)
            {
                Plugin.Log.LogInfo($"[디버그][{key}] {max}건 넘어서 이후 생략(디버그 로그를 껐다 켜면 다시 기록)");
            }

            return n < max;
        }

        public static void ResetLimits() => Counts.Clear();
    }
}
