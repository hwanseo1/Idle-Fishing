using System;
using System.Collections.Generic;
using UnityEngine;

namespace JHS.Equipment
{
    // 배 액티브 스킬: 자동 낚시 중 쿨타임마다 자동 발동. (쿨타임/지속/수치는 미확정 — SO 값 사용)
    // 발동 중인 버프를 외부(자동 낚시 등)가 읽어간다.
    // 스킬별 행동은 IBoatSkill 전략으로 분리 — 새 스킬 추가 시 이 클래스는 수정하지 않는다.
    public class BoatActiveSkillController : MonoBehaviour, IBoatSkillContext, IBoatRarityProvider
    {
        [SerializeField] private EquipmentManager _manager;

        public float CatchMultiplier { get; private set; } = 1f;  // 진공 흡입기 발동 중
        public float RarityBonus { get; private set; } = 0f;       // 야근 전등 발동 중
        public event Action<float> OnDecoyPenguin;                 // 펭귄 발동 (예정)

        // 발동 순간 연출 훅(표현 레이어 전용). (skill, value=현재 등급 수치, duration=지속시간·즉발은 0)  // BoatSkillFxView가 구독
        // 로직은 이 이벤트를 전혀 안 봄 — BoatSkillFxView가 구독해 월드 연출만 그린다(연출 분리, TP-10).
        public event Action<BoatSkill, float, float> OnSkillActivated;

        private class Runtime { public BoatSkillEntry cfg; public IBoatSkill behaviour; public float cd; public float active; }
        private readonly List<Runtime> _skills = new();
        private readonly BoatBuffState _buff = new();
        private float _retryTimer;
        private bool _warned;
        private bool _subscribed;
        private bool _gameplayActive = true;   // 기본 true (배 단독 테스트씬 대비). 컨트롤러가 메인스테이지 게이트로 갱신.

        // 즉시효과 통로 (IBoatSkillContext) — 전략이 컨트롤러 내부를 모르고도 발행 가능.
        void IBoatSkillContext.RaisePenguin(float value)
        {
            OnDecoyPenguin?.Invoke(value);   // 예정 — 다음 스테이지 물고기 지급
            Debug.Log("[Boat] 유인용 로봇 펭귄 발동 (예정)");
        }

        // 장착한 배 1척의 스킬만 적재. (3척 전부가 아니라 EquippedBoat 1개 — 장착 모델)
        // 매니저가 아직 없으면(초기화 순서) 다음 Update에서 재시도.
        private void TryLoad()
        {
            var mgr = _manager != null ? _manager : EquipmentManager.Instance;
            if (mgr == null) return;
            _manager = mgr;

            _skills.Clear();
            if (!mgr.HasEquippedBoat) return;   // 장착 가능한 배 없음 → 발동할 스킬 없음

            BoatSkill skill = mgr.EquippedBoat;
            var cfg = mgr.GetBoatSkillEntry(skill);
            if (cfg == null || !mgr.IsBoatUnlocked(skill)) return;   // 미존재/미해금 방어 (런타임 해금 상태 기준)

            var behaviour = BoatSkillRegistry.Get(skill);
            if (behaviour == null)
            {
                Debug.LogWarning($"[Boat] '{skill}' 행동 미등록 — BoatSkillRegistry.Build()에 추가 필요");
                return;
            }
            _skills.Add(new Runtime { cfg = cfg, behaviour = behaviour, cd = cfg.cooldown, active = 0f });
        }

        // 장착 구독 (매니저 준비된 시점에 1회). OnDisable에서 풀고, 재활성 시 Update가 다시 건다.
        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            var mgr = _manager != null ? _manager : EquipmentManager.Instance;
            if (mgr == null) return;
            _manager = mgr;
            mgr.OnBoatEquipped += OnBoatEquipped;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed && _manager != null) _manager.OnBoatEquipped -= OnBoatEquipped;
            _subscribed = false;

            // 비활성 동안 장착이 바뀌어도(구독이 풀려 알림을 못 받음) 재활성 시 현재 장착 배로
            // 새로 적재되도록 비워 둔다. 비활성 컨트롤러가 옛 버프를 계속 흘리지 않게 초기화도 함께.
            _skills.Clear();
            CatchMultiplier = 1f;
            RarityBonus = 0f;
        }

        // 장착 배가 바뀌면 현재 스킬을 비우고(이전 배 버프 즉시 해제) 다음 Update에서 새 배로 재적재.
        private void OnBoatEquipped(BoatSkill skill)
        {
            _skills.Clear();
            _retryTimer = 0f;
            _warned = false;
            CatchMultiplier = 1f;   // 이전 배 버프가 남지 않게 즉시 초기화
            RarityBonus = 0f;
        }

        // 자동낚시와 동일한 활성화 게이트. AutoFishingController.RefreshLoopState가 메인스테이지·보스 조건으로 호출.
        // 비활성이면 쿨타임·발동을 멈추고(메뉴에서 쿨다운 허비 방지) 진행 중 버프도 해제한다.
        public void SetGameplayActive(bool active)
        {
            _gameplayActive = active;
            if (!active) { _buff.Reset(); CatchMultiplier = 1f; RarityBonus = 0f; }
        }

        private void Update()
        {
            EnsureSubscribed();
            if (!_gameplayActive) return;   // 낚시 비활성 구간(메인스테이지 밖·보스)엔 쿨타임·발동 정지

            // 이벤트 없이 장착이 바뀐 경우(예: 런타임 LoadSaveState — 로드는 이벤트 미발행) 자가 보정:
            // 적재된 배가 현재 장착과 다르면 비워 재적재. (강화 등급값은 이미 매 프레임 읽어 라이브 반영되므로 장착만 메움)
            if (_skills.Count > 0 && _manager != null && _manager.HasEquippedBoat
                && _skills[0].cfg.skill != _manager.EquippedBoat)
                OnBoatEquipped(_manager.EquippedBoat);   // _skills 비움 → 아래에서 새 배 적재

            // 아직 스킬이 없으면 장착 배 적재 재시도 (한 번 채워지면 더는 재빌드 안 해 쿨타임 보존.
            // 장착 변경 시엔 OnBoatEquipped가 _skills를 비워 여기서 새 배로 다시 적재).
            if (_skills.Count == 0)
            {
                TryLoad();
                if (_skills.Count == 0)
                {
                    _retryTimer += Time.deltaTime;
                    if (_retryTimer > 2f && !_warned)
                    {
                        _warned = true;
                        var info = (_manager != null) ? _manager.DebugSummary() : "EquipmentManager 없음";
                        Debug.LogWarning($"[Boat] 장착 배 스킬 미적재 — {info}\n" +
                            "→ 'Boat' 미등록이면 EquipmentManager._equipments에 추가 / 'Boat스킬 0개'면 에셋 재import / 장착 배가 미해금/행동 미등록인지 확인");
                    }
                    return;
                }
            }

            float dt = Time.deltaTime;
            _buff.Reset();

            foreach (var rt in _skills)
            {
                // 현재 등급의 효과 수치 (강화하면 즉시 반영 — 매 프레임 읽음)
                float value = _manager.GetBoatSkillValue(rt.cfg.skill);

                if (rt.active > 0f) rt.active -= dt;

                rt.cd -= dt;
                if (rt.cd <= 0f)
                {
                    rt.cd = rt.cfg.cooldown;
                    float duration = rt.behaviour.Activate(rt.cfg, value, this);
                    OnSkillActivated?.Invoke(rt.cfg.skill, value, duration);   // 발동 순간 연출 훅(3종 공통)
                    if (duration > 0f)
                    {
                        rt.active = Mathf.Max(duration, 0.01f);
                        Debug.Log($"[Boat] {rt.cfg.skill} 발동 ({duration}초)");
                    }
                }

                if (rt.active > 0f) rt.behaviour.Accumulate(value, _buff);
            }

            CatchMultiplier = _buff.CatchMultiplier;
            RarityBonus = _buff.RarityBonus;
        }

        // 현재 상태 조회 (디버그 HUD·메인화면 쿨타임 HUD가 읽음).
        // cooldown = 총 쿨타임(라디얼 fill = cd/cooldown 계산용), cd = 남은 쿨타임, active = 남은 발동시간.
        public IEnumerable<(BoatSkill skill, float cd, float cooldown, float active)> GetStates()
        {
            foreach (var rt in _skills)
                yield return (rt.cfg.skill, Mathf.Max(rt.cd, 0f), Mathf.Max(rt.cfg.cooldown, 0f), Mathf.Max(rt.active, 0f));
        }
    }
}
