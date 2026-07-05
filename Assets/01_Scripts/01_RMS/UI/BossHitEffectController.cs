using RMS.Fishing;
using UnityEngine;


namespace RMS.UI
{
    // 보스 피격 이벤트를 구독해 데미지 넘버 / 플래시 / 공격 이펙트를 트리거한다.
    // 데미지 넘버와 플래시는 자동/수동 공통, 공격 이펙트(슬래시)는 수동 낚시 성공 시에만 재생한다.
    public class BossHitEffectController : MonoBehaviour
    {
        [SerializeField] private FishSpawnManager _spawnManager;
        [SerializeField] private BossHitFlash _hitFlash;
        [SerializeField] private BossDamageNumberSpawner _damageSpawner;
        [SerializeField] private BossAttackEffect _attackEffect;

        private void Awake()
        {
            if (_spawnManager == null)
                _spawnManager = FindFirstObjectByType<FishSpawnManager>();

            if (_spawnManager != null)
                _spawnManager.OnBossDamaged += HandleBossDamaged;
            else
                Debug.LogWarning("[BossHitEffectController] FishSpawnManager를 찾지 못했습니다.");
        }

        private void OnDestroy()
        {
            if (_spawnManager != null)
                _spawnManager.OnBossDamaged -= HandleBossDamaged;
        }

        private void HandleBossDamaged(int damage, bool isManual)
        {
            _hitFlash?.Flash();
            _damageSpawner?.Spawn(damage);

            // 공격 이펙트(슬래시)는 수동 낚시 성공 시에만 재생. 자동은 깜빡임 + 데미지 넘버만.
            if (isManual)
                _attackEffect?.Play();
        }
    }
}