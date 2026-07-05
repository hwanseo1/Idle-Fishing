using System;
using System.Collections;
using UnityEngine;
using RMS.Data;   // FishData, BossData

namespace JHS.Fishing
{
    // 수동 미니게임 결과
    public enum ManualResult
    {
        Idle,     // 무행동 → 자동 보상 폴백
        Success,  // 성공 → 고보상
        Fail      // 실패 → 보상 0
    }

    // 입질 시 분기되는 수동 미니게임 (실제 구현은 류민서)
    // fish: 입질 때 확정된 물고기를 JHS가 넘겨준다. 이걸로 무엇을 할지(난이도 등)는 미니게임 구현 몫. (미연결 시 null 가능)
    // boss: 보스 스테이지에서 입질 시 BossData를 넘겨 난이도/경계선 개수를 결정한다.
    public interface IManualFishing
    {
        IEnumerator RunMiniGame(FishData fish, Action<ManualResult> onDone);
        IEnumerator RunMiniGame(BossData boss, Action<ManualResult> onDone);
    }

    // 미니게임 없을 때 분기 테스트용 더미 (딜레이 후 지정 결과 반환)
    public sealed class DummyManualFishing : IManualFishing
    {
        private readonly ManualResult _forced;
        private readonly float _delay;

        public DummyManualFishing(ManualResult forced, float delay = 0.3f)
        {
            _forced = forced;
            _delay = delay;
        }

        public IEnumerator RunMiniGame(FishData fish, Action<ManualResult> onDone)
        {
            yield return new WaitForSeconds(_delay);   // 더미는 난이도 무시, 지정 결과 반환
            onDone?.Invoke(_forced);
        }

        // 보스용 오버로드 — 더미는 난이도 무시, 동일하게 지정 결과 반환
        public IEnumerator RunMiniGame(BossData boss, Action<ManualResult> onDone)
        {
            yield return new WaitForSeconds(_delay);
            onDone?.Invoke(_forced);
        }
    }
}
