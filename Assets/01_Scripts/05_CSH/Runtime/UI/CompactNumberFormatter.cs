using System;
using System.Globalization;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 재화, 경험치, 수량을 UI에 짧게 표시하기 위한 K/M/B 숫자 formatter입니다.
    /// </summary>
    public static class CompactNumberFormatter
    {
        #region Constants

        private const long Thousand = 1_000L;
        private const long Million = 1_000_000L;
        private const long Billion = 1_000_000_000L;

        #endregion

        #region Formatting

        /// <summary>
        /// 1,000 이상 값을 K/M/B 단위로 압축해 표시합니다.
        /// </summary>
        public static string Format(long value)
        {
            if (value < 0)
            {
                if (value == long.MinValue)
                {
                    return value.ToString(CultureInfo.InvariantCulture);
                }

                return "-" + Format(-value);
            }

            if (value >= Billion)
            {
                return FormatUnit(value, Billion, "B");
            }

            if (value >= Million)
            {
                return FormatUnit(value, Million, "M");
            }

            if (value >= Thousand)
            {
                return FormatUnit(value, Thousand, "K");
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatGold(long value)
        {
            return Format(value) + " G";
        }

        public static string FormatCount(long value)
        {
            return Format(value) + " 개";
        }

        private static string FormatUnit(long value, long divisor, string suffix)
        {
            decimal scaled = decimal.Round((decimal)value / divisor, 1, MidpointRounding.AwayFromZero);
            return scaled.ToString("0.0", CultureInfo.InvariantCulture) + suffix;
        }

        #endregion
    }
}
