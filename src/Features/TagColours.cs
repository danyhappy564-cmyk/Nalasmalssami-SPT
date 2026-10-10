// 색상표 확장 방식은 acidphantasm의 "More Tag Colours"(MIT)에서 가져왔다.
// 원본: https://github.com/acidphantasm/acidphantasm-moretagcolours  (Copyright (c) 2025 acidphantasm)
// 바뀐 점: spt-reflection 없이 이름으로 패치, 끄기/회색 포함/창 크기를 F12에서 바로 적용, 열려 있는 아이템 칸 즉시 다시 칠하기.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Nalasmalssami.Util;
using UnityEngine;

namespace Nalasmalssami.Features
{
    /// <summary>
    /// 태그 색을 게임 기본 7색 → 18색 + 회색 9단계(총 27)로 늘린다.
    /// 태그 색은 프로필에 "번호"로 저장되므로, 번호 순서를 원본 모드와 똑같이 맞춰 두었다
    /// (원본 모드 ↔ 나랏말싸미를 바꿔 써도 같은 태그가 같은 색).
    /// </summary>
    internal static class TagColours
    {
        private static readonly Color[] Greys =
        {
            new Color(0.1f, 0.1f, 0.1f), new Color(0.2f, 0.2f, 0.2f), new Color(0.3f, 0.3f, 0.3f),
            new Color(0.4f, 0.4f, 0.4f), new Color(0.5f, 0.5f, 0.5f), new Color(0.6f, 0.6f, 0.6f),
            new Color(0.7f, 0.7f, 0.7f), new Color(0.8f, 0.8f, 0.8f), new Color(0.9f, 0.9f, 0.9f),
        };

        private static readonly Vector2 BigWindowSize = new Vector2(450f, 260f);
        private static readonly Dictionary<int, Vector2> OriginalWindowSizes = new Dictionary<int, Vector2>();

        /// <summary>게임 기본 7색 배열(EditTagWindow.color_0). 색 선택 창에 이 배열이 들어오면 우리 것으로 바꾼다.</summary>
        internal static Color[] Vanilla;

        /// <summary>표시용 전체 색상표(항상 27개) — 회색을 숨겨도 이미 회색으로 단 태그는 제 색으로 보이게.</summary>
        private static Color[] _full;

        /// <summary>색 선택 창에 보여 줄 목록(회색 포함 여부에 따라 18 또는 27개).</summary>
        private static Color[] _picker;

        private static bool Active => Settings.ExtraColours.Value;

        public static string Enable()
        {
            Type window = Reflect.Type("EFT.UI.EditTagWindow");
            Type tagComponent = Reflect.Type("EFT.InventoryLogic.TagComponent");
            MethodInfo getColor = Reflect.Method(window, "GetColor", typeof(int));
            MethodInfo show = Reflect.Method(window, "Show", tagComponent);
            FieldInfo vanilla = window?.GetField("color_0", Reflect.Stat);

            string missing = Reflect.Missing(
                ("EditTagWindow.GetColor(int)", getColor),
                ("EditTagWindow.Show(TagComponent)", show),
                ("EditTagWindow.color_0", vanilla));
            if (missing.Length > 0)
            {
                return "멤버를 찾지 못함: " + missing;
            }

            Vanilla = vanilla.GetValue(null) as Color[];
            Rebuild();

            Plugin.Harmony.Patch(getColor, prefix: new HarmonyMethod(typeof(TagColours), nameof(GetColorPrefix)));
            Plugin.Harmony.Patch(show, postfix: new HarmonyMethod(typeof(TagColours), nameof(ShowPostfix)));
            // 색 목록 바꿔치기는 안전장치(ColourPanelGuard)의 TagColorsPanel.Show 패치 안에서 같이 처리한다.

            var entries = new List<BepInEx.Configuration.ConfigEntryBase> { Settings.ExtraColours, Settings.IncludeGreys };
            entries.AddRange(Settings.Colours);
            Settings.OnChanged(() =>
            {
                Rebuild();
                GridRefresh.Request(); // 인벤토리에 보이는 태그 색을 바로 다시 칠함
            }, entries.ToArray());

            Settings.BiggerWindow.SettingChanged += (_, __) =>
            {
                if (HangulInput.CurrentWindow != null)
                {
                    ApplyWindowSize(HangulInput.CurrentWindow);
                }
            };

            return null;
        }

        private static void Rebuild()
        {
            var full = new Color[Settings.Colours.Length + Greys.Length];
            for (int i = 0; i < Settings.Colours.Length; i++)
            {
                full[i] = Settings.Colours[i].Value;
            }

            Greys.CopyTo(full, Settings.Colours.Length);
            _full = full;

            if (Settings.IncludeGreys.Value)
            {
                _picker = full;
            }
            else
            {
                _picker = new Color[Settings.Colours.Length];
                Array.Copy(full, _picker, _picker.Length);
            }
        }

        /// <summary>상태 기록용</summary>
        internal static string DescribeState()
        {
            if (Compat.MoreTagColoursInstalled)
            {
                return "원본 MoreTagColours가 처리";
            }

            return $"사용={Active}, 표시용 색 {_full?.Length ?? 0}개, 선택 창 색 {_picker?.Length ?? 0}개, 게임 기본 {Vanilla?.Length ?? 0}개";
        }

        /// <summary>색 선택 창에 넘길 색 목록을 정한다(기능이 꺼져 있으면 그대로).</summary>
        internal static Color[] PickerFor(Color[] requested)
        {
            if (!Active || _picker == null || !ReferenceEquals(requested, Vanilla))
            {
                return requested;
            }

            return _picker;
        }

        // EditTagWindow.GetColor(번호 → 색): 아이템 칸 태그 막대 색이 여기서 정해진다.
        public static bool GetColorPrefix(ref Color __result, int colorIndex)
        {
            if (!Active || _full == null)
            {
                return true; // 게임 기본
            }

            if (colorIndex < 0 || colorIndex >= _full.Length)
            {
                colorIndex = 0;
            }

            __result = _full[colorIndex];
            return false;
        }

        public static void ShowPostfix(object __instance)
        {
            try
            {
                ApplyWindowSize(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[태그 색상 확장] 창 크기 조절 실패: " + e);
            }
        }

        private static void ApplyWindowSize(object window)
        {
            if (!(window is Component component) || Reflect.IsNull(component))
            {
                return;
            }

            RectTransform rect = component.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            int id = rect.GetInstanceID();
            if (!OriginalWindowSizes.ContainsKey(id))
            {
                OriginalWindowSizes[id] = rect.sizeDelta;
            }

            rect.sizeDelta = Active && Settings.BiggerWindow.Value ? BigWindowSize : OriginalWindowSizes[id];
            Dbg.Log("태그 색상 확장", $"태그 창 크기 {rect.sizeDelta} (원래 {OriginalWindowSizes[id]})");
        }
    }

    /// <summary>
    /// 색 선택 창(TagColorsPanel.Show) 안전장치 — 항상 켠다(원본 MoreTagColours와 같이 써도).
    /// 게임 코드는 태그의 색 번호로 칸 배열을 바로 꺼내서, 색이 27개였던 모드를 빼고 나면
    /// 20번 색 태그를 열 때 "IndexOutOfRange"로 태그 창이 깨진다. 번호가 범위를 벗어나면 0번으로 고친다.
    /// 다른 모드가 색 목록을 바꾼 "뒤에" 검사해야 하므로 가장 늦게(Priority.Last) 실행한다.
    /// </summary>
    internal static class ColourPanelGuard
    {
        public static string Enable()
        {
            Type panel = Reflect.Type("EFT.UI.TagColorsPanel");
            MethodInfo show = Reflect.MethodByArity(panel, "Show", 3);
            if (show == null)
            {
                return "TagColorsPanel.Show 를 찾지 못함";
            }

            var prefix = new HarmonyMethod(typeof(ColourPanelGuard), nameof(ShowPrefix)) { priority = Priority.Last };
            Plugin.Harmony.Patch(show, prefix: prefix);
            return null;
        }

        public static void ShowPrefix(ref Color[] colors, ref int selectedIndex)
        {
            try
            {
                if (!Compat.MoreTagColoursInstalled)
                {
                    colors = TagColours.PickerFor(colors);
                }

                if (colors != null && colors.Length > 0 && (selectedIndex < 0 || selectedIndex >= colors.Length))
                {
                    selectedIndex = 0;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[색 선택 창 안전장치] 오류: " + e);
            }
        }
    }
}
