using System;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 재화 계산에서 overflow를 실패 결과로 돌려주기 위한 안전 연산 모음입니다.
    /// </summary>
    internal static class CurrencyMath
    {
        public static bool TryAdd(long current, long delta, out long result)
        {
            try
            {
                result = checked(current + delta);
                return true;
            }
            catch (OverflowException)
            {
                result = current;
                return false;
            }
        }

        public static bool TryMultiply(long value, int multiplier, out long result)
        {
            try
            {
                result = checked(value * multiplier);
                return true;
            }
            catch (OverflowException)
            {
                result = value;
                return false;
            }
        }
    }

    /// <summary>
    /// Fisher 런타임에서 사용하는 3화폐 표시, 조회, 증감 계약을 한 곳에 모읍니다.
    /// 서버/PlayerData 연동은 이 이름을 기준으로 통화 키를 맞춥니다.
    /// </summary>
    public static class FisherCurrencyContract
    {
        /// <summary>
        /// Gold 계열 통화인지 확인합니다.
        /// </summary>
        public static bool IsGoldCurrency(string currency)
        {
            return currency == "softCurrency" || currency == "gold" || currency == "Gold";
        }

        /// <summary>
        /// Prism Pearl 표시/보상 계약에서 인식하는 통화 이름인지 확인합니다.
        /// </summary>
        public static bool IsPrismPearl(string currency)
        {
            return currency == "prismPearl" ||
                   currency == "PrismPearl" ||
                   currency == "prism_pearl";
        }

        /// <summary>
        /// Pirate Coin 표시/보상 계약에서 인식하는 통화 이름인지 확인합니다.
        /// </summary>
        public static bool IsPirateCoin(string currency)
        {
            return currency == "pirateCoin" ||
                   currency == "PirateCoin" ||
                   currency == "pirate_coin";
        }

        /// <summary>
        /// Fisher가 표시하거나 보상으로 지급할 수 있는 통화 이름인지 확인합니다.
        /// </summary>
        public static bool IsKnownCurrency(string currency)
        {
            return IsGoldCurrency(currency) || IsPrismPearl(currency) || IsPirateCoin(currency);
        }

        /// <summary>
        /// 지정 통화의 현재 보유량을 읽습니다. 알 수 없는 통화는 0으로 취급합니다.
        /// </summary>
        public static long GetBalance(PlayerRuntimeState state, string currency)
        {
            if (state == null)
            {
                return 0;
            }

            if (IsGoldCurrency(currency))
            {
                return state.softCurrency;
            }

            if (IsPrismPearl(currency))
            {
                return state.prismPearl;
            }

            if (IsPirateCoin(currency))
            {
                return state.pirateCoin;
            }

            return 0;
        }

        /// <summary>
        /// 지정 통화에 delta를 적용합니다. overflow나 알 수 없는 통화는 실패로 돌려줍니다.
        /// </summary>
        public static bool TryAddBalance(PlayerRuntimeState state, string currency, long delta, out long nextBalance)
        {
            nextBalance = 0;
            if (state == null || !IsKnownCurrency(currency))
            {
                return false;
            }

            long current = GetBalance(state, currency);
            if (!CurrencyMath.TryAdd(current, delta, out nextBalance))
            {
                return false;
            }

            if (IsGoldCurrency(currency))
            {
                state.softCurrency = nextBalance;
                return true;
            }

            if (IsPrismPearl(currency))
            {
                state.prismPearl = nextBalance;
                return true;
            }

            if (IsPirateCoin(currency))
            {
                state.pirateCoin = nextBalance;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 내부 결제 테스트용 Cash 잔액입니다. UI 지갑/상점 표시 통화에는 노출하지 않습니다.
        /// </summary>
        public static long GetHiddenCashBalance(PlayerRuntimeState state)
        {
            return state == null ? 0 : state.cash;
        }

        /// <summary>
        /// 테스트/운영툴에서만 쓰는 내부 Cash 지급입니다.
        /// </summary>
        public static bool TryGrantHiddenCash(PlayerRuntimeState state, long amount, out long nextBalance)
        {
            nextBalance = 0;
            if (state == null || amount < 0)
            {
                return false;
            }

            if (!CurrencyMath.TryAdd(state.cash, amount, out nextBalance))
            {
                return false;
            }

            state.cash = nextBalance;
            return true;
        }

        /// <summary>
        /// 유료 상품 결제 시 내부적으로만 쓰는 Cash 소비입니다. 실패 시 잔액을 바꾸지 않습니다.
        /// </summary>
        public static bool TryConsumeHiddenCash(PlayerRuntimeState state, long amount, out long nextBalance)
        {
            nextBalance = 0;
            if (state == null || amount < 0 || state.cash < amount)
            {
                return false;
            }

            if (!CurrencyMath.TryAdd(state.cash, -amount, out nextBalance))
            {
                return false;
            }

            state.cash = nextBalance;
            return true;
        }

        /// <summary>
        /// 가방/상태 UI에서 쓰는 3화폐 지갑 표시 문자열입니다.
        /// </summary>
        public static string FormatWallet(PlayerRuntimeState state)
        {
            long gold = state == null ? 0 : state.softCurrency;
            long prismPearl = state == null ? 0 : state.prismPearl;
            long pirateCoin = state == null ? 0 : state.pirateCoin;
            return "골드 " + CompactNumberFormatter.Format(gold) +
                   " / 진주 " + CompactNumberFormatter.Format(prismPearl) +
                   " / 해적 " + CompactNumberFormatter.Format(pirateCoin);
        }

        /// <summary>
        /// 상점/보상 행에서 쓰는 단일 통화 금액 표시 문자열입니다.
        /// </summary>
        public static string FormatAmount(string currency, long amount)
        {
            return CompactNumberFormatter.Format(amount) + " " + CurrencySuffix(currency);
        }

        private static string CurrencySuffix(string currency)
        {
            if (IsGoldCurrency(currency))
            {
                return "G";
            }

            if (IsPrismPearl(currency))
            {
                return "PP";
            }

            if (IsPirateCoin(currency))
            {
                return "PC";
            }

            return "?";
        }
    }
}
