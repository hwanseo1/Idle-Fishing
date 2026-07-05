using UnityEngine;

namespace JHS.Content
{
    // 자체 부트스트랩(트랙 A↔B 글루): 백엔드가 준비되면 서버 dataVersion을 읽어 콘텐츠 카탈로그 갱신을 1회 트리거.
    // ContentCatalogUpdater는 백엔드를 직접 모르고(인자 주입), 이 글루만 BackendService에 의존 = 결합을 한 곳에 격리(DIP).
    // ※ 실제 게임 부팅순서(YWJ RSM) 배치는 팀 합의(③단계). 지금은 JHS 솔로 검증용 자체 실행.
    public sealed class ContentUpdateBootstrap : MonoBehaviour
    {
        // ⚠️ 자동 실행 비활성화(2026-06-24): 콘텐츠 패치 트리거를 로그인 흐름
        // (TransitionAfterLoginController.CheckNextStateAsync)으로 이관함 — "게이트 → 패치 → 오프라인 정산"
        // 순서 보장을 위해. 여기서 자체 Start로 또 돌리면 이중 실행/순서 어긋남이라 끔.
        // (씬에 이 컴포넌트가 남아 있어도 무해하도록 Start를 빈 동작으로 둔다.)
        private void Start()
        {
            // intentionally empty — 콘텐츠 동기는 로그인 후·정산 전에 CheckNextStateAsync가 호출함.
        }

#if UNITY_EDITOR
        // 에디터 테스트용 — 저장된 로컬 dataVersion을 비운다(다음 실행 = "첫 실행(없음)" 경로 재현).
        [ContextMenu("콘텐츠 버전 비우기 (ContentCatalogUpdater.Clear)")]
        private void Editor_ClearVersion()
        {
            ContentCatalogUpdater.Clear();
            Debug.Log("[ContentUpdate] 저장된 dataVersion 비움 — 다음 실행 시 변경 감지 경로로 진입.");
        }
#endif
    }
}
