using UnityEngine;

namespace JHS.Sound
{
    // BGM 재생 계약. 호출자(StageBgmController 등)는 이 인터페이스(또는 HS_Bgm 파사드)에만 의존하고
    // 실제 재생기(HS_BgmManager)는 모른다(DIP). 매니저가 없을 땐 Dummy로 무음 동작.
    public interface IHS_BgmService
    {
        void PlayLoop(AudioClip clip);       // 단일곡 루프(보스/멀티)
        void PlayShuffle(AudioClip[] clips);  // 여러 곡 랜덤 순환(메인 3곡)
        void Stop();
    }

    // 매니저 미배치 시 폴백 — 아무 소리도 안 내고 조용히 무시.
    public sealed class DummyHS_BgmService : IHS_BgmService
    {
        public void PlayLoop(AudioClip clip) { }
        public void PlayShuffle(AudioClip[] clips) { }
        public void Stop() { }
    }

    // 전역 진입점. HS_Sound(효과음)의 BGM 짝. HS_Bgm.PlayShuffle(...) 한 줄로 호출.
    public static class HS_Bgm
    {
        private static IHS_BgmService _current = new DummyHS_BgmService();

        public static IHS_BgmService Current => _current;

        public static void Register(IHS_BgmService service)
            => _current = service ?? new DummyHS_BgmService();

        public static void Unregister(IHS_BgmService service)
        {
            if (_current == service) _current = new DummyHS_BgmService();
        }

        public static void PlayLoop(AudioClip clip) => _current.PlayLoop(clip);
        public static void PlayShuffle(AudioClip[] clips) => _current.PlayShuffle(clips);
        public static void Stop() => _current.Stop();
    }
}
