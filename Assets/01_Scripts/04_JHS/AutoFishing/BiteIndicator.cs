using UnityEngine;

namespace JHS.Fishing
{
    // 입질(Bite) 단계 동안에만 지정한 UI 오브젝트를 켜는 프로토타입 표시기.
    // 낚시 로직(AutoFishingController)은 이 클래스를 전혀 모른다 — OnPhaseChanged 신호만 구독한다
    // (연출 분리, FishingView와 동일 패턴).
    //
    // 사용법: _indicator에 "입질!" UI(버튼)를 연결하고 평소엔 꺼둔다. Bite 단계가 오면 자동으로 켜지고,
    //   다음 단계(탭해서 Reeling / 시간초과 후 Success)로 넘어가면 자동으로 꺼진다.
    //   그 버튼의 OnClick을 AutoFishingController.RequestInteraction()에 연결하면
    //   "입질 표시 → 탭 → 수동 분기"가 완성된다.
    public class BiteIndicator : MonoBehaviour
    {
        [SerializeField] private AutoFishingController _controller;  // 비우면 같은 오브젝트→씬에서 자동 탐색
        [SerializeField] private GameObject _indicator;             // 입질 때 켤 UI (평소 비활성)

        private void OnEnable()
        {
            if (_controller == null) _controller = GetComponent<AutoFishingController>();
            if (_controller == null) _controller = FindFirstObjectByType<AutoFishingController>();

            if (_indicator != null) _indicator.SetActive(false);    // 시작은 꺼둔 상태로 강제
            if (_controller != null) _controller.OnPhaseChanged += HandlePhase;
        }

        private void OnDisable()
        {
            if (_controller != null) _controller.OnPhaseChanged -= HandlePhase;
            if (_indicator != null) _indicator.SetActive(false);    // 꺼질 때 표시도 정리
        }

        // 입질 단계에서만 표시. 다른 단계로 넘어가면(탭했거나 시간초과) 자동으로 꺼진다.
        private void HandlePhase(FishingPhase phase)
        {
            if (_indicator != null) _indicator.SetActive(phase == FishingPhase.Bite);
        }
    }
}
