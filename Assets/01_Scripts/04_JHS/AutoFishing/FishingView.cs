using UnityEngine;
using RMS.Data;   // FishData

namespace JHS.Fishing
{
    // 자동 낚시 "연출" 전담. 컨트롤러의 단계 신호(OnPhaseChanged)와 포획 결과(OnFishCaught)를
    // 구독해서 캐릭터 애니메이션/이펙트로 번역한다. 낚시 로직은 이 클래스를 전혀 모른다(연출 분리).
    //
    // 지금 단계: Animator 없이도 콘솔에 단계가 순서대로 찍히는지 확인용 골격.
    // 나중에: 인스펙터에 Animator만 꽂으면 단계 이름 그대로 SetTrigger 발동.
    //   → Animator Controller에 FishingPhase 이름과 똑같은 Trigger 파라미터를 만들면 된다
    //     (Idle/Casting/Waiting/Bite/Reeling/Success/Fail). 클립 준비/그래프 연결은 에디터 작업.
    public class FishingView : MonoBehaviour
    {
        [SerializeField] private AutoFishingController _controller;  // 비우면 같은 오브젝트→씬에서 자동 탐색
        [SerializeField] private Animator _animator;                 // 선택: 없으면 로그만, 있으면 SetTrigger
        [SerializeField] private bool _logToConsole = true;          // 골격 확인용 콘솔 출력

        private void OnEnable()
        {
            if (_controller == null) _controller = GetComponent<AutoFishingController>();
            if (_controller == null) _controller = FindFirstObjectByType<AutoFishingController>();
            if (_controller == null) return;

            _controller.OnPhaseChanged += HandlePhase;
            _controller.OnFishCaught   += HandleFishCaught;
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            _controller.OnPhaseChanged -= HandlePhase;
            _controller.OnFishCaught   -= HandleFishCaught;
        }

        // 단계 → 애니메이션. Trigger 이름은 enum 이름과 1:1 (phase.ToString()).
        private void HandlePhase(FishingPhase phase)
        {
            if (_logToConsole) Debug.Log($"[FishingView] 단계 ▶ {phase}");
            if (_animator != null) _animator.SetTrigger(phase.ToString());
        }

        // 포획 결과 — 잡힌 물고기 정보(등급 등)를 받는다. 등급별 파티클 분기는 여기에 붙이면 됨.
        private void HandleFishCaught(FishData fish, float contribution, bool isManual)
        {
            if (_logToConsole)
                Debug.Log($"[FishingView] 포획 연출 ({(isManual ? "수동" : "자동")}) : " +
                          $"{(fish != null ? $"{fish.DisplayName}/{fish.Rarity}" : "물고기 미정")}");
            // TODO: fish.Rarity 보고 등급별 파티클/사운드 재생 (아트 들어오면)
        }
    }
}
