// 이 파일의 동작 원리와 대부분의 코드는 FalconPilot의 "SPT Tag over name"(MIT)에서 가져왔다.
// 원본: https://github.com/FalconPilot/spt-tag-over-name  (Copyright (c) 2026 FalconPilot)
// 바뀐 점: F12 설정을 바꾸면 인벤토리를 다시 열지 않아도 바로 적용(GridRefresh), 주석 한글화.
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Nalasmalssami.Util;

namespace Nalasmalssami.Features
{
    /// <summary>
    /// 게임 기본 동작을 뒤집는다: 태그가 있으면 태그 글자 대신 "아이템 짧은 이름"을 숨긴다.
    ///
    /// 게임(GridItemView.ResizeTag): 남는 폭 = 칸 폭 - 이름 글자 폭 - 2. 40px 미만이면 태그 글자를 숨기고 색 막대만 남김.
    /// 여기선: 태그가 있으면 이름(Caption)을 비워서 남는 폭이 항상 충분하게 만든다.
    /// 한글 태그는 영어보다 글자당 폭이 넓어 잘리기 쉬워서 특히 유용하다.
    /// </summary>
    internal static class TagOverName
    {
        private static bool _errorLogged;

        public static string Enable()
        {
            if (!GridViewAccess.Init(out string error))
            {
                return error;
            }

            Plugin.Harmony.Patch(GridViewAccess.UpdateItemName,
                postfix: new HarmonyMethod(typeof(TagOverName), nameof(UpdateItemNamePostfix)));
            Plugin.Harmony.Patch(GridViewAccess.UpdateTag,
                postfix: new HarmonyMethod(typeof(TagOverName), nameof(UpdateTagPostfix)));

            if (GridViewAccess.LayoutFixAvailable)
            {
                Plugin.Harmony.Patch(GridViewAccess.ResizeTag,
                    postfix: new HarmonyMethod(typeof(TagOverName), nameof(ResizeTagPostfix)));
            }
            else
            {
                Plugin.Log.LogWarning("[태그를 이름 위에] 배치 보정용 멤버를 못 찾아 기본 모드로만 동작합니다.");
            }

            Settings.HideNameWhenTagged.SettingChanged += (_, __) => GridRefresh.Request();
            return null;
        }

        // 짧은 이름은 GridItemView.UpdateItemName(이름 글자 쓰기)에서만 쓰인다 → 직후에 태그가 있으면 비운다.
        public static void UpdateItemNamePostfix(object __instance)
        {
            try
            {
                if (Settings.HideNameWhenTagged.Value && GridViewAccess.HasVisibleTag(__instance))
                {
                    GridViewAccess.SetCaptionText(__instance, string.Empty);
                }
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        // UpdateTag(태그 표시 갱신)는 ResizeTag를 부르고, ResizeTag는 한 프레임 뒤 이름 폭을 잰다.
        // 그래서 여기서도 이름을 비우고, 태그가 지워졌거나 설정이 꺼졌으면 이름을 되살린다.
        public static void UpdateTagPostfix(object __instance)
        {
            try
            {
                if (Settings.HideNameWhenTagged.Value && GridViewAccess.HasVisibleTag(__instance))
                {
                    GridViewAccess.SetCaptionText(__instance, string.Empty);
                }
                else if (GridViewAccess.CaptionIsEmpty(__instance))
                {
                    GridViewAccess.UpdateItemName.Invoke(__instance, null);
                }
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        // ResizeTag(비동기)가 오래된 폭 값 때문에 태그 글자를 숨긴 채 끝날 수 있다 →
        // 끝난 뒤 태그 있는 아이템만 게임과 같은 공식으로 다시 배치: 막대 폭 = clamp(글자 폭 + 12, 40, 칸 폭 - 2).
        public static void ResizeTagPostfix(object __instance, Task __result)
        {
            try
            {
                if (__result == null || !Settings.HideNameWhenTagged.Value)
                {
                    return;
                }

                // 게임의 Task.Yield()와 같은 Unity 메인 스레드에서 이어서 실행
                __result.ContinueWith(
                    _ => ApplyTagLayout(__instance),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        private static void ApplyTagLayout(object view)
        {
            try
            {
                if (Settings.HideNameWhenTagged.Value && GridViewAccess.HasVisibleTag(view))
                {
                    GridViewAccess.ForceTagLayout(view);
                }
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        private static void LogOnce(Exception e)
        {
            if (_errorLogged)
            {
                return;
            }

            _errorLogged = true;
            Plugin.Log.LogError("[태그를 이름 위에] 오류(이후 같은 오류는 생략): " + e);
        }
    }

    /// <summary>EFT.UI.DragAndDrop.GridItemView(인벤토리 아이템 칸) 멤버 리플렉션 접근 (SPT 4.1.x).</summary>
    internal static class GridViewAccess
    {
        private const BindingFlags Any = Reflect.Inst;

        internal static MethodInfo UpdateItemName;
        internal static MethodInfo UpdateTag;
        internal static MethodInfo ResizeTag;
        internal static bool LayoutFixAvailable;

        private static FieldInfo _caption;          // TMP: 짧은 이름
        private static FieldInfo _tagName;          // TMP: 태그 글자
        private static FieldInfo _tagColor;         // Image: 태그 색 막대
        private static PropertyInfo _captionText;   // TMP_Text.text
        private static PropertyInfo _item;          // ItemView.Item
        private static MethodInfo _getTagComponent; // Item.GetItemComponent<TagComponent>()
        private static FieldInfo _tagComponentName; // TagComponent.Name
        private static MethodInfo _forceMeshUpdate; // TMP_Text.ForceMeshUpdate(...) (선택)
        private static object[] _forceMeshUpdateArgs;
        private static PropertyInfo _preferredWidth; // TMP_Text.preferredWidth
        private static PropertyInfo _gameObject;     // Component.gameObject
        private static MethodInfo _setActive;        // GameObject.SetActive(bool)
        private static PropertyInfo _graphicRect;    // Graphic.rectTransform
        private static PropertyInfo _sizeDelta;      // RectTransform.sizeDelta
        private static PropertyInfo _viewRect;       // ItemView.RectTransform
        private static FieldInfo _vecX;              // Vector2.x

        internal static bool Init(out string error)
        {
            error = null;

            Type view = Reflect.Type("EFT.UI.DragAndDrop.GridItemView");
            Type item = Reflect.Type("EFT.InventoryLogic.Item");
            Type tag = Reflect.Type("EFT.InventoryLogic.TagComponent");
            if (view == null || item == null || tag == null)
            {
                error = "GridItemView / Item / TagComponent 타입을 찾지 못함(게임 버전 변경?)";
                return false;
            }

            UpdateItemName = view.GetMethod("UpdateItemName", Any, null, Type.EmptyTypes, null);
            UpdateTag = view.GetMethod("UpdateTag", Any, null, Type.EmptyTypes, null);
            _caption = view.GetField("Caption", Any);
            _tagName = view.GetField("TagName", Any);
            _tagColor = view.GetField("_tagColor", Any);
            _item = view.GetProperty("Item", Any);
            _tagComponentName = tag.GetField("Name", Any);
            _captionText = _caption?.FieldType.GetProperty("text", Any);

            // 선택: 이름을 비운 뒤 TMP가 바로 다시 계산하게 해서, 한 프레임 뒤 ResizeTag가 읽는
            // renderedWidth가 옛 값이 아니라 0이 되게 한다.
            _forceMeshUpdate = _caption?.FieldType.GetMethods(Any)
                .Where(m => m.Name == "ForceMeshUpdate")
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
            if (_forceMeshUpdate != null)
            {
                // 첫 bool(ignoreActiveState) = true, 나머지는 기본값/false
                _forceMeshUpdateArgs = _forceMeshUpdate.GetParameters()
                    .Select((p, i) => p.ParameterType == typeof(bool) ? (object)(i == 0)
                        : p.HasDefaultValue ? p.DefaultValue : null)
                    .ToArray();
            }

            MethodInfo generic = item.GetMethods(Any).FirstOrDefault(m =>
                m.Name == "GetItemComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            _getTagComponent = generic?.MakeGenericMethod(tag);

            ResolveLayoutMembers(view);

            string missing = Reflect.Missing(
                ("GridItemView.UpdateItemName", UpdateItemName),
                ("GridItemView.UpdateTag", UpdateTag),
                ("GridItemView.Caption", _caption),
                ("GridItemView.TagName", _tagName),
                ("GridItemView._tagColor", _tagColor),
                ("ItemView.Item", _item),
                ("TMP_Text.text", _captionText),
                ("Item.GetItemComponent<T>", _getTagComponent),
                ("TagComponent.Name", _tagComponentName));
            if (missing.Length > 0)
            {
                error = "멤버를 찾지 못함: " + missing;
                return false;
            }

            return true;
        }

        // 선택 기능: 하나라도 없으면 배치 보정 없이 기본 모드로 동작
        private static void ResolveLayoutMembers(Type view)
        {
            try
            {
                ResizeTag = view.GetMethod("ResizeTag", Any, null, Type.EmptyTypes, null);
                _preferredWidth = _tagName?.FieldType.GetProperty("preferredWidth", Any);
                _gameObject = _tagName?.FieldType.GetProperty("gameObject", Any);
                _setActive = _gameObject?.PropertyType.GetMethod("SetActive", Any, null, new[] { typeof(bool) }, null);
                _graphicRect = _tagColor?.FieldType.GetProperty("rectTransform", Any);
                _sizeDelta = _graphicRect?.PropertyType.GetProperty("sizeDelta", Any);
                _viewRect = view.GetProperty("RectTransform", Any);
                _vecX = _sizeDelta?.PropertyType.GetField("x", Any);

                LayoutFixAvailable =
                    ResizeTag != null && typeof(Task).IsAssignableFrom(ResizeTag.ReturnType)
                    && _preferredWidth != null && _setActive != null && _graphicRect != null
                    && _sizeDelta != null && _viewRect != null && _vecX != null;
            }
            catch (AmbiguousMatchException)
            {
                LayoutFixAvailable = false;
            }
        }

        // GridItemView.ResizeTag의 "폭이 충분할 때" 분기를 그대로 재현
        internal static void ForceTagLayout(object view)
        {
            object tagName = _tagName.GetValue(view);
            object tagColor = _tagColor.GetValue(view);
            object viewRect = _viewRect.GetValue(view, null);
            if (Reflect.IsNull(tagName) || Reflect.IsNull(tagColor) || Reflect.IsNull(viewRect))
            {
                return;
            }

            float itemWidth = (float)_vecX.GetValue(_sizeDelta.GetValue(viewRect, null));
            float max = itemWidth - 2f;
            if (max < 40f)
            {
                return; // 칸이 너무 좁음: 게임도 태그 글자를 안 보여 줌
            }

            object tagGameObject = _gameObject.GetValue(tagName, null);
            _setActive.Invoke(tagGameObject, new object[] { true });

            float value = (float)_preferredWidth.GetValue(tagName, null) + 12f;
            float width = value < 40f ? 40f : (value > max ? max : value);

            object bar = _graphicRect.GetValue(tagColor, null);
            object size = _sizeDelta.GetValue(bar, null);
            _vecX.SetValue(size, width);          // 박싱된 구조체 사본을 고치고
            _sizeDelta.SetValue(bar, size, null); // 다시 대입
        }

        // GridItemView.UpdateTag와 같은 기준: 태그 UI가 있고, 태그 이름이 비어 있지 않을 때만 "보이는 태그"
        internal static bool HasVisibleTag(object view)
        {
            if (Reflect.IsNull(_tagColor.GetValue(view)) || Reflect.IsNull(_tagName.GetValue(view)) || Reflect.IsNull(_caption.GetValue(view)))
            {
                return false;
            }

            object item = _item.GetValue(view, null);
            if (item == null)
            {
                return false;
            }

            object tag = _getTagComponent.Invoke(item, null);
            if (tag == null)
            {
                return false;
            }

            return _tagComponentName.GetValue(tag) is string name && !string.IsNullOrEmpty(name.Trim());
        }

        internal static bool CaptionIsEmpty(object view)
        {
            object caption = _caption.GetValue(view);
            return !Reflect.IsNull(caption) && string.IsNullOrEmpty(_captionText.GetValue(caption, null) as string);
        }

        internal static void SetCaptionText(object view, string text)
        {
            object caption = _caption.GetValue(view);
            if (Reflect.IsNull(caption))
            {
                return;
            }

            _captionText.SetValue(caption, text, null);
            _forceMeshUpdate?.Invoke(caption, _forceMeshUpdateArgs);
        }
    }
}
