using System;
using System.Reflection;
using HarmonyLib;
using Nalasmalssami.Util;

namespace Nalasmalssami.Features
{
    /// <summary>
    /// 창고 검색(돋보기) 창에서 태그 글자로도 찾기.
    ///
    /// 게임(StashSearchWindow, 디컴파일 확인): 검색어가 바뀌면 이름에 검색어가 들어간 아이템 "종류" 목록을 만들고,
    /// CheckItem(아이템 하나가 검색 결과에 들어갈지 판단)이 그 목록에 없는 아이템을 뺀다.
    /// 여기선 CheckItem이 "아니오"라고 했을 때, 그 아이템 태그에 검색어가 있으면 검색어 조건만 빼고 한 번 더 물어본다.
    /// → 잠긴 칸 제외·필터·이번 레이드 획득 등 다른 조건은 게임 판단을 그대로 따른다.
    /// 검색어가 '#'으로 시작하면 이름과 맞는 아이템이 없으므로 자연히 "태그만 검색"이 된다.
    /// </summary>
    internal static class TagSearch
    {
        private const string TagOnlyPrefix = "#";

        private static Type _window;
        private static MethodInfo _checkItem;      // StashSearchWindow.CheckItem(Item, bool)
        private static FieldInfo _textSearch;      // StashSearchWindow._textSearch (소문자로 바뀐 검색어)
        private static PropertyInfo _updateNeeded; // PaginationWindow.ItemsUpdateRequired

        [ThreadStatic] private static bool _reentry;
        private static bool _errorLogged;

        public static string Enable()
        {
            _window = Reflect.Type("EFT.UI.StashSearchWindow");
            _checkItem = Reflect.MethodByArity(_window, "CheckItem", 2);
            _textSearch = Reflect.Field(_window, "_textSearch");
            _updateNeeded = Reflect.Prop(_window, "ItemsUpdateRequired");

            string missing = Reflect.Missing(
                ("StashSearchWindow.CheckItem", _checkItem),
                ("StashSearchWindow._textSearch", _textSearch),
                ("TagComponent", TagAccess.Ready ? (object)true : null));
            if (missing.Length > 0)
            {
                return "멤버를 찾지 못함: " + missing;
            }

            Plugin.Harmony.Patch(_checkItem, postfix: new HarmonyMethod(typeof(TagSearch), nameof(CheckItemPostfix)));
            Settings.TagSearch.SettingChanged += (_, __) => RefreshOpenWindows();
            return null;
        }

        public static void CheckItemPostfix(object __instance, object item, bool useFilter, ref bool __result)
        {
            if (__result || _reentry || !Settings.TagSearch.Value)
            {
                return;
            }

            try
            {
                string search = _textSearch.GetValue(__instance) as string;
                if (string.IsNullOrEmpty(search))
                {
                    return; // 검색어가 없으면 다른 이유로 빠진 것 — 손대지 않음
                }

                string needle = search.StartsWith(TagOnlyPrefix, StringComparison.Ordinal)
                    ? search.Substring(TagOnlyPrefix.Length).Trim()
                    : search.Trim();
                string tag = TagAccess.GetTagName(item);
                if (needle.Length == 0 || tag == null || tag.ToLowerInvariant().IndexOf(needle, StringComparison.Ordinal) < 0)
                {
                    return;
                }

                // 검색어 조건만 잠시 빼고 게임에게 다시 판단시킨다(나머지 조건은 그대로).
                _reentry = true;
                _textSearch.SetValue(__instance, null);
                try
                {
                    __result = (bool)_checkItem.Invoke(__instance, new[] { item, useFilter });
                    if (Dbg.Limit("태그 검색", 60))
                    {
                        Dbg.Log("태그 검색", $"검색어='{search}' 태그='{tag}' → {(__result ? "결과에 포함" : "다른 조건(잠김·필터 등)으로 제외")}");
                    }
                }
                finally
                {
                    _textSearch.SetValue(__instance, search);
                    _reentry = false;
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("[태그 검색] 오류(이후 생략): " + e);
                }
            }
        }

        /// <summary>검색 창이 열린 채로 설정을 바꾸면 결과를 다시 계산하게 한다.</summary>
        private static void RefreshOpenWindows()
        {
            if (_updateNeeded == null || !_updateNeeded.CanWrite)
            {
                return;
            }

            foreach (UnityEngine.Object window in UnityEngine.Object.FindObjectsOfType(_window))
            {
                try
                {
                    _updateNeeded.SetValue(window, true, null);
                }
                catch (Exception e)
                {
                    Dbg.Log("태그 검색", "검색 창 새로고침 실패(무시): " + e.Message);
                }
            }
        }
    }
}
