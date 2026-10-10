using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Nalasmalssami.Util;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Nalasmalssami.Features
{
    /// <summary>
    /// 태그 창에 "자주 쓰는 태그" 버튼 줄을 붙인다. 누르면 글자(와 색)가 들어가고, 설정에 따라 바로 저장.
    ///
    /// 버튼은 태그 창의 저장 버튼(DefaultUIButton)을 복제해서 만든다 → 게임 버튼과 같은 모양·소리.
    /// 버튼 줄은 태그 창의 "형제"(같은 부모, 바로 뒤 순서)로 붙인다: 창 안에 넣으면 창 영역 밖이 잘릴 수 있고,
    /// 형제로 두면 창 뒤의 배경보다 위에 그려져서 클릭이 인벤토리로 새지 않는다.
    /// 위치는 매 프레임 창의 아래(또는 위) 모서리에 맞춘다.
    /// </summary>
    internal static class PresetBar
    {
        private const int MaxPresets = 10;
        private const int ButtonFontSize = 18;
        private const float Gap = 4f;
        private const float Margin = 6f;

        private static FieldInfo _saveButton;   // EditTagWindow._saveButtonSpawner (DefaultUIButton)
        private static FieldInfo _tagInput;     // EditTagWindow._tagInput
        private static FieldInfo _colorsPanel;  // EditTagWindow._colorsPanel (TagColorsPanel)
        private static FieldInfo _cells;        // TagColorsPanel._cells (ColorView[])
        private static MethodInfo _colorSelected; // EditTagWindow.ColorSelectedHandler(ColorView, int)
        private static MethodInfo _save;        // EditTagWindow.Save()
        private static FieldInfo _onClick;      // DefaultUIButton.OnClick (UnityEvent)
        private static MethodInfo _setRawText;  // DefaultUIButton.SetRawText(string, int)
        private static FieldInfo _headerLabel;  // DefaultUIButton._headerLabel (TMP)
        private static PropertyInfo _preferredWidth; // TMP_Text.preferredWidth
        private static PropertyInfo _inputText; // TMP_InputField.text
        private static PropertyInfo _inputLimit;// TMP_InputField.characterLimit
        private static Type _layoutElement;     // UnityEngine.UI.LayoutElement
        private static PropertyInfo _ignoreLayout;

        private static GameObject _bar;
        private static Component _barWindow;
        private static readonly List<(RectTransform rect, object button)> Buttons = new List<(RectTransform, object)>();

        private struct Preset
        {
            public string Text;
            public int ColorIndex; // -1 = 색은 안 바꿈
        }

        public static string Enable()
        {
            Type window = Reflect.Type("EFT.UI.EditTagWindow");
            Type tagComponent = Reflect.Type("EFT.InventoryLogic.TagComponent");
            Type button = Reflect.Type("EFT.UI.DefaultUIButton");
            Type panel = Reflect.Type("EFT.UI.TagColorsPanel");
            Type input = Reflect.Type("EFT.UI.ValidationInputField");

            _saveButton = Reflect.Field(window, "_saveButtonSpawner");
            _tagInput = Reflect.Field(window, "_tagInput");
            _colorsPanel = Reflect.Field(window, "_colorsPanel");
            _cells = Reflect.Field(panel, "_cells");
            _colorSelected = Reflect.MethodByArity(window, "ColorSelectedHandler", 2);
            _save = Reflect.Method(window, "Save");
            _onClick = Reflect.Field(button, "OnClick");
            _setRawText = Reflect.MethodByArity(button, "SetRawText", 2);
            _headerLabel = Reflect.Field(button, "_headerLabel");
            _preferredWidth = Reflect.Prop(_headerLabel?.FieldType, "preferredWidth");
            _inputText = Reflect.Prop(input, "text");
            _inputLimit = Reflect.Prop(input, "characterLimit");
            _layoutElement = Reflect.Type("UnityEngine.UI.LayoutElement");
            _ignoreLayout = Reflect.Prop(_layoutElement, "ignoreLayout");

            MethodInfo show = Reflect.Method(window, "Show", tagComponent);
            MethodInfo close = Reflect.Method(window, "Close");

            string missing = Reflect.Missing(
                ("EditTagWindow.Show(TagComponent)", show),
                ("EditTagWindow.Close", close),
                ("EditTagWindow._saveButtonSpawner", _saveButton),
                ("EditTagWindow._tagInput", _tagInput),
                ("EditTagWindow.Save", _save),
                ("DefaultUIButton.OnClick", _onClick),
                ("DefaultUIButton.SetRawText", _setRawText),
                ("TMP_InputField.text", _inputText));
            if (missing.Length > 0)
            {
                return "멤버를 찾지 못함: " + missing;
            }

            // 창 크기 조절(태그 색상 확장) 다음에 붙이도록 늦게 실행
            Plugin.Harmony.Patch(show, postfix: new HarmonyMethod(typeof(PresetBar), nameof(ShowPostfix)) { priority = Priority.Low });
            Plugin.Harmony.Patch(close, postfix: new HarmonyMethod(typeof(PresetBar), nameof(ClosePostfix)));

            Settings.OnChanged(() =>
            {
                if (_barWindow != null && !Reflect.IsNull(_barWindow))
                {
                    Build(_barWindow);
                }
            }, Settings.PresetsEnabled, Settings.Presets);
            return null;
        }

        public static void ShowPostfix(object __instance)
        {
            try
            {
                Build(__instance as Component);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[태그 프리셋] 버튼 만들기 실패: " + e);
                Destroy();
            }
        }

        public static void ClosePostfix(object __instance)
        {
            if (ReferenceEquals(__instance, _barWindow))
            {
                Destroy();
            }
        }

        private static void Build(Component window)
        {
            Destroy();
            if (window == null || !Settings.PresetsEnabled.Value)
            {
                return;
            }

            List<Preset> presets = Parse(Settings.Presets.Value);
            Component template = _saveButton.GetValue(window) as Component;
            Transform parent = window.transform.parent;
            if (presets.Count == 0 || template == null || parent == null)
            {
                return;
            }

            _barWindow = window;
            _bar = new GameObject("Nalasmalssami_TagPresets", typeof(RectTransform));
            _bar.transform.SetParent(parent, false);
            _bar.transform.SetSiblingIndex(window.transform.GetSiblingIndex() + 1);
            if (_layoutElement != null && _ignoreLayout != null)
            {
                // 부모에 자동 배치(LayoutGroup)가 있어도 우리 줄은 건드리지 않게
                _ignoreLayout.SetValue(_bar.AddComponent(_layoutElement), true, null);
            }

            foreach (Preset preset in presets)
            {
                GameObject go = Object.Instantiate(template.gameObject, _bar.transform, false);
                go.name = "Preset_" + preset.Text;
                go.SetActive(true);
                object button = go.GetComponent(template.GetType());
                _setRawText.Invoke(button, new object[] { preset.Text, ButtonFontSize });

                // 복제본의 클릭 이벤트는 비어 있다(실행 중 붙인 저장 처리기는 복제되지 않음). 혹시 몰라 비우고 우리 것만.
                var onClick = (UnityEvent)_onClick.GetValue(button);
                onClick.RemoveAllListeners();
                Preset captured = preset;
                onClick.AddListener(() => OnPresetClicked(window, captured));

                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                Buttons.Add((rect, button));
            }

            Layout();
        }

        /// <summary>매 프레임: 창 위치·크기를 따라가고, 버튼 폭을 글자에 맞춰 줄바꿈 배치.</summary>
        public static void Tick()
        {
            if (_bar == null)
            {
                return;
            }

            if (Reflect.IsNull(_barWindow) || !_barWindow.gameObject.activeInHierarchy)
            {
                Destroy(); // 창이 Close 없이 사라진 경우
                return;
            }

            Layout();
        }

        private static void Layout()
        {
            var windowRect = _barWindow.GetComponent<RectTransform>();
            var barRect = _bar.GetComponent<RectTransform>();
            if (windowRect == null || barRect == null)
            {
                return;
            }

            float maxWidth = Mathf.Max(windowRect.rect.width, 120f);
            float x = 0f, y = 0f, rowHeight = 0f;
            foreach ((RectTransform rect, object button) in Buttons)
            {
                if (rect == null)
                {
                    continue;
                }

                float width = Mathf.Clamp(LabelWidth(button) + 28f, 56f, maxWidth);
                float height = rect.rect.height > 1f ? rect.rect.height : 32f;
                if (x > 0f && x + width > maxWidth)
                {
                    x = 0f;
                    y += rowHeight + Gap;
                    rowHeight = 0f;
                }

                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = new Vector2(x, -y);
                x += width + Gap;
                rowHeight = Mathf.Max(rowHeight, height);
            }

            // 줄 전체를 창의 아래(또는 위) 모서리에 붙인다. 같은 부모라 크기 단위가 같다.
            bool above = Settings.PresetPosition.Value == Settings.PresetAbove;
            barRect.anchorMin = barRect.anchorMax = windowRect.anchorMin == windowRect.anchorMax ? windowRect.anchorMin : new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0f, above ? 0f : 1f);
            barRect.sizeDelta = new Vector2(maxWidth, y + rowHeight);

            var corners = new Vector3[4]; // 0 왼쪽아래, 1 왼쪽위, 2 오른쪽위, 3 오른쪽아래 (월드 좌표)
            windowRect.GetWorldCorners(corners);
            Vector3 anchor = above ? corners[1] : corners[0];
            float offset = Margin * windowRect.lossyScale.y;
            barRect.position = anchor + new Vector3(0f, above ? offset : -offset, 0f);
        }

        private static float LabelWidth(object button)
        {
            try
            {
                object label = _headerLabel?.GetValue(button);
                if (!Reflect.IsNull(label) && _preferredWidth != null)
                {
                    return (float)_preferredWidth.GetValue(label, null);
                }
            }
            catch
            {
                // 아래 추정값 사용
            }

            return 60f;
        }

        private static void OnPresetClicked(Component window, Preset preset)
        {
            try
            {
                object input = _tagInput.GetValue(window);
                if (Reflect.IsNull(input))
                {
                    return;
                }

                string text = preset.Text;
                int limit = _inputLimit != null ? (int)_inputLimit.GetValue(input, null) : 0;
                if (limit > 0 && text.Length > limit)
                {
                    text = text.Substring(0, limit);
                }

                _inputText.SetValue(input, text, null); // 입력 칸 검사(한글 허용 규칙)와 글자 수 표시도 같이 갱신됨

                if (preset.ColorIndex >= 0 && _colorsPanel != null && _cells != null && _colorSelected != null)
                {
                    object panel = _colorsPanel.GetValue(window);
                    if (!Reflect.IsNull(panel) && _cells.GetValue(panel) is Array cells && preset.ColorIndex < cells.Length)
                    {
                        _colorSelected.Invoke(window, new[] { cells.GetValue(preset.ColorIndex), (object)preset.ColorIndex });
                    }
                }

                if (Settings.PresetInstantSave.Value)
                {
                    _save.Invoke(window, null);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[태그 프리셋] 적용 실패: " + e);
            }
        }

        /// <summary>"팔 것=1, 퀘템=3, 보관" → [(팔 것, 0), (퀘템, 2), (보관, -1)]</summary>
        private static List<Preset> Parse(string value)
        {
            var list = new List<Preset>();
            foreach (string raw in (value ?? string.Empty).Split(','))
            {
                string part = raw.Trim();
                int colour = -1;
                int eq = part.LastIndexOf('=');
                if (eq > 0 && int.TryParse(part.Substring(eq + 1).Trim(), out int n) && n >= 1)
                {
                    colour = n - 1;
                    part = part.Substring(0, eq).Trim();
                }

                if (part.Length > 0)
                {
                    list.Add(new Preset { Text = part, ColorIndex = colour });
                }

                if (list.Count >= MaxPresets)
                {
                    break;
                }
            }

            return list;
        }

        private static void Destroy()
        {
            if (_bar != null)
            {
                Object.Destroy(_bar);
            }

            _bar = null;
            _barWindow = null;
            Buttons.Clear();
        }
    }
}
