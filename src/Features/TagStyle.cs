using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Nalasmalssami.Util;
using UnityEngine;

namespace Nalasmalssami.Features
{
    /// <summary>
    /// 아이템 칸 태그 글자 모양: ① 밝은 색 막대면 검은 글씨(자동 대비) ② 글자 크기 배율 ③ 굵기 ④ 글자 위치.
    /// GridItemView.UpdateTag(태그 표시 갱신) 뒤에 실행 — 이때 막대 색은 이미 칠해져 있으므로
    /// 어느 모드가 색을 정했든(나랏말싸미/MoreTagColours/게임 기본) 실제 막대 색을 보고 판단한다.
    /// 원래 글자 색·크기·스타일은 칸마다 처음 본 값을 기억해 두고, 설정을 끄면 그대로 돌려놓는다.
    /// </summary>
    internal static class TagStyle
    {
        private const int BoldFlag = 1; // TMPro.FontStyles.Bold
        private static readonly Color DarkText = new Color(0.07f, 0.07f, 0.07f, 1f);

        private static FieldInfo _tagName;         // GridItemView.TagName (TMP 글자)
        private static FieldInfo _tagColor;        // GridItemView._tagColor (Image 막대)
        private static PropertyInfo _barColor;     // Graphic.color (막대)
        private static PropertyInfo _textColor;    // Graphic.color (글자)
        private static PropertyInfo _fontSize;     // TMP_Text.fontSize
        private static PropertyInfo _fontSizeMax;  // TMP_Text.fontSizeMax (자동 크기일 때)
        private static PropertyInfo _autoSize;     // TMP_Text.enableAutoSizing
        private static PropertyInfo _fontStyle;    // TMP_Text.fontStyle (FontStyles 플래그)
        private static PropertyInfo _margin;       // TMP_Text.margin (글자 칸 안쪽 여백: 왼·위·오른·아래)

        private sealed class Original
        {
            public Color Color;
            public float Size;
            public float SizeMax;
            public int Style;
            public Vector4 Margin;
        }

        private static readonly Dictionary<int, Original> Originals = new Dictionary<int, Original>();
        private static bool _errorLogged;

        public static string Enable()
        {
            Type view = Reflect.Type("EFT.UI.DragAndDrop.GridItemView");
            _tagName = Reflect.Field(view, "TagName");
            _tagColor = Reflect.Field(view, "_tagColor");
            _barColor = Reflect.Prop(_tagColor?.FieldType, "color");
            Type text = _tagName?.FieldType;
            _textColor = Reflect.Prop(text, "color");
            _fontSize = Reflect.Prop(text, "fontSize");
            _fontSizeMax = Reflect.Prop(text, "fontSizeMax");
            _autoSize = Reflect.Prop(text, "enableAutoSizing");
            _fontStyle = Reflect.Prop(text, "fontStyle");
            _margin = Reflect.Prop(text, "margin");
            MethodInfo updateTag = Reflect.Method(view, "UpdateTag");

            string missing = Reflect.Missing(
                ("GridItemView.UpdateTag", updateTag),
                ("GridItemView.TagName", _tagName),
                ("GridItemView._tagColor", _tagColor),
                ("Graphic.color", _barColor),
                ("TMP_Text.color", _textColor),
                ("TMP_Text.fontSize", _fontSize),
                ("TMP_Text.fontStyle", _fontStyle));
            if (missing.Length > 0)
            {
                return "멤버를 찾지 못함: " + missing;
            }

            Plugin.Harmony.Patch(updateTag, postfix: new HarmonyMethod(typeof(TagStyle), nameof(UpdateTagPostfix)));
            Settings.OnChanged(GridRefresh.Request,
                Settings.AutoContrast, Settings.ContrastThreshold, Settings.TagFontScale, Settings.TagWeight,
                Settings.TagOffsetX, Settings.TagOffsetY);
            return null;
        }

        public static void UpdateTagPostfix(object __instance)
        {
            try
            {
                object text = _tagName.GetValue(__instance);
                object bar = _tagColor.GetValue(__instance);
                if (Reflect.IsNull(text) || Reflect.IsNull(bar))
                {
                    return;
                }

                Original original = Remember(text);

                // ① 글자색: 막대 밝기(사람 눈 기준 가중치)로 판단
                Color barColor = (Color)_barColor.GetValue(bar, null);
                float brightness = 0.299f * barColor.r + 0.587f * barColor.g + 0.114f * barColor.b;
                Color wantedColor = Settings.AutoContrast.Value && brightness > Settings.ContrastThreshold.Value
                    ? DarkText
                    : original.Color;
                if ((Color)_textColor.GetValue(text, null) != wantedColor)
                {
                    _textColor.SetValue(text, wantedColor, null);
                }

                // ② 크기: 자동 크기 글자면 최대 크기를, 아니면 크기를 배율만큼
                float scale = Settings.TagFontScale.Value;
                bool auto = _autoSize != null && (bool)_autoSize.GetValue(text, null);
                if (auto && _fontSizeMax != null)
                {
                    SetIfDifferent(_fontSizeMax, text, original.SizeMax * scale);
                }
                else
                {
                    SetIfDifferent(_fontSize, text, original.Size * scale);
                }

                // ③ 굵기: 1.1.4까지는 "끔 = 원래 값"이라 게임 원래 값이 굵게면 꺼도 굵게 남았다 → 3단계로 명확히
                string weight = Settings.TagWeight.Value;
                int style = weight == Settings.WeightBold ? original.Style | BoldFlag
                    : weight == Settings.WeightNormal ? original.Style & ~BoldFlag
                    : original.Style;
                if (Convert.ToInt32(_fontStyle.GetValue(text, null)) != style)
                {
                    _fontStyle.SetValue(text, Enum.ToObject(_fontStyle.PropertyType, style), null);
                }

                // ④ 위치: 여백으로 글자 칸만 민다(막대·폭 계산은 그대로 — 좌우 여백 합이 0이라 글자 폭도 안 변함)
                if (_margin != null)
                {
                    float x = Settings.TagOffsetX.Value;
                    float y = Settings.TagOffsetY.Value;
                    Vector4 margin = original.Margin + new Vector4(x, -y, -x, y);
                    if ((Vector4)_margin.GetValue(text, null) != margin)
                    {
                        _margin.SetValue(text, margin, null);
                    }
                }

                if (Dbg.Limit("태그 글자 모양", 40))
                {
                    string tag = TagAccess.GetTagName(ItemOf(__instance));
                    if (tag != null)
                    {
                        Dbg.Log("태그 글자 모양", $"'{tag}': 막대색=({barColor.r:0.00},{barColor.g:0.00},{barColor.b:0.00}) 밝기={brightness:0.00} " +
                                             $"→ {(wantedColor == DarkText ? "검은 글씨" : "원래 글씨")}, 크기={(auto ? "자동" : "고정")} x{scale:0.00}, " +
                                             $"굵기={weight}(원래 굵게={(original.Style & BoldFlag) != 0}), 위치=({Settings.TagOffsetX.Value:0.#},{Settings.TagOffsetY.Value:0.#})");
                    }
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("[태그 글자 모양] 오류(이후 생략): " + e);
                }
            }
        }

        private static PropertyInfo _item; // ItemView.Item

        private static object ItemOf(object view)
        {
            _item = _item ?? Reflect.Prop(view.GetType(), "Item");
            return _item?.GetValue(view, null);
        }

        private static Original Remember(object text)
        {
            int id = ((UnityEngine.Object)text).GetInstanceID();
            if (!Originals.TryGetValue(id, out Original original))
            {
                original = new Original
                {
                    Color = (Color)_textColor.GetValue(text, null),
                    Size = (float)_fontSize.GetValue(text, null),
                    SizeMax = _fontSizeMax != null ? (float)_fontSizeMax.GetValue(text, null) : 0f,
                    Style = Convert.ToInt32(_fontStyle.GetValue(text, null)),
                    Margin = _margin != null ? (Vector4)_margin.GetValue(text, null) : Vector4.zero,
                };
                Originals[id] = original;
            }

            return original;
        }

        private static void SetIfDifferent(PropertyInfo prop, object target, float value)
        {
            if (Math.Abs((float)prop.GetValue(target, null) - value) > 0.01f)
            {
                prop.SetValue(target, value, null);
            }
        }
    }
}
