using System;
using System.Reflection;
using Nalasmalssami.Util;
using UnityEngine;

namespace Nalasmalssami.Features
{
    /// <summary>
    /// F12에서 설정을 바꾸면 지금 화면에 떠 있는 아이템 칸(GridItemView)을 다시 그려서 "바로 적용"되게 한다.
    /// 색 고르기처럼 값이 연달아 바뀌는 경우를 위해 0.2초 모았다가 한 번만 그린다.
    /// </summary>
    internal static class GridRefresh
    {
        private static Type _view;
        private static MethodInfo _updateItemName;
        private static MethodInfo _updateTag;
        private static bool _resolved;
        private static float _dueTime = -1f;

        public static void Request()
        {
            _dueTime = Time.realtimeSinceStartup + 0.2f;
        }

        public static void Tick()
        {
            if (_dueTime < 0f || Time.realtimeSinceStartup < _dueTime)
            {
                return;
            }

            _dueTime = -1f;
            RefreshAll();
        }

        /// <summary>상태 기록용</summary>
        internal static string DescribeState()
        {
            Type view = _view ?? Reflect.Type("EFT.UI.DragAndDrop.GridItemView");
            return view == null ? "GridItemView 타입 없음" : $"지금 화면의 아이템 칸 {UnityEngine.Object.FindObjectsOfType(view).Length}개";
        }

        private static void RefreshAll()
        {
            if (!_resolved)
            {
                _resolved = true;
                _view = Reflect.Type("EFT.UI.DragAndDrop.GridItemView");
                _updateItemName = Reflect.Method(_view, "UpdateItemName");
                _updateTag = Reflect.Method(_view, "UpdateTag");
            }

            if (_view == null || _updateItemName == null || _updateTag == null)
            {
                return;
            }

            int count = 0;
            foreach (UnityEngine.Object view in UnityEngine.Object.FindObjectsOfType(_view))
            {
                try
                {
                    // 이름을 먼저 원래대로 쓰고, 태그를 다시 그리면 "이름 숨기기"/색상 패치가 새 설정으로 다시 돈다.
                    _updateItemName.Invoke(view, null);
                    _updateTag.Invoke(view, null);
                    count++;
                }
                catch (Exception e)
                {
                    Dbg.Log("아이템 칸", "새로 그리기 실패(무시): " + e.Message);
                }
            }

            Dbg.Log("아이템 칸", $"설정 변경 반영: {count}개 새로 그림");
        }
    }
}
