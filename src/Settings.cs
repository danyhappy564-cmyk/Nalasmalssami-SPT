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
        private const string SecStyle = "4. 태그 글자 모양";
        private const string SecPresets = "5. 태그 프리셋";
        private const string SecSearch = "6. 태그 검색";

        public const string PresetBelow = "창 아래";
        public const string PresetAbove = "창 위";

        public const string CharsetHangul = "한글";
        public const string CharsetCjk = "한글 + 일본어·한자";
        public const string CharsetAll = "모든 문자";

        // 0. 정보
        public static ConfigEntry<bool> DebugLog;
        public static ConfigEntry<bool> DumpState;

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

        // 4. 태그 글자 모양
        public static ConfigEntry<bool> AutoContrast;
        public static ConfigEntry<float> ContrastThreshold;
        public static ConfigEntry<float> TagFontScale;
        public static ConfigEntry<bool> TagBold;

        // 5. 태그 프리셋
        public static ConfigEntry<bool> PresetsEnabled;
        public static ConfigEntry<string> Presets;
        public static ConfigEntry<bool> PresetInstantSave;
        public static ConfigEntry<string> PresetPosition;

        // 6. 태그 검색
        public static ConfigEntry<bool> TagSearch;

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

        /// <summary>상태 기록(DebugDump)에서 모든 설정값을 나열할 때 쓴다.</summary>
        internal static ConfigFile File;

        public static void Init(ConfigFile config)
        {
            File = config;
            // 0. 정보 — 원본 모드 감지 결과(읽기 전용, 매 실행 갱신)
            ConfigEntry<string> status = config.Bind(SecInfo, "Original mods", "",
                new ConfigDescription(
                    "원본 모드(TagOverName / MoreTagColours)가 같이 설치되어 있으면 겹치는 기능은 원본에 맡기고 나랏말싸미 쪽은 자동으로 끕니다.",
                    null, new ConfigurationManagerAttributes { DispName = "원본 모드 감지", Order = _order--, ReadOnly = true }));
            status.Value = Compat.Summary();
            DebugLog = config.Bind(SecInfo, "Debug log", false,
                Desc("켜면 태그 창 열기·글자 입력(조합 중 글자 포함)·저장·글꼴·프리셋 버튼 위치·태그 검색·글자 모양 판단을 " +
                     "BepInEx/LogOutput.log에 [디버그] 줄로 자세히 남깁니다. 문제 제보할 때만 켜고, 끝나면 꺼 주세요.",
                    "디버그 로그"));
            DumpState = config.Bind(SecInfo, "Dump state now", false,
                Desc("체크하면 지금 상태(기능별 켜짐/실패, 설정값, 입력기 상태, 열린 창 등)를 LogOutput.log에 한 번 적고 자동으로 꺼집니다. " +
                     "디버그 로그가 꺼져 있어도 동작합니다.",
                    "지금 상태 기록 (1회)"));
            DumpState.Value = false; // 지난 실행에서 체크된 채 남았어도 항상 꺼진 상태로 시작

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

            // 4. 태그 글자 모양
            AutoContrast = config.Bind(SecStyle, "Auto text contrast", true,
                Desc("태그 색 막대가 밝으면(노랑·밝은 회색 등) 글자를 검게 바꿔서 잘 보이게 합니다. 어두운 막대는 원래 흰 글씨 그대로.",
                    "태그 글자색 자동 대비"));
            ContrastThreshold = config.Bind(SecStyle, "Contrast threshold", 0.5f,
                Desc("막대 밝기가 이 값보다 크면 검은 글씨. 낮출수록 더 많은 색에서 검은 글씨가 됩니다.",
                    "검은 글씨 기준 밝기", new AcceptableValueRange<float>(0.2f, 0.9f)));
            TagFontScale = config.Bind(SecStyle, "Tag font scale", 1f,
                Desc("아이템 칸 태그 글자 크기 배율. 1 = 게임 기본. 너무 키우면 막대 위아래로 넘칠 수 있습니다.",
                    "태그 글자 크기", new AcceptableValueRange<float>(0.7f, 1.5f)));
            TagBold = config.Bind(SecStyle, "Tag bold", false,
                Desc("아이템 칸 태그 글자를 굵게 합니다. 작은 칸에서 한글이 뭉개질 때 도움이 됩니다.", "태그 글자 굵게"));

            // 5. 태그 프리셋
            PresetsEnabled = config.Bind(SecPresets, "Enable presets", true,
                Desc("태그 창에 자주 쓰는 태그 버튼을 띄웁니다. 누르면 그 글자(와 색)가 바로 들어갑니다.", "태그 프리셋 버튼"));
            Presets = config.Bind(SecPresets, "Preset list", "팔 것=1, 퀘템=3, 보관=6, 은신처=4, 수리=2",
                Desc("쉼표(,)로 구분. '글자=색번호'로 쓰면 색도 같이 고릅니다(색번호는 F12의 '색 01'=1). 색번호를 빼면 글자만.\n" +
                     "최대 10개. 예: 팔 것=1, 퀘템=3, 보관",
                    "프리셋 목록"));
            PresetInstantSave = config.Bind(SecPresets, "Save on click", false,
                Desc("프리셋을 누르면 저장까지 한 번에 하고 창을 닫습니다.", "누르면 바로 저장"));
            PresetPosition = config.Bind(SecPresets, "Position", PresetBelow,
                Desc("프리셋 버튼 줄을 태그 창 아래/위 중 어디에 붙일지. 화면 밖으로 나가면 바꿔 보세요.", "버튼 위치",
                    new AcceptableValueList<string>(PresetBelow, PresetAbove)));

            // 6. 태그 검색
            TagSearch = config.Bind(SecSearch, "Search by tag", true,
                Desc("창고 검색(돋보기) 창에서 아이템 이름뿐 아니라 태그 글자로도 찾습니다.\n" +
                     "앞에 #을 붙이면 태그만 검색합니다(예: #팔).",
                    "태그로 창고 검색"));
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
                    case ConfigEntry<float> f: f.SettingChanged += (_, __) => handler(); break;
                    case ConfigEntry<string> s: s.SettingChanged += (_, __) => handler(); break;
                    case ConfigEntry<Color> c: c.SettingChanged += (_, __) => handler(); break;
                }
            }
        }
    }
}
