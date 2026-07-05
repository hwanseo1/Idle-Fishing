using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 서비스 결과에서 UI, 저장, 로그가 참고할 아이템 수량 변화입니다.
    /// </summary>
    public sealed class ItemDelta
    {
        public string ItemId;
        public int CountDelta;
        public string InstanceId;
        public int LevelIndex;

        /// <summary>
        /// 단일 아이템 변화량을 생성합니다.
        /// </summary>
        public ItemDelta(string itemId, int countDelta, string instanceId = "", int levelIndex = -1)
        {
            ItemId = itemId;
            CountDelta = countDelta;
            InstanceId = instanceId;
            LevelIndex = levelIndex;
        }
    }

    /// <summary>
    /// Fisher 서비스가 반환하는 공통 결과 DTO입니다. 실패해도 상태 변경 여부를 호출자가 확인할 수 있게 합니다.
    /// </summary>
    public sealed class ServiceResult
    {
        public bool Success;
        public string FailureReason;
        public readonly List<ItemDelta> ItemDeltas = new List<ItemDelta>();
        public long CurrencyDelta;
        public readonly List<string> AffectedIds = new List<string>();
        public string MessageKey;

        /// <summary>
        /// 실패 사유와 로컬라이징 가능한 messageKey를 담은 결과를 만듭니다.
        /// </summary>
        public static ServiceResult Fail(string failureReason, string messageKey)
        {
            return new ServiceResult
            {
                Success = false,
                FailureReason = failureReason,
                MessageKey = messageKey
            };
        }

        /// <summary>
        /// 성공 messageKey를 담은 결과를 만듭니다.
        /// </summary>
        public static ServiceResult Ok(string messageKey)
        {
            return new ServiceResult
            {
                Success = true,
                FailureReason = string.Empty,
                MessageKey = messageKey
            };
        }
    }
}
