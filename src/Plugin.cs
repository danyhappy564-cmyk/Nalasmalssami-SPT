using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Nalasmalssami.Features;

namespace Nalasmalssami
{
    /// <summary>
    /// 나랏말싸미 — 아이템 태그에 한글을 쓸 수 있게 해 주는 클라이언트 모드.
    /// + "태그를 이름 위에"(FalconPilot/spt-tag-over-name, MIT)
    /// + "태그 색상 확장"(acidphantasm/acidphantasm-moretagcolours, MIT) 기능을 함께 담았다.
    ///
    /// 게임 타입은 전부 이름(리플렉션)으로 찾는다 — 게임이 업데이트되어 이름이 바뀌면
    /// 해당 기능만 꺼지고 로그에 남는다(게임은 정상 동작).
    /// </summary>
    [BepInPlugin(Guid, "나랏말싸미 (Nalasmalssami)", Version)]
    // 원본 모드가 있으면 먼저 로드되게 해서, Awake에서 설치 여부를 확실히 알 수 있게 한다.
    [BepInDependency(Compat.TagOverNameGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Compat.MoreTagColoursGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.rf.nalasmalssami";
        public const string Version = "1.1.3";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        /// <summary>기능 이름 → "켜짐" 또는 실패 이유 (상태 기록용)</summary>
        internal static readonly Dictionary<string, string> FeatureStatus = new Dictionary<string, string>();

        private void Awake()
        {
            Log = Logger;
            Harmony = new Harmony(Guid);

            Compat.Detect(Chainloader.PluginInfos.ContainsKey);
            Settings.Init(Config);

            // 기능마다 따로 켠다 — 하나가 실패해도 나머지는 동작한다.
            TryEnable("한글 입력", HangulInput.Enable);
            TryEnable("한글 글꼴 보강", HangulFont.Enable);
            TryEnable("색 선택 창 안전장치", ColourPanelGuard.Enable);
            TryEnable("태그 글자 모양", TagStyle.Enable);
            TryEnable("태그 프리셋", PresetBar.Enable);
            TryEnable("태그 검색", TagSearch.Enable);

            if (Compat.TagOverNameInstalled)
            {
                Log.LogWarning("원본 'TagOverName' 모드가 설치되어 있어 나랏말싸미의 '태그를 이름 위에' 기능은 끕니다(원본이 처리).");
                FeatureStatus["태그를 이름 위에"] = "원본 모드에 양보";
            }
            else
            {
                TryEnable("태그를 이름 위에", TagOverName.Enable);
            }

            if (Compat.MoreTagColoursInstalled)
            {
                Log.LogWarning("원본 'MoreTagColours' 모드가 설치되어 있어 나랏말싸미의 '태그 색상 확장' 기능은 끕니다(원본이 처리).");
                FeatureStatus["태그 색상 확장"] = "원본 모드에 양보";
            }
            else
            {
                TryEnable("태그 색상 확장", TagColours.Enable);
            }

            Settings.DebugLog.SettingChanged += (_, __) =>
            {
                Util.Dbg.ResetLimits();
                Log.LogInfo(Settings.DebugLog.Value ? "[디버그] 디버그 로그 켜짐" : "[디버그] 디버그 로그 꺼짐");
            };
            Settings.DumpState.SettingChanged += (_, __) =>
            {
                // 1.1.1: 이 안에서 바로 false로 되돌리면 F12 창이 다시 true로 써서 기록이 여러 번 찍혔다 →
                // 다음 프레임(Update)에 한 번만 기록하고 끈다.
                if (Settings.DumpState.Value)
                {
                    _dumpPending = true;
                }
            };

            Log.LogInfo($"나랏말싸미 {Version} 로드 완료.");
            if (Settings.DebugLog.Value)
            {
                Log.LogWarning("[디버그] 디버그 로그가 켜져 있습니다(F12 → 0. 정보). 제보가 끝나면 꺼 주세요.");
            }
        }

        private static bool _dumpPending;

        private void Update()
        {
            if (_dumpPending)
            {
                _dumpPending = false;
                DebugDump.Write();
                Settings.DumpState.Value = false; // 1회용 스위치
            }

            HangulInput.Tick();
            PresetBar.Tick();
            GridRefresh.Tick();
            HangulFont.Tick();
        }

        private static void TryEnable(string name, Func<string> enable)
        {
            try
            {
                string error = enable();
                FeatureStatus[name] = error == null ? "켜짐" : "꺼짐 — " + error;
                if (error != null)
                {
                    Log.LogError($"[{name}] 꺼짐: {error}");
                }
            }
            catch (Exception e)
            {
                FeatureStatus[name] = "오류 — " + e.Message;
                Log.LogError($"[{name}] 켜는 중 오류 — 이 기능만 꺼집니다: {e}");
            }
        }
    }
}
