using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Nalasmalssami.Util;
using UnityEngine;

namespace Nalasmalssami.Features
{
    /// <summary>
    /// 핵심 기능: 태그 입력 칸에서 한글이 지워지지 않게 한다.
    ///
    /// 원인(게임 디컴파일로 확인): 태그 창(EditTagWindow)의 입력 칸은 ValidationInputField이고,
    /// 글자가 바뀔 때마다 ValidationError(입력값 검사)가 "허용 문자 목록"에 없는 글자를
    /// 정규식(_replacer)으로 지워 버린다. 이 목록은 프리팹에 저장된 _validationType에서 만들어지는데,
    /// 태그 칸은 영문·숫자 위주라 한글이 즉시 삭제된다.
    ///
    /// 해결: 태그 창이 열릴 때(Show 직전) 그 입력 칸 하나의 _validator/_replacer만
    /// "게임 기본 목록 + 한글 범위"로 바꿔 끼운다. 닉네임 칸 등 다른 입력 칸은 건드리지 않는다.
    /// </summary>
    internal static class HangulInput
    {
        // 한글 자모(1100–11FF), 호환 자모(3130–318F, 입력 중 'ㄱ','ㅏ' 같은 낱자), 완성형 음절(AC00–D7A3),
        // 확장 자모 A/B(A960–A97F, D7B0–D7FF).
        private const string HangulRanges = @"ᄀ-ᇿ㄰-㆏ꥠ-꥿가-힣ힰ-퟿";

        // 가나(3040–30FF), CJK 기호(3000–303F), 한자(4E00–9FFF, 3400–4DBF), 전각 문자(FF00–FFEF)
        private const string CjkRanges = @"　-〿぀-ヿ㐀-䶿一-鿿＀-￯";

        private static FieldInfo _tagInput;        // EditTagWindow._tagInput
        private static FieldInfo _validator;       // ValidationInputField._validator (Regex)
        private static FieldInfo _replacer;        // ValidationInputField._replacer (Regex)
        private static FieldInfo _validationType;  // ValidationInputField._validationType (EValidationType)
        private static MethodInfo _regexFrom;      // ValidationInputField.GetRegexStringFrom(EValidationType)
        private static MethodInfo _validateCurrent;// ValidationInputField.ValidateCurrentInput()
        private static PropertyInfo _text;         // TMP_InputField.text
        private static PropertyInfo _limit;        // TMP_InputField.characterLimit
        private static PropertyInfo _isFocused;    // TMP_InputField.isFocused
        private static PropertyInfo _textComponent;// TMP_InputField.textComponent (TMP_Text)
        private static PropertyInfo _stringPosition;// TMP_InputField.stringPosition (커서 위치, 선택)
        private static PropertyInfo _charValidation;// TMP_InputField.characterValidation (TMP 자체 글자 검사, 선택)

        // TMP 자체 검사 중 한글을 막는 종류(영문·숫자만 받는 Alphanumeric 등)
        private static readonly HashSet<string> HangulBlockingValidation = new HashSet<string> { "Alphanumeric", "Regex", "EmailAddress" };

        private static readonly Dictionary<int, int> OriginalLimits = new Dictionary<int, int>();
        private static readonly Dictionary<int, object> OriginalValidation = new Dictionary<int, object>();
        private static bool _gameSettingsLogged;

        /// <summary>지금 열려 있는 태그 창과 그 입력 칸(없으면 null).</summary>
        internal static object CurrentWindow { get; private set; }
        private static object _currentInput;

        private static IMECompositionMode? _imeBefore;

        public static string Enable()
        {
            Type window = Reflect.Type("EFT.UI.EditTagWindow");
            Type field = Reflect.Type("EFT.UI.ValidationInputField");
            Type tagComponent = Reflect.Type("EFT.InventoryLogic.TagComponent");
            if (window == null || field == null || tagComponent == null)
            {
                return "EditTagWindow / ValidationInputField / TagComponent 타입을 찾지 못함(게임 버전 변경?)";
            }

            _tagInput = Reflect.Field(window, "_tagInput");
            _validator = Reflect.Field(field, "_validator");
            _replacer = Reflect.Field(field, "_replacer");
            _validationType = Reflect.Field(field, "_validationType");
            _regexFrom = Reflect.MethodByArity(field, "GetRegexStringFrom", 1);
            _validateCurrent = Reflect.Method(field, "ValidateCurrentInput");
            _text = Reflect.Prop(field, "text");
            _limit = Reflect.Prop(field, "characterLimit");
            _isFocused = Reflect.Prop(field, "isFocused");
            _textComponent = Reflect.Prop(field, "textComponent");
            _stringPosition = Reflect.Prop(field, "stringPosition");
            _charValidation = Reflect.Prop(field, "characterValidation");

            MethodInfo show = Reflect.Method(window, "Show", tagComponent);
            MethodInfo save = Reflect.Method(window, "Save");
            MethodInfo close = Reflect.Method(window, "Close");

            string missing = Reflect.Missing(
                ("EditTagWindow._tagInput", _tagInput),
                ("EditTagWindow.Show(TagComponent)", show),
                ("EditTagWindow.Save", save),
                ("EditTagWindow.Close", close),
                ("ValidationInputField._validator", _validator),
                ("ValidationInputField._replacer", _replacer),
                ("ValidationInputField._validationType", _validationType),
                ("ValidationInputField.GetRegexStringFrom", _regexFrom),
                ("TMP_InputField.text", _text),
                ("TMP_InputField.characterLimit", _limit));
            if (missing.Length > 0)
            {
                return "멤버를 찾지 못함: " + missing;
            }

            // Show 앞(prefix): 입력 칸에 원래 태그 글자가 들어가기 "전에" 검사 규칙을 바꿔야
            // 이미 한글로 달린 태그를 다시 열었을 때도 지워지지 않는다.
            Plugin.Harmony.Patch(show, prefix: new HarmonyMethod(typeof(HangulInput), nameof(ShowPrefix)));
            Plugin.Harmony.Patch(save, prefix: new HarmonyMethod(typeof(HangulInput), nameof(SavePrefix)));
            Plugin.Harmony.Patch(close, postfix: new HarmonyMethod(typeof(HangulInput), nameof(ClosePostfix)));

            // 창이 열린 채로 F12에서 바꿔도 바로 적용
            Settings.OnChanged(() =>
            {
                try
                {
                    if (!Reflect.IsNull(_currentInput))
                    {
                        Apply(_currentInput);
                        _validateCurrent?.Invoke(_currentInput, null);
                    }

                    if (!Settings.HangulEnabled.Value || !Settings.ForceIme.Value)
                    {
                        RestoreIme();
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("[한글 입력] 설정 바로 적용 실패: " + e);
                }
            }, Settings.HangulEnabled, Settings.Charset, Settings.MaxLength, Settings.ForceIme);

            return null;
        }

        public static void ShowPrefix(object __instance)
        {
            try
            {
                CurrentWindow = __instance;
                _currentInput = _tagInput.GetValue(__instance);
                Apply(_currentInput);

                if (!Reflect.IsNull(_currentInput) && _textComponent != null)
                {
                    HangulFont.Ensure(_textComponent.GetValue(_currentInput, null));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[한글 입력] 태그 창 준비 중 오류: " + e);
            }
        }

        /// <summary>입력 칸 하나의 허용 문자 규칙과 최대 글자 수를 현재 설정대로 다시 만든다.</summary>
        private static void Apply(object input)
        {
            if (Reflect.IsNull(input))
            {
                return;
            }

            string gameSet = _regexFrom.Invoke(null, new[] { _validationType.GetValue(input) }) as string ?? string.Empty;

            Regex validator, replacer;
            if (!Settings.HangulEnabled.Value)
            {
                if (gameSet.Length == 0)
                {
                    return; // 게임 규칙이 비어 있으면(그럴 일은 없지만) 손대지 않음
                }

                validator = new Regex("^[" + gameSet + "]+$");
                replacer = new Regex("[^" + gameSet + "]");
            }
            else if (Settings.Charset.Value == Settings.CharsetAll)
            {
                // 제어 문자(줄바꿈·탭 등)만 막는다.
                validator = new Regex(@"^[^\p{Cc}]+$");
                replacer = new Regex(@"\p{Cc}");
            }
            else
            {
                string allowed = gameSet + HangulRanges;
                if (Settings.Charset.Value == Settings.CharsetCjk)
                {
                    allowed += CjkRanges;
                }

                validator = new Regex("^[" + allowed + "]+$");
                replacer = new Regex("[^" + allowed + "]");
            }

            _validator.SetValue(input, validator);
            _replacer.SetValue(input, replacer);

            int id = ((UnityEngine.Object)input).GetInstanceID();
            ApplyLimit(input, id);
            ApplyTmpValidation(input, id);

            if (!_gameSettingsLogged)
            {
                _gameSettingsLogged = true;
                Plugin.Log.LogInfo($"[한글 입력] 태그 칸 게임 설정 — 허용 규칙: {_validationType.GetValue(input)} [{gameSet}], " +
                                   $"TMP 검사: {(OriginalValidation.TryGetValue(id, out object v) ? v : "?")}, 최대 글자 수: {OriginalLimits[id]}");
            }
        }

        private static void ApplyLimit(object input, int id)
        {
            if (!OriginalLimits.TryGetValue(id, out int original))
            {
                original = (int)_limit.GetValue(input, null);
                OriginalLimits[id] = original;
            }

            int wanted = Settings.MaxLength.Value > 0 ? Settings.MaxLength.Value : original;
            if ((int)_limit.GetValue(input, null) != wanted)
            {
                _limit.SetValue(input, wanted, null);
            }
        }

        /// <summary>
        /// 게임 정규식보다 먼저 TMP 입력 칸 자체가 글자를 거를 수 있다(예: Alphanumeric = 영문·숫자만).
        /// 그런 종류면 한글 허용 중에만 None(검사 안 함)으로 바꾸고, 끄면 원래대로 돌린다.
        /// 실제 허용 문자는 위의 정규식이 계속 관리하므로 아무 글자나 들어가지는 않는다.
        /// </summary>
        private static void ApplyTmpValidation(object input, int id)
        {
            if (_charValidation == null)
            {
                return;
            }

            if (!OriginalValidation.TryGetValue(id, out object original))
            {
                original = _charValidation.GetValue(input, null);
                OriginalValidation[id] = original;
            }

            bool blocks = original != null && HangulBlockingValidation.Contains(original.ToString());
            object wanted = Settings.HangulEnabled.Value && blocks
                ? Enum.Parse(_charValidation.PropertyType, "None")
                : original;
            if (wanted != null && !Equals(_charValidation.GetValue(input, null), wanted))
            {
                _charValidation.SetValue(input, wanted, null);
            }
        }

        /// <summary>
        /// 한글 입력기는 마지막 글자를 "조합 중"(밑줄) 상태로 들고 있다. 그 상태에서 저장을 누르면
        /// 입력 칸 text에는 아직 그 글자가 없어서 한 글자가 사라질 수 있다 → 저장 직전에 붙여 준다.
        /// </summary>
        public static void SavePrefix(object __instance)
        {
            try
            {
                if (!Settings.HangulEnabled.Value || !Settings.CommitComposition.Value)
                {
                    return;
                }

                string composing = Input.compositionString;
                object input = _tagInput.GetValue(__instance);
                if (string.IsNullOrEmpty(composing) || Reflect.IsNull(input))
                {
                    return;
                }

                // Unity 입력기 규칙: compositionString이 비어 있지 않다 = 아직 확정 안 됨 = text에 없음.
                // 커서 위치(stringPosition)에 끼워 넣고, 못 읽으면 끝에 붙인다.
                string text = _text.GetValue(input, null) as string ?? string.Empty;
                int at = text.Length;
                if (_stringPosition != null && _stringPosition.GetValue(input, null) is int pos && pos >= 0 && pos <= text.Length)
                {
                    at = pos;
                }

                string merged = text.Insert(at, composing);
                int limit = (int)_limit.GetValue(input, null);
                if (limit > 0 && merged.Length > limit)
                {
                    merged = merged.Substring(0, limit);
                }

                _text.SetValue(input, merged, null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[한글 입력] 조합 중 글자 붙이기 실패: " + e);
            }
        }

        public static void ClosePostfix(object __instance)
        {
            if (ReferenceEquals(__instance, CurrentWindow))
            {
                CurrentWindow = null;
                _currentInput = null;
                RestoreIme();
            }
        }

        /// <summary>
        /// 매 프레임: 태그 입력 칸에 커서가 있으면 IME 조합을 켠다. TMP 입력 칸도 포커스 때 켜지만
        /// 게임이 다른 곳에서 끄는 경우가 있어 한/영 전환이 안 될 수 있다(미검증 — 안전장치).
        /// </summary>
        public static void Tick()
        {
            if (Reflect.IsNull(_currentInput))
            {
                RestoreIme(); // 창이 Close 없이 사라진 경우(장면 전환 등)에도 원래대로
                return;
            }

            if (!Settings.HangulEnabled.Value || !Settings.ForceIme.Value || _isFocused == null)
            {
                return;
            }

            bool focused;
            try
            {
                focused = (bool)_isFocused.GetValue(_currentInput, null);
            }
            catch
            {
                return;
            }

            if (focused && Input.imeCompositionMode != IMECompositionMode.On)
            {
                if (_imeBefore == null)
                {
                    _imeBefore = Input.imeCompositionMode;
                }

                Input.imeCompositionMode = IMECompositionMode.On;
            }
        }

        private static void RestoreIme()
        {
            if (_imeBefore != null)
            {
                Input.imeCompositionMode = _imeBefore.Value;
                _imeBefore = null;
            }
        }
    }
}
