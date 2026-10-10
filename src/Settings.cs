using System;
using BepInEx.Configuration;
using UnityEngine;

namespace Nalasmalssami
{
    /// <summary>
    /// F12(ConfigurationManager) 설정. 값을 바꾸면 SettingChanged로 바로 게임에 다시 적용된다.
    /// </summary>
    internal static class Settings
    {
        private const string SecInfo = "0. 정보";
        private const string SecHangul = "1. 한글 입력";
        private const string SecTagName = "2. 태그를 이름 위에";
        private const string SecColours = "3. 태그 색상 확장";

        public const string CharsetHangul = "한글";
        public const string CharsetCjk = "한글 + 일본어·한자";
        public const string CharsetAll = "모든 문자";

        // 1. 한글 입력
        public static ConfigEntry<bool> HangulEnabled;
        public static ConfigEntry<string> Charset;
        public static ConfigEntry<int> MaxLength;
        public static ConfigEntry<bool> ForceIme;
        public static ConfigEntry<bool> CommitComposition;
        public static ConfigEntry<bool> FontFallback;

        // 2. 태그를 이름 위에
        public static ConfigEntry<bool> HideNameWhenTagged;

        // 3. 태그 색상 확장
        public static ConfigEntry<bool> ExtraColours;
        public static ConfigEntry<bool> IncludeGreys;
        public static ConfigEntry<bool> BiggerWindow;
        public static readonly ConfigEntry<Color>[] Colours = new ConfigEntry<Color>[18];

        // acidphantasm-moretagcolours 기본값과 같은 순서·같은 색 — 두 모드를 바꿔 써도 태그 색 번호가 같은 색으로 보인다.
        internal static readonly Color[] DefaultColours =
        {
            new Color(0.85f, 0f, 0f),
            new Color(0.85f, 0.5f, 0f),
            new Color(0.85f, 0.85f, 0f),
            new Color(0.224f, 0.85f, 0f),
            new Color(0f, 0.85f, 0.759f),
            new Color(0f, 0.529f, 0.85f),
            new Color(0.102f, 0f, 0.85f),
            new Color(0.463f, 0f, 0.85f),
            new Color(0.85f, 0f, 0.549f),
            new Color(0.349f, 0.122f, 0.122f),
            new Color(0.18f, 0.106f, 0.024f),
            new Color(0.349f, 0.267f, 0.122f),
            new Color(0.106f, 0.329f, 0.067f),
            new Color(0.122f, 0.271f, 0.247f),
            new Color(0.071f, 0.22f, 0.349f),
            new Color(0.067f, 0.047f, 0.251f),
            new Color(0.165f, 0.051f, 0.302f),
            new Color(0.278f, 0.047f, 0.176f),
        };

        private static int _order = 1000;

        public static void Init(ConfigFile config)
        {
            // 0. 정보 — 원본 모드 감지 결과(읽기 전용, 매 실행 갱신)
            ConfigEntry<string> status = config.Bind(SecInfo, "Original mods", "",
                new ConfigDescription(
                    "원본 모드(TagOverName / MoreTagColours)가 같이 설치되어 있으면 겹치는 기능은 원본에 맡기고 나랏말싸미 쪽은 자동으로 끕니다.",
                    null, new ConfigurationManagerAttributes { DispName = "원본 모드 감지", Order = _order--, ReadOnly = true }));
            status.Value = Compat.Summary();

            // 1. 한글 입력
            HangulEnabled = config.Bind(SecHangul, "Allow Hangul", true,
                Desc("태그 입력 칸에서 한글이 지워지지 않게 합니다. 끄면 게임 기본(영문·숫자 위주)으로 돌아갑니다.",
                    "한글 태그 입력 허용"));
            Charset = config.Bind(SecHangul, "Allowed characters", CharsetHangul,
                Desc("게임 기본 허용 문자에 더해 무엇을 허용할지.\n" +
                     "· 한글: 한글(자모 포함)만 추가\n" +
                     "· 한글 + 일본어·한자: 가나·한자·전각 기호도 추가\n" +
                     "· 모든 문자: 줄바꿈 같은 제어 문자만 빼고 전부 허용(글꼴에 없는 이모지는 네모로 보일 수 있음)",
                    "허용 문자 범위", new AcceptableValueList<string>(CharsetHangul, CharsetCjk, CharsetAll)));
            MaxLength = config.Bind(SecHangul, "Max length", 0,
                Desc("태그 최대 글자 수. 0 = 게임 기본값 그대로.", "최대 글자 수 (0 = 게임 기본)",
                    new AcceptableValueRange<int>(0, 64)));
            ForceIme = config.Bind(SecHangul, "Force IME", true,
                Desc("태그 입력 칸에 커서가 있는 동안 한/영 입력기(IME) 조합을 강제로 켭니다. 한/영 키가 안 먹을 때 켜 두세요.",
                    "입력기(IME) 강제 켜기"));
            CommitComposition = config.Bind(SecHangul, "Keep composing char on save", true,
                Desc("마지막 글자를 조합하는 도중(밑줄 상태)에 저장을 눌러도 그 글자가 사라지지 않게 붙여서 저장합니다.",
                    "저장 시 조합 중인 글자 살리기"));
            FontFallback = config.Bind(SecHangul, "Hangul font fallback", true,
                Desc("한글이 네모(□)로 보이면, 게임에 들어 있는 한글 글꼴(없으면 윈도우 '맑은 고딕')을 예비 글꼴로 붙입니다.",
                    "한글 글꼴 보강"));

            // 2. 태그를 이름 위에 (FalconPilot/spt-tag-over-name)
            HideNameWhenTagged = config.Bind(SecTagName, "Hide item name when tagged", true,
                Desc("태그가 있는 아이템은 칸 안의 짧은 이름을 숨겨서, 태그 글자가 잘리지 않고 다 보이게 합니다. 바로 적용됩니다.",
                    "태그 있으면 아이템 이름 숨기기", readOnly: Compat.TagOverNameInstalled));

            // 3. 태그 색상 확장 (acidphantasm/acidphantasm-moretagcolours)
            bool colourLocked = Compat.MoreTagColoursInstalled;
            ExtraColours = config.Bind(SecColours, "Extra tag colours", true,
                Desc("태그 색을 7개 → 18개(+회색 9단계)로 늘립니다.\n" +
                     "끄면 게임 기본 7색으로 돌아가며, 8번 이후 색으로 단 태그는 1번 색으로 보입니다(태그 자체는 남아 있음).",
                    "태그 색상 확장 사용", readOnly: colourLocked));
            IncludeGreys = config.Bind(SecColours, "Include greys", true,
                Desc("색 선택 창에 회색 9단계를 함께 보여 줍니다.", "회색 9단계 포함", readOnly: colourLocked));
            BiggerWindow = config.Bind(SecColours, "Bigger tag window", true,
                Desc("색이 많아져도 한눈에 보이게 태그 창을 키웁니다(450×260).", "태그 창 크게", readOnly: colourLocked));
            for (int i = 0; i < Colours.Length; i++)
            {
                Colours[i] = config.Bind(SecColours, $"Colour {i + 1:00}", DefaultColours[i],
                    Desc($"{i + 1}번 태그 색.", $"색 {i + 1:00}", readOnly: colourLocked));
            }
        }

        private static ConfigDescription Desc(string description, string dispName,
            AcceptableValueBase range = null, bool readOnly = false)
        {
            if (readOnly)
            {
                dispName += " (원본 모드 사용 중 — 꺼짐)";
            }

            return new ConfigDescription(description, range, new ConfigurationManagerAttributes
            {
                DispName = dispName,
                Order = _order--,
                ReadOnly = readOnly ? true : (bool?)null,
            });
        }

        /// <summary>여러 설정에 같은 처리기를 붙인다.</summary>
        public static void OnChanged(Action handler, params ConfigEntryBase[] entries)
        {
            foreach (ConfigEntryBase entry in entries)
            {
                switch (entry)
                {
                    case ConfigEntry<bool> b: b.SettingChanged += (_, __) => handler(); break;
                    case ConfigEntry<int> n: n.SettingChanged += (_, __) => handler(); break;
                    case ConfigEntry<string> s: s.SettingChanged += (_, __) => handler(); break;
                    case ConfigEntry<Color> c: c.SettingChanged += (_, __) => handler(); break;
                }
            }
        }
    }
}
