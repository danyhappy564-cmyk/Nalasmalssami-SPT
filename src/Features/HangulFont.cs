using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Nalasmalssami.Util;
using UnityEngine;

namespace Nalasmalssami.Features
{
    /// <summary>
    /// 한글이 네모(□)로 보이는 것을 막는다. TextMeshPro(TMP)는 글꼴에 없는 글자를
    /// "예비 글꼴(fallback)" 목록에서 찾으므로, 한글이 들어 있는 글꼴을 그 목록에 붙인다.
    ///
    /// 찾는 순서: ① 이미 그 글꼴로 한글을 그릴 수 있으면 아무것도 안 함
    ///            ② 게임이 불러 둔 TMP 글꼴 중 한글이 있는 것
    ///            ③ 윈도우 글꼴(맑은 고딕 등)로 동적 TMP 글꼴을 만듦
    /// 붙이는 곳: TMP_Settings.fallbackFontAssets(전역 예비 목록). 끄면 붙였던 것만 뺀다.
    /// ※ ②·③은 인게임 미검증 — 결과는 LogOutput.log의 [한글 글꼴] 줄로 확인.
    /// </summary>
    internal static class HangulFont
    {
        private const char Probe = '한';
        private static readonly string[] OsFonts = { "Malgun Gothic", "맑은 고딕", "Gulim", "굴림", "Noto Sans CJK KR", "Noto Sans KR" };

        private static Type _fontAsset;     // TMPro.TMP_FontAsset
        private static PropertyInfo _font;  // TMP_Text.font
        private static MethodInfo _hasChar; // TMP_FontAsset.HasCharacter(char, bool searchFallbacks, bool tryAddCharacter)
        private static PropertyInfo _globalFallbacks; // TMP_Settings.fallbackFontAssets (static List<TMP_FontAsset>)

        private static FieldInfo _gridTagName; // GridItemView.TagName (태그 글자)
        private static bool _checked;
        private static object _added;
        private static IList _addedTo;

        public static string Enable()
        {
            _fontAsset = Reflect.Type("TMPro.TMP_FontAsset");
            Type tmpText = Reflect.Type("TMPro.TMP_Text");
            Type tmpSettings = Reflect.Type("TMPro.TMP_Settings");
            _font = Reflect.Prop(tmpText, "font");
            _hasChar = Reflect.Method(_fontAsset, "HasCharacter", typeof(char), typeof(bool), typeof(bool));
            _globalFallbacks = tmpSettings?.GetProperty("fallbackFontAssets", Reflect.Stat);

            string missing = Reflect.Missing(
                ("TMP_FontAsset", _fontAsset),
                ("TMP_Text.font", _font),
                ("TMP_FontAsset.HasCharacter(char,bool,bool)", _hasChar),
                ("TMP_Settings.fallbackFontAssets", _globalFallbacks));
            if (missing.Length > 0)
            {
                return "멤버를 찾지 못함: " + missing;
            }

            // 태그 창을 안 열어도, 인벤토리에 한글 태그가 보이는 순간 한 번 검사한다.
            Type view = Reflect.Type("EFT.UI.DragAndDrop.GridItemView");
            _gridTagName = Reflect.Field(view, "TagName");
            MethodInfo updateTag = Reflect.Method(view, "UpdateTag");
            if (updateTag != null && _gridTagName != null)
            {
                Plugin.Harmony.Patch(updateTag, postfix: new HarmonyMethod(typeof(HangulFont), nameof(UpdateTagPostfix)));
            }

            Settings.FontFallback.SettingChanged += (_, __) =>
            {
                if (Settings.FontFallback.Value)
                {
                    _checked = false; // 다음 태그 표시 때 다시 검사
                }
                else
                {
                    Remove();
                }

                GridRefresh.Request();
            };

            return null;
        }

        public static void UpdateTagPostfix(object __instance)
        {
            if (_checked || !Settings.FontFallback.Value)
            {
                return;
            }

            try
            {
                Ensure(_gridTagName.GetValue(__instance));
            }
            catch (Exception e)
            {
                _checked = true;
                Plugin.Log.LogError("[한글 글꼴] 검사 중 오류: " + e);
            }
        }

        /// <summary>sampleText(TMP_Text)의 글꼴로 한글을 못 그리면 예비 글꼴을 붙인다. 한 번만 실제로 검사.</summary>
        public static void Ensure(object sampleText)
        {
            if (_checked || !Settings.FontFallback.Value || _fontAsset == null || Reflect.IsNull(sampleText))
            {
                return;
            }

            _checked = true;
            object font = _font.GetValue(sampleText, null);
            if (!Reflect.IsNull(font) && HasHangul(font, searchFallbacks: true))
            {
                Plugin.Log.LogInfo("[한글 글꼴] 게임 글꼴에 한글이 있어 그대로 씁니다.");
                return;
            }

            object hangul = FindLoadedHangulFont() ?? CreateFromOsFont();
            if (hangul == null)
            {
                Plugin.Log.LogWarning("[한글 글꼴] 한글 글꼴을 찾지 못했습니다 — 한글이 네모로 보일 수 있습니다.");
                return;
            }

            IList list = _globalFallbacks.GetValue(null, null) as IList;
            if (list == null)
            {
                Plugin.Log.LogWarning("[한글 글꼴] TMP 전역 예비 글꼴 목록이 없습니다(TMP_Settings 미로드).");
                return;
            }

            if (!list.Contains(hangul))
            {
                list.Add(hangul);
                _added = hangul;
                _addedTo = list;
            }

            Plugin.Log.LogInfo($"[한글 글꼴] 예비 글꼴 추가: {((UnityEngine.Object)hangul).name}");
            GridRefresh.Request();
        }

        private static bool HasHangul(object font, bool searchFallbacks)
        {
            try
            {
                return (bool)_hasChar.Invoke(font, new object[] { Probe, searchFallbacks, true });
            }
            catch
            {
                return false;
            }
        }

        private static object FindLoadedHangulFont()
        {
            foreach (UnityEngine.Object font in Resources.FindObjectsOfTypeAll(_fontAsset))
            {
                if (HasHangul(font, searchFallbacks: false))
                {
                    Plugin.Log.LogInfo($"[한글 글꼴] 게임에 들어 있는 한글 글꼴 발견: {font.name}");
                    return font;
                }
            }

            return null;
        }

        private static object CreateFromOsFont()
        {
            string[] installed = Font.GetOSInstalledFontNames();
            string family = OsFonts.FirstOrDefault(f => installed.Contains(f));
            if (family == null)
            {
                return null;
            }

            try
            {
                // TMP 3.2+: 시스템 글꼴 이름으로 바로 만들기
                MethodInfo byName = Reflect.Method(_fontAsset, "CreateFontAsset", typeof(string), typeof(string), typeof(int));
                object asset = byName?.Invoke(null, new object[] { family, "Regular", 90 });

                // 구버전 TMP: Unity 동적 글꼴을 거쳐 만들기
                if (Reflect.IsNull(asset))
                {
                    MethodInfo byFont = Reflect.Method(_fontAsset, "CreateFontAsset", typeof(Font));
                    asset = byFont?.Invoke(null, new object[] { Font.CreateDynamicFontFromOSFont(family, 36) });
                }

                if (!Reflect.IsNull(asset) && HasHangul(asset, searchFallbacks: false))
                {
                    ((UnityEngine.Object)asset).name = "Nalasmalssami-" + family;
                    Plugin.Log.LogInfo($"[한글 글꼴] 윈도우 글꼴로 만듦: {family}");
                    return asset;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[한글 글꼴] 윈도우 글꼴({family})로 만들기 실패: {e.Message}");
            }

            return null;
        }

        private static void Remove()
        {
            if (_added != null && _addedTo != null && _addedTo.Contains(_added))
            {
                _addedTo.Remove(_added);
                Plugin.Log.LogInfo("[한글 글꼴] 예비 글꼴 제거.");
            }

            _added = null;
            _addedTo = null;
        }
    }
}
