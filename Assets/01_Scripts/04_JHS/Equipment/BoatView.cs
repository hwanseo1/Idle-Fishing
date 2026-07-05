using System;
using UnityEngine;
using UnityEngine.UI;   // Image (UI로 표시할 때만)

namespace JHS.Equipment
{
    // 메인화면 "장착한 배" 비주얼 동기화 전담. EquipmentManager.OnBoatEquipped를 구독해
    // 장착 배가 바뀌면 그 배 스프라이트로 교체한다. 매니저/컨트롤러는 이 클래스를 모름(연출 분리).
    //
    // 지금 단계: 스프라이트 없어도 콘솔 로그로 장착 동기화가 도는지 확인하는 골격.
    // 나중: 배별 스프라이트 배정 + SpriteRenderer/Image 연결 → 자동 스왑. "슝" 등장 연출은 그 위에 얹음.
    //   메인화면 부착·배치는 양원준(YWJ). (낚시 FishingView와 동일 사상 — 포트폴리오 TP-10)
    public class BoatView : MonoBehaviour
    {
        [Serializable]
        public struct BoatSprite
        {
            public BoatSkill boat;        // 어느 배(스킬)
            public Sprite sprite;         // 그 배의 메인화면 스프라이트
            public Vector3 visualOffset;  // 배별 BoatVisual 위치 보정(펭귄=0 기준). 스프라이트 크기차로 내려가는 배를 올려 정렬.
        }

        [SerializeField] private EquipmentManager _manager;       // 비우면 Instance 사용

        [Header("표시 대상 (쓰는 것만 연결, 둘 다 선택)")]
        [SerializeField] private SpriteRenderer _spriteRenderer;  // 월드 스프라이트로 표시할 때
        [SerializeField] private Image _image;                    // UI 이미지로 표시할 때

        [Header("배별 스프라이트 (아트 들어오면 배정)")]
        [SerializeField] private BoatSprite[] _sprites;
        [SerializeField] private bool _logToConsole = true;       // 골격 확인용

        private EquipmentManager Manager => _manager != null ? _manager : (_manager = EquipmentManager.Instance);
        private bool _subscribed;
        private Vector3 _baseBoatPos;   // Boat 루트 초기 위치(배별 visualOffset의 기준)

        private void Awake() => _baseBoatPos = transform.localPosition;

        // OnEnable(재활성)·Start(첫 로드 시 매니저 Awake 이후 보장) 양쪽에서 구독 시도 → 초기화 순서 무관.
        private void OnEnable() => EnsureSubscribed();
        private void Start() => EnsureSubscribed();

        private void OnDisable()
        {
            if (_subscribed && Manager != null) Manager.OnBoatEquipped -= HandleEquipped;
            _subscribed = false;
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            var mgr = Manager;
            if (mgr == null) return;   // 매니저 아직 없음 — 다음 진입(Start/재활성)에서 재시도

            mgr.OnBoatEquipped += HandleEquipped;
            _subscribed = true;

            // 구독 전에 이미 장착된 상태를 한 번 반영(켜질 때 현재 장착 배로 즉시 동기화).
            if (mgr.HasEquippedBoat) HandleEquipped(mgr.EquippedBoat);
        }

        private void HandleEquipped(BoatSkill boat)
        {
            if (_logToConsole) Debug.Log($"[BoatView] 장착 배 동기화 ▶ {boat}");

            if (_sprites == null) return;
            bool found = false;
            BoatSprite entry = default;
            foreach (var e in _sprites)
                if (e.boat == boat) { entry = e; found = true; break; }
            if (!found) return;

            if (entry.sprite != null)
            {
                if (_spriteRenderer != null) _spriteRenderer.sprite = entry.sprite;
                if (_image != null) { _image.sprite = entry.sprite; _image.enabled = true; }
            }

            // 배별 위치 보정: 스프라이트 크기(PPU) 차로 내려가는 배를 올려 정렬.
            // ※ BoatVisual은 Animator(BoatFloat 클립)가 localPosition을 매 프레임 덮으므로 보정이 무효.
            //   → 보정은 Boat 루트(this)에 적용한다. Boat를 올리면 배 스프라이트+선원이 한 덩어리로 올라가고(정합 유지),
            //     둥둥 애니는 BoatVisual local에서 그 위로 동작한다. (BoatFloatingMotion은 현재 비활성)
            transform.localPosition = _baseBoatPos + entry.visualOffset;
            // TODO: 배 교체 "슝" 등장 연출은 여기서 트윈으로 (스프라이트 자리잡은 뒤). 로직/장착 상태는 모른 채.
        }
    }
}
