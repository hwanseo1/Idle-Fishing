using UnityEngine;
using RMS.Data;   // FishData

namespace JHS.Fishing
{
    // 자동 낚시 결과를 Play 모드 화면에 표시하는 디버그용. (실제 UI/기여도 시스템 붙으면 제거)
    public class FishingDebugHUD : MonoBehaviour
    {
        [SerializeField] private AutoFishingController _controller;

        private int _autoCount;
        private int _manualCount;
        private float _totalContribution;
        private string _lastFish = "-";

        private void OnEnable()
        {
            if (_controller == null) _controller = GetComponent<AutoFishingController>();
            if (_controller != null) _controller.OnFishCaught += OnFishCaught;
        }

        private void OnDisable()
        {
            if (_controller != null) _controller.OnFishCaught -= OnFishCaught;
        }

        private void OnFishCaught(FishData fish, float contribution, bool isManual)
        {
            if (isManual) _manualCount++; else _autoCount++;
            _totalContribution += contribution;
            _lastFish = fish != null ? $"{fish.DisplayName} ({fish.Rarity})" : "미정(스폰매니저 연결 전)";
        }

        private void OnGUI()
        {
            const int w = 280, h = 150;
            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.box);
            GUILayout.Label("자동 낚시 디버그");
            GUILayout.Label($"누적 기여도 : {_totalContribution:0.##}");
            GUILayout.Label($"자동 포획 : {_autoCount}   수동 포획 : {_manualCount}");
            GUILayout.Label($"최근 물고기 : {_lastFish}");

            if (_controller != null && GUILayout.Button("입질 때 상호작용 (수동 분기)"))
                _controller.RequestInteraction();

            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        // 개발용: 인스펙터에서 이 컴포넌트 우클릭 → 메뉴로 즉시 보스전 진입(사운드/보스 연출 테스트).
        // 플레이 중 메인스테이지에서 실행. RMS 코드 무수정(공개 StartBossStage + 리플렉션으로 현재스테이지 지정).
        [ContextMenu("▶ 보스전으로 점프 (1-boss)")]  private void DebugJumpBoss1() => DebugJumpBoss("Assets/03_Data/01_RMS/StageData/1_Stage/1-boss.asset");
        [ContextMenu("▶ 보스전으로 점프 (2-boss)")]  private void DebugJumpBoss2() => DebugJumpBoss("Assets/03_Data/01_RMS/StageData/2_Stage/2-boss.asset");
        [ContextMenu("▶ 보스전으로 점프 (3-boss)")]  private void DebugJumpBoss3() => DebugJumpBoss("Assets/03_Data/01_RMS/StageData/3_Stage/3-boss.asset");

        private void DebugJumpBoss(string bossAssetPath)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[FishingDebugHUD] 플레이 중에 실행하세요."); return; }

            var fsm = FindFirstObjectByType<RMS.Fishing.FishSpawnManager>(FindObjectsInactive.Include);
            if (fsm == null) { Debug.LogWarning("[FishingDebugHUD] FishSpawnManager 없음(메인스테이지에서 실행)."); return; }

            var boss = UnityEditor.AssetDatabase.LoadAssetAtPath<StageData>(bossAssetPath);
            if (boss == null) { Debug.LogWarning($"[FishingDebugHUD] 보스 SO 로드 실패: {bossAssetPath}"); return; }

            var T = typeof(RMS.Fishing.FishSpawnManager);
            const System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            T.GetField("_currentStage", NP)?.SetValue(fsm, boss);
            (T.GetField("OnStageChanged", NP)?.GetValue(fsm) as System.Delegate)?.DynamicInvoke(boss);  // 구독자(BGM/UI) 갱신
            fsm.StartBossStage();

            Debug.Log($"[FishingDebugHUD] 보스전 점프 → {(fsm.CurrentBossData != null ? fsm.CurrentBossData.DisplayName : "?")} (자동낚시 타격 시 소리)");
        }
#endif
    }
}
