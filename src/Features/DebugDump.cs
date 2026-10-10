using System;
using System.Linq;
using System.Text;
using Nalasmalssami.Util;
using UnityEngine;

namespace Nalasmalssami.Features
{
    /// <summary>F12 "지금 상태 기록 (1회)" — 제보용으로 현재 상태를 LogOutput.log에 한 덩어리로 적는다.</summary>
    internal static class DebugDump
    {
        public static void Write()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"===== 나랏말싸미 {Plugin.Version} 상태 기록 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
            sb.AppendLine("원본 모드: " + Compat.Summary());

            sb.AppendLine("[기능]");
            foreach (var pair in Plugin.FeatureStatus)
            {
                sb.AppendLine($"  {pair.Key}: {pair.Value}");
            }

            sb.AppendLine("[설정]");
            if (Settings.File != null)
            {
                foreach (var entry in Settings.File.Keys.OrderBy(k => k.Section).ThenBy(k => k.Key))
                {
                    sb.AppendLine($"  {entry.Section} / {entry.Key} = {Settings.File[entry].BoxedValue}");
                }
            }

            sb.AppendLine("[실행 상태]");
            sb.AppendLine($"  입력기: imeCompositionMode={Input.imeCompositionMode}, 조합 중 글자='{Input.compositionString}', " +
                          $"시스템 입력기 사용 중={Input.imeIsSelected}");
            Append(sb, "태그 창", HangulInput.DescribeState);
            Append(sb, "한글 글꼴", HangulFont.DescribeState);
            Append(sb, "프리셋", PresetBar.DescribeState);
            Append(sb, "색상 확장", TagColours.DescribeState);
            Append(sb, "아이템 칸", GridRefresh.DescribeState);
            sb.Append("===== 상태 기록 끝 =====");

            Plugin.Log.LogInfo(sb.ToString());
        }

        private static void Append(StringBuilder sb, string name, Func<string> describe)
        {
            try
            {
                sb.AppendLine($"  {name}: {describe()}");
            }
            catch (Exception e)
            {
                sb.AppendLine($"  {name}: (읽기 실패 — {e.Message})");
            }
        }
    }
}
