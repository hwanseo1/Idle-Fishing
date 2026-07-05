namespace JHS.Equipment
{
    // 골드 지갑 (실제 구현은 재화 파트). 강화 비용 차감에만 사용.
    public interface IGoldWallet
    {
        long Gold { get; }
        bool TrySpend(long amount);
        void Refund(long amount);   // 차감 후 롤백용 (재료 차감 실패 등)
    }

    // 재화 시스템 없을 때 쓰는 더미
    public sealed class DummyGoldWallet : IGoldWallet
    {
        public DummyGoldWallet(long initial = 999999) => Gold = initial;
        public long Gold { get; private set; }

        public bool TrySpend(long amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        public void Refund(long amount)
        {
            if (amount > 0) Gold += amount;
        }
    }
}
