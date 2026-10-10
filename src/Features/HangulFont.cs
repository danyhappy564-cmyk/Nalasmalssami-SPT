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
        private static FieldInfo _gridCaption; // GridItemView.Caption (아이템 짧은 이름 — 한국어 게임에선 한글이 잘 보이는 글자)
        private static PropertyInfo _textProp;  // TMP_Text.text
        private static PropertyInfo _fallbackTable; // TMP_FontAsset.fallbackFontAssetTable
        private static PropertyInfo _sharedMat;     // TMP_Text.fontSharedMaterial (글자를 그리는 재질 — 글꼴 그림판과 짝)
        private static Type _subMeshUi;             // TMPro.TMP_SubMeshUI (예비 글꼴 글자를 따로 그리는 자식 조각)

        // 정밀 검사: 한글 태그가 처음 보인 칸을 몇 프레임 뒤(크기 조정 끝난 뒤) 한 번 자세히 기록
        private static object _probeView;
        private static int _probeFrame;
        private static bool _probed;

        // 태그 글자 칸 글꼴 바꿔치기: 칸(인스턴스 ID) → 원래 글꼴
        private static readonly System.Collections.Generic.Dictionary<int, object> SwappedFrom =
            new System.Collections.Generic.Dictionary<int, object>();
        private static readonly System.Collections.Generic.Dictionary<int, object> SwappedMat =
            new System.Collections.Generic.Dictionary<int, object>(); // 칸 ID → 원래 재질
        private static readonly System.Collections.Generic.Dictionary<int, object> SwappedText =
            new System.Collections.Generic.Dictionary<int, object>(); // 칸 ID → 태그 글자(TMP) — 끌 때 되돌리기용
        private static object _directFont;     // 한글이 "자기 안에" 들어 있는 글꼴(예비 글꼴을 거치지 않음)
        private static bool _directResolved;
        private static bool _diagLogged;
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
            _gridCaption = Reflect.Field(view, "Caption");
            _textProp = Reflect.Prop(tmpText, "text");
            _fallbackTable = Reflect.Prop(_fontAsset, "fallbackFontAssetTable");
            _sharedMat = Reflect.Prop(tmpText, "fontSharedMaterial");
            _subMeshUi = Reflect.Type("TMPro.TMP_SubMeshUI");
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
                    RestoreAllSwaps();
                }

                GridRefresh.Request();
            };

            return null;
        }

        public static void UpdateTagPostfix(object __instance)
        {
            if (!Settings.FontFallback.Value)
            {
                return;
            }

            try
            {
                object tagText = _gridTagName.GetValue(__instance);
                Ensure(tagText);
                FixTagFont(__instance, tagText);
            }
            catch (Exception e)
            {
                _checked = true;
                Plugin.Log.LogError("[한글 글꼴] 검사 중 오류: " + e);
            }
        }

        /// <summary>
        /// 1.1.1 인게임 제보: 인벤토리 칸 태그에서 한글만 안 보임(막대 폭은 글자 수만큼 늘어남 → 글자는 있는데 안 그려짐).
        /// 태그 글자 칸 글꼴은 한글을 "예비 글꼴"로만 갖고 있고, 그 예비 글꼴 경로가 칸 안에서 그려지지 않는 것으로 추정(미검증).
        /// → 태그에 한글(비ASCII)이 있고 칸 글꼴 자체에 그 글자가 없으면, 한글을 직접 가진 글꼴로 바꿔 끼운다.
        ///   영어 태그로 바뀌거나 기능을 끄면 원래 글꼴로 되돌린다.
        /// </summary>
        private static void FixTagFont(object view, object tagText)
        {
            if (Reflect.IsNull(tagText) || _textProp == null)
            {
                return;
            }

            int id = ((UnityEngine.Object)tagText).GetInstanceID();
            string tag = _textProp.GetValue(tagText, null) as string ?? string.Empty;
            bool needsHangul = tag.Any(c => c > 0x7F);
            SwappedFrom.TryGetValue(id, out object original);

            if (!needsHangul)
            {
                if (original != null)
                {
                    _font.SetValue(tagText, original, null); // 영어 태그 → 원래 글꼴
                    RestoreMat(id, tagText);
                    SwappedFrom.Remove(id);
                    SwappedText.Remove(id);
                }

                return;
            }

            if (!_probed && _probeView == null)
            {
                _probeView = view;
                _probeFrame = Time.frameCount + 15;
            }

            object current = original ?? _font.GetValue(tagText, null);
            char sample = tag.First(c => c > 0x7F);

            // 1.1.3: 같은 칸의 아이템 이름(Caption)이 한글을 그리고 있으면 그 글꼴+재질을 그대로 빌린다.
            // 1.1.2는 "태그 글꼴에 한글이 직접 있음"이면 아무것도 안 했는데, 그래도 안 보였을 수 있어서
            // 글꼴 검사 결과와 상관없이 "실제로 한글이 보이는 칸"을 따라 한다.
            object caption = _gridCaption?.GetValue(view);
            string captionText = Reflect.IsNull(caption) ? string.Empty : _textProp.GetValue(caption, null) as string ?? string.Empty;
            object direct = null;
            object material = null;
            string how;
            if (captionText.Any(c => c > 0x7F) && !Reflect.IsNull(_font.GetValue(caption, null)))
            {
                direct = _font.GetValue(caption, null);
                material = _sharedMat?.GetValue(caption, null);
                how = "이름 칸 따라 하기";
            }
            else
            {
                if (!Reflect.IsNull(current) && HasChar(current, sample, searchFallbacks: false))
                {
                    return; // 칸 글꼴이 직접 그릴 수 있음
                }

                direct = ResolveDirectFont(view);
                how = "한글 글꼴 찾기";
            }

            LogDiagOnce(view, current, direct, sample, how);
            if (Reflect.IsNull(direct))
            {
                return;
            }

            object nowFont = _font.GetValue(tagText, null);
            object nowMat = _sharedMat?.GetValue(tagText, null);
            if (ReferenceEquals(direct, nowFont) && (Reflect.IsNull(material) || ReferenceEquals(material, nowMat)))
            {
                return; // 이미 같음
            }

            if (original == null)
            {
                SwappedFrom[id] = current;
                SwappedMat[id] = nowMat;
                SwappedText[id] = tagText;
            }

            _font.SetValue(tagText, direct, null);
            if (!Reflect.IsNull(material))
            {
                _sharedMat.SetValue(tagText, material, null);
            }

            if (Dbg.Limit("한글 글꼴 교체", 20))
            {
                Dbg.Log("한글 글꼴", $"태그 '{tag}' 칸 글꼴 교체({how}): {Name(current)} → {Name(direct)}, 재질 {Name(nowMat)} → {Name(material ?? nowMat)}");
            }
        }

        /// <summary>한글을 자기 안에 가진 글꼴 찾기: 아이템 이름 칸 글꼴 → 그 예비 글꼴들 → 태그 칸 예비 글꼴들 → 게임에 불러 둔 글꼴.</summary>
        private static object ResolveDirectFont(object view)
        {
            if (_directResolved)
            {
                return _directFont;
            }

            _directResolved = true;
            var candidates = new System.Collections.Generic.List<object>();
            object caption = _gridCaption?.GetValue(view);
            if (!Reflect.IsNull(caption))
            {
                AddWithFallbacks(candidates, _font.GetValue(caption, null));
            }

            object tagText = _gridTagName.GetValue(view);
            if (!Reflect.IsNull(tagText))
            {
                AddWithFallbacks(candidates, _font.GetValue(tagText, null));
            }

            foreach (object font in candidates)
            {
                if (HasChar(font, Probe, searchFallbacks: false))
                {
                    _directFont = font;
                    return font;
                }
            }

            _directFont = FindLoadedHangulFont() ?? CreateFromOsFont();
            return _directFont;
        }

        private static void AddWithFallbacks(System.Collections.Generic.List<object> list, object font)
        {
            if (Reflect.IsNull(font) || list.Contains(font))
            {
                return;
            }

            list.Add(font);
            if (_fallbackTable?.GetValue(font, null) is IList table)
            {
                foreach (object f in table)
                {
                    if (!Reflect.IsNull(f) && !list.Contains(f))
                    {
                        list.Add(f);
                    }
                }
            }
        }

        private static void LogDiagOnce(object view, object tagFont, object direct, char sample, string how)
        {
            if (_diagLogged)
            {
                return;
            }

            _diagLogged = true;
            object caption = _gridCaption?.GetValue(view);
            object captionFont = Reflect.IsNull(caption) ? null : _font.GetValue(caption, null);
            Plugin.Log.LogInfo(
                $"[한글 글꼴] 태그 칸 글꼴={Name(tagFont)} (직접 '{sample}' 있음={HasChar(tagFont, sample, false)}, 예비 포함={HasChar(tagFont, sample, true)}), " +
                $"이름 칸 글꼴={Name(captionFont)} (직접={HasChar(captionFont, sample, false)}), " +
                $"바꿔 끼울 글꼴={(Reflect.IsNull(direct) ? "못 찾음" : Name(direct))} ({how})");
        }

        private static void RestoreAllSwaps()
        {
            foreach (var pair in SwappedText)
            {
                if (!Reflect.IsNull(pair.Value) && SwappedFrom.TryGetValue(pair.Key, out object original))
                {
                    _font.SetValue(pair.Value, original, null);
                    RestoreMat(pair.Key, pair.Value);
                }
            }

            SwappedFrom.Clear();
            SwappedMat.Clear();
            SwappedText.Clear();
        }

        private static void RestoreMat(int id, object tagText)
        {
            if (SwappedMat.TryGetValue(id, out object mat))
            {
                if (!Reflect.IsNull(mat))
                {
                    _sharedMat?.SetValue(tagText, mat, null);
                }

                SwappedMat.Remove(id);
            }
        }

        /// <summary>Plugin.Update에서 매 프레임 호출 — 예약된 정밀 검사를 한 번 실행.</summary>
        internal static void Tick()
        {
            if (_probeView == null || Time.frameCount < _probeFrame)
            {
                return;
            }

            object view = _probeView;
            _probeView = null;
            _probed = true;
            try
            {
                if (!Reflect.IsNull(view))
                {
                    Plugin.Log.LogInfo("[한글 글꼴][정밀 검사] 태그 글자: " + DescribeText(_gridTagName.GetValue(view)));
                    Plugin.Log.LogInfo("[한글 글꼴][정밀 검사] 이름 글자: " + DescribeText(_gridCaption?.GetValue(view)));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[한글 글꼴][정밀 검사] 실패: " + e.Message);
            }
        }

        /// <summary>TMP 글자 한 칸의 상태를 한 줄로: 글꼴·재질·색·크기·켜짐·글자별로 그려지는지·자식 조각(예비 글꼴용).</summary>
        private static string DescribeText(object text)
        {
            if (Reflect.IsNull(text))
            {
                return "없음";
            }

            var comp = (Component)text;
            var sb = new System.Text.StringBuilder();
            object font = Get(text, "font");
            object mat = Get(text, "fontSharedMaterial");
            sb.Append($"글자='{Get(text, "text")}', 글꼴={Name(font)}(그림판={Name(Get(font, "atlasTexture"))}, 방식={Get(font, "atlasPopulationMode")})");
            sb.Append($", 재질={Name(mat)}(셰이더={Name(Get(mat, "shader"))}, 그림판={Name(Get(mat, "mainTexture"))})");
            sb.Append($", 색={Get(text, "color")}, 크기={Get(text, "fontSize")}, 자동크기={Get(text, "enableAutoSizing")}");
            sb.Append($", 켜짐={comp.gameObject.activeInHierarchy}/{Get(text, "enabled")}, 칸={((RectTransform)comp.transform).rect.size}");
            sb.Append($", 넘침={Get(text, "overflowMode")}, 보이는 글자수={Get(Get(text, "textInfo"), "characterCount")}");

            if (Get(Get(text, "textInfo"), "characterInfo") is Array chars)
            {
                int count = Convert.ToInt32(Get(Get(text, "textInfo"), "characterCount") ?? 0);
                sb.Append(", 글자별=[");
                for (int i = 0; i < Math.Min(count, Math.Min(chars.Length, 8)); i++)
                {
                    object ci = chars.GetValue(i);
                    sb.Append($"'{Get(ci, "character")}':보임={Get(ci, "isVisible")},글꼴={Name(Get(ci, "fontAsset"))} ");
                }

                sb.Append(']');
            }

            if (_subMeshUi != null)
            {
                Component[] subs = comp.GetComponentsInChildren(_subMeshUi, true);
                sb.Append($", 자식 조각 {subs.Length}개");
                foreach (Component sub in subs.Take(4))
                {
                    sb.Append($" [{sub.gameObject.activeInHierarchy}, 글꼴={Name(Get(sub, "fontAsset"))}, 재질={Name(Get(sub, "sharedMaterial"))}]");
                }
            }

            return sb.ToString();
        }

        private static object Get(object o, string member)
        {
            if (Reflect.IsNull(o))
            {
                return null;
            }

            try
            {
                Type t = o.GetType();
                PropertyInfo p = AccessTools.Property(t, member);
                if (p != null)
                {
                    return p.GetValue(o, null);
                }

                return AccessTools.Field(t, member)?.GetValue(o);
            }
            catch
            {
                return null;
            }
        }

        private static string Name(object font) => Reflect.IsNull(font) ? "없음" : font is UnityEngine.Object o ? o.name : font.ToString();

        /// <summary>sampleText(TMP_Text)의 글꼴로 한글을 못 그리면 예비 글꼴을 붙인다. 한 번만 실제로 검사.</summary>
        public static void Ensure(object sampleText)
        {
            if (_checked || !Settings.FontFallback.Value || _fontAsset == null || Reflect.IsNull(sampleText))
            {
                return;
            }

            _checked = true;
            object font = _font.GetValue(sampleText, null);
            Plugin.Log.LogInfo($"[한글 글꼴] 검사 — 글자 칸 글꼴: {(Reflect.IsNull(font) ? "없음" : ((UnityEngine.Object)font).name)}, " +
                              $"예비 글꼴 포함 한글 가능: {(!Reflect.IsNull(font) && HasHangul(font, searchFallbacks: true))}");
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

        private static bool HasHangul(object font, bool searchFallbacks) => HasChar(font, Probe, searchFallbacks);

        private static bool HasChar(object font, char c, bool searchFallbacks)
        {
            if (Reflect.IsNull(font))
            {
                return false;
            }

            try
            {
                return (bool)_hasChar.Invoke(font, new object[] { c, searchFallbacks, true });
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

        /// <summary>상태 기록용</summary>
        internal static string DescribeState()
        {
            int count = _globalFallbacks?.GetValue(null, null) is IList list ? list.Count : -1;
            return $"검사 완료={_checked}, 우리가 붙인 예비 글꼴={(_added == null ? "없음" : ((UnityEngine.Object)_added).name)}, " +
                   $"TMP 전역 예비 글꼴 수={count}, 태그 칸 교체용 글꼴={(_directResolved ? Name(_directFont) : "아직 안 찾음")}, 교체한 칸 {SwappedFrom.Count}개";
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
