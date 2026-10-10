using System;

namespace Nalasmalssami
{
    /// <summary>
    /// 원본 모드 동시 설치 감지. 같은 게임 메서드를 둘이 동시에 고치면
    /// (예: 색상표를 서로 덮어쓰기) 어느 쪽이 이길지 모르므로, 원본이 있으면 그 기능은 원본에 양보한다.
    /// 한글 입력·글꼴 보강은 원본 모드에 없는 기능이라 항상 켠다.
    /// </summary>
    internal static class Compat
    {
        public const string TagOverNameGuid = "com.falconpilot.tagovername";
        public const string MoreTagColoursGuid = "com.acidphantasm.moretagcolours";

        public static bool TagOverNameInstalled { get; private set; }
        public static bool MoreTagColoursInstalled { get; private set; }

        public static void Detect(Func<string, bool> isLoaded)
        {
            TagOverNameInstalled = isLoaded(TagOverNameGuid);
            MoreTagColoursInstalled = isLoaded(MoreTagColoursGuid);
        }

        public static string Summary()
        {
            string a = TagOverNameInstalled ? "TagOverName 감지(이름 숨기기는 원본이 처리)" : "TagOverName 없음";
            string b = MoreTagColoursInstalled ? "MoreTagColours 감지(색상 확장은 원본이 처리)" : "MoreTagColours 없음";
            return a + " / " + b;
        }
    }
}
