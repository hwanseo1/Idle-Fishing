namespace JHS.Sound
{
    // 사운드 재생 계약. 호출자(바인더/연출)는 이 인터페이스(또는 HS_Sound 파사드)에만 의존하고
    // 실제 재생기(HS_SoundManager)는 모른다 (DIP). 매니저가 없을 땐 Dummy로 무음 동작.
    public interface IHS_SoundService
    {
        void Play(HS_SoundId id);
    }

    // 매니저 미주입/씬 미배치 시 폴백 — 아무 소리도 안 내고 조용히 무시(단독 동작·NPE 방지).
    public sealed class DummyHS_SoundService : IHS_SoundService
    {
        public void Play(HS_SoundId id) { }
    }

    // 전역 진입점. 게임 어디서나 HS_Sound.Play(id) 한 줄로 호출.
    // 실제 매니저가 OnEnable에서 Register하면 그쪽으로, 없으면 Dummy(무음)로 라우팅된다.
    public static class HS_Sound
    {
        private static IHS_SoundService _current = new DummyHS_SoundService();

        public static IHS_SoundService Current => _current;

        public static void Register(IHS_SoundService service)
            => _current = service ?? new DummyHS_SoundService();

        // 등록했던 그 서비스가 사라질 때만 Dummy로 되돌림(다른 매니저가 이미 잡았으면 유지).
        public static void Unregister(IHS_SoundService service)
        {
            if (_current == service) _current = new DummyHS_SoundService();
        }

        public static void Play(HS_SoundId id) => _current.Play(id);
    }
}
