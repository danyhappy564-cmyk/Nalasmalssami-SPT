using System;
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
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

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

            if (Compat.TagOverNameInstalled)
            {
                Log.LogWarning("원본 'TagOverName' 모드가 설치되어 있어 나랏말싸미의 '태그를 이름 위에' 기능은 끕니다(원본이 처리).");
            }
            else
            {
                TryEnable("태그를 이름 위에", TagOverName.Enable);
            }

            if (Compat.MoreTagColoursInstalled)
            {
                Log.LogWarning("원본 'MoreTagColours' 모드가 설치되어 있어 나랏말싸미의 '태그 색상 확장' 기능은 끕니다(원본이 처리).");
            }
            else
            {
                TryEnable("태그 색상 확장", TagColours.Enable);
            }

            Log.LogInfo($"나랏말싸미 {Version} 로드 완료.");
        }

        private void Update()
        {
            HangulInput.Tick();
            GridRefresh.Tick();
        }

        private static void TryEnable(string name, Func<string> enable)
        {
            try
            {
                string error = enable();
                if (error != null)
                {
                    Log.LogError($"[{name}] 꺼짐: {error}");
                }
            }
            catch (Exception e)
            {
                Log.LogError($"[{name}] 켜는 중 오류 — 이 기능만 꺼집니다: {e}");
            }
        }
    }
}
