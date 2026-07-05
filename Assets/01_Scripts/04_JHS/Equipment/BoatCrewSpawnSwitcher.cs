using System;
using UnityEngine;

namespace JHS.Equipment
{
    // 배는 스프라이트 1개만 스왑되는데(BoatView) 선원 자리(CrewSpawnPoint_1~4)는 한 세트뿐 →
    // 배 모양이 다르면 선원이 공중에 뜨거나 배 밖에 선다. 이를 해결하는 "연출 전용" 어댑터.
    //
    //  배 교체(OnBoatEquipped)·메인 진입(OnMainStageStateEntered) 시 → "현재 배 레이아웃"의 4자리 위치를
    //  CrewSpawnPoint_1~4에 복사하고 선원을 재스폰한다.
    //  ※ 원준 MainStageCrewManager는 무수정 — 그대로 CrewSpawnPoint를 참조해 스폰하고, 우리는 그 위치만 갱신 + 재스폰 트리거.
    //  ※ 배별 자리(_layouts의 points)는 BoatVisual 아래 빈 마커로 두고 Scene 뷰에서 배치한다(배별 미세조정 쉬움).
    public class BoatCrewSpawnSwitcher : MonoBehaviour
    {
        [Serializable]
        public struct BoatLayout
        {
            public BoatSkill boat;      // 어느 배
            public Transform[] points;  // 이 배의 선원 4자리(마커 Transform)
        }

        [Header("연동 (비우면 자동)")]
        [SerializeField] private EquipmentManager _manager;          // 비우면 Instance
        [SerializeField] private MainStageCrewManager _crewManager;  // 재스폰 호출 대상

        [Header("위치 갱신 대상 (원준 CrewSpawnPoint_1~4)")]
        [SerializeField] private Transform[] _crewSpawnPoints;

        [Header("배별 선원 자리 레이아웃 (마커는 Scene에서 배치)")]
        [SerializeField] private BoatLayout[] _layouts;

        private EquipmentManager Manager => _manager != null ? _manager : (_manager = EquipmentManager.Instance);
        private bool _subscribed;

        // OnEnable(재활성)·Start(첫 로드 시 매니저 Awake 이후 보장) 양쪽에서 구독 시도 → 초기화 순서 무관.
        // ⚠️ 과거엔 OnEnable에서 한 번만 구독해서, 멀티 복귀 등 씬 리로드 타이밍에 그 순간 Manager(=Instance)가
        //    아직 null이면 OnBoatEquipped 구독을 놓치고 재시도가 없었다. 그 결과 메인진입 시엔 기본 배(vacuum)
        //    레이아웃만 깔리고, 이후 서버 스냅샷이 실제 배(펭귄 등)로 OnBoatEquipped를 쏴도 못 받아 선원 자리가
        //    엉뚱하게 고착됐다(배를 다시 바꾸면 되돌아옴). BoatView와 동일한 재시도 구독으로 해소.
        private void OnEnable()
        {
            RuntimeStateEventBus.OnMainStageStateEntered += HandleMainEntered;
            EnsureSubscribed();
        }

        private void Start() => EnsureSubscribed();

        private void OnDisable()
        {
            RuntimeStateEventBus.OnMainStageStateEntered -= HandleMainEntered;
            if (_subscribed && Manager != null) Manager.OnBoatEquipped -= HandleBoatEquipped;
            _subscribed = false;
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            var mgr = Manager;
            if (mgr == null) return;   // 매니저 아직 없음 — 다음 진입(Start/재활성)에서 재시도
            mgr.OnBoatEquipped += HandleBoatEquipped;
            _subscribed = true;
            // ※ 여기서 즉시 HandleBoatEquipped를 부르지 않는다 — HandleBoatEquipped는 원준 크루매니저의
            //   SpawnEquippedCrewOnShip()까지 호출하는데, 구독 시점(OnEnable)엔 크루매니저가 아직 준비 전이라 NRE 난다.
            //   실제 적용은 (a)메인진입 HandleMainEntered (b)서버 스냅샷 복원 시 OnBoatEquipped 이벤트가 담당한다
            //   — 재시도 구독으로 그 이벤트를 놓치지 않는 것이 이 수정의 핵심.
        }

        // 배를 바꾸면 그 배 자리로 옮기고 즉시 재스폰.
        private void HandleBoatEquipped(BoatSkill boat)
        {
            ApplyLayout(boat);
            if (_crewManager != null) _crewManager.SpawnEquippedCrewOnShip();
        }

        // 메인 진입 시 현재 장착 배 레이아웃을 적용하고 재스폰.
        // (매니저도 같은 이벤트로 스폰하지만, 실행 순서와 무관하게 새 위치로 한 번 더 재스폰해 결과를 보장)
        private void HandleMainEntered()
        {
            if (Manager == null || !Manager.HasEquippedBoat) return;
            ApplyLayout(Manager.EquippedBoat);
            if (_crewManager != null) _crewManager.SpawnEquippedCrewOnShip();
        }

        // 해당 배 레이아웃의 4자리(마커) 위치를 CrewSpawnPoint들에 복사.
        private void ApplyLayout(BoatSkill boat)
        {
            if (_crewSpawnPoints == null || _layouts == null) return;

            Transform[] pts = null;
            foreach (var l in _layouts)
                if (l.boat == boat) { pts = l.points; break; }
            if (pts == null) return;   // 이 배 레이아웃 미설정 → 현재 자리 유지

            int n = Mathf.Min(_crewSpawnPoints.Length, pts.Length);
            for (int i = 0; i < n; i++)
                if (_crewSpawnPoints[i] != null && pts[i] != null)
                    _crewSpawnPoints[i].position = pts[i].position;
        }
    }
}
