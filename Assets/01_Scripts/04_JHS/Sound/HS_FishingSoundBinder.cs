using UnityEngine;
using RMS.Data;        // FishData, FishRarity
using JHS.Fishing;     // AutoFishingController, FishingPhase

namespace JHS.Sound
{
    // 낚시 루프 이벤트 → 효과음 (1순위). FishingView와 동일 사상: 이벤트 구독만, 낚시 로직은 모름.
    // 컨트롤러를 비우면 같은 오브젝트 → 씬에서 자동 탐색.
    public class HS_FishingSoundBinder : MonoBehaviour
    {
        [SerializeField] private AutoFishingController _controller;

        private void OnEnable()
        {
            if (_controller == null) _controller = GetComponent<AutoFishingController>();
            if (_controller == null) _controller = FindFirstObjectByType<AutoFishingController>();
            if (_controller == null) return;

            _controller.OnPhaseChanged += HandlePhase;
            _controller.OnFishCaught   += HandleFishCaught;
            _controller.OnMultiCatch   += HandleMultiCatch;
            _controller.OnBossHit      += HandleBossHit;
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            _controller.OnPhaseChanged -= HandlePhase;
            _controller.OnFishCaught   -= HandleFishCaught;
            _controller.OnMultiCatch   -= HandleMultiCatch;
            _controller.OnBossHit      -= HandleBossHit;
        }

        // 보스 타격 → 타격 종류별 타격음. 다중(배율>1)이 최우선, 아니면 수동/자동.
        // (자동 보스타격은 항상 count 1, 다중 피격은 수동 경로에서만 발생)
        private void HandleBossHit(bool isManual, int multiCatchCount)
        {
            if (multiCatchCount > 1)  HS_Sound.Play(HS_SoundId.BossHitMulti);
            else if (isManual)        HS_Sound.Play(HS_SoundId.BossHitManual);
            else                      HS_Sound.Play(HS_SoundId.BossHitAuto);
        }

        // 단계 신호 → 효과음. 클립 미할당 id는 무음이라 안전(원하는 단계만 클립 끼우면 됨).
        private void HandlePhase(FishingPhase phase)
        {
            switch (phase)
            {
                case FishingPhase.Casting: HS_Sound.Play(HS_SoundId.Cast);           break;
                case FishingPhase.Bite:    HS_Sound.Play(HS_SoundId.Bite);           break;
                case FishingPhase.Reeling: HS_Sound.Play(HS_SoundId.Reeling);        break;
                case FishingPhase.Success: HS_Sound.Play(HS_SoundId.FishingSuccess); break;
                case FishingPhase.Fail:    HS_Sound.Play(HS_SoundId.FishingFail);    break;
            }
        }

        private int _lastMultiSoundFrame = -1;

        // 포획 결과 → 레어도별 효과음. (Success 단계음과 동시에 울릴 수 있으니, 둘 중 원하는 쪽만 클립 할당)
        // 수동 성공이면 다중포획 사운드도 함께(요청). 실제 다중포획(OnMultiCatch)과 겹쳐도 같은 프레임 1회만.
        private void HandleFishCaught(FishData fish, float contribution, bool isManual)
        {
            HS_Sound.Play(CatchIdFor(fish != null ? fish.Rarity : FishRarity.Common));
            if (isManual) PlayMultiCatchOnce();
        }

        private void HandleMultiCatch(int count) => PlayMultiCatchOnce();

        // 다중포획 사운드 — 같은 프레임 중복 방지(수동 성공 + 실제 다중포획이 동시에 와도 1회).
        private void PlayMultiCatchOnce()
        {
            if (Time.frameCount == _lastMultiSoundFrame) return;
            _lastMultiSoundFrame = Time.frameCount;
            HS_Sound.Play(HS_SoundId.MultiCatch);
        }

        private static HS_SoundId CatchIdFor(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Rare:      return HS_SoundId.CatchRare;
                case FishRarity.Epic:      return HS_SoundId.CatchEpic;
                case FishRarity.Legendary: return HS_SoundId.CatchLegendary;
                default:                   return HS_SoundId.CatchCommon;
            }
        }
    }
}
