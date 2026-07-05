using Fusion;
using UnityEngine;


namespace RMS.Multiplay
{
    // 전체 기여도(TotalContribution) 네트워크 동기화 전용 컴포넌트.
    // FishingGameManager는 MonoBehaviour라 [Networked] 값을 가질 수 없어 분리됨.
    // Host(StateAuthority)만 값을 변경하고, 모든 클라는 Render()에서 변경을 감지해 UI만 갱신한다.
    public class TotalContributionSync : NetworkBehaviour
    {
        [Networked] public int TotalContribution { get; set; }
        private int _prevTotalContribution = -1; // 최소 1회는 무조건 갱신되도록 -1로 시작

        // 매치 종료 신호. 클라이언트가 도중에 나갔을 때 남은 클라이언트도 함께 종료되도록 전파한다.
        [Networked] public NetworkBool IsMatchEnded { get; set; }
        private bool _prevIsMatchEnded = false;

        // 조기 종료 여부 (정산 팝업 "중도 종료" 문구 분기용). IsMatchEnded와 같은 프레임에 세팅됨.
        [Networked] public NetworkBool IsEarlyExit { get; set; }

        // ⚠️ 퇴장자 명시적 추적용. 최대 3인이므로 고정 크기 배열로 둔다. 0 = 빈 슬롯(미사용), 1~3 = 퇴장한 PlayerIndex.
        // BuildRankEntries()가 "씬에 남은 오브젝트" 대신 이 값을 신뢰 기준으로 사용해야
        // Despawn 네트워크 전파 타이밍에 따라 호스트/클라 정산 결과가 달라지는 문제를 피할 수 있다.
        [Networked, Capacity(3)] public NetworkArray<int> LeftPlayerIndices => default;


        public override void Spawned()
        {
            _prevTotalContribution = TotalContribution;
            NotifyGameManager(TotalContribution);
            _prevIsMatchEnded = IsMatchEnded;
        }

        public override void Render()
        {
            if (TotalContribution != _prevTotalContribution)
            {
                _prevTotalContribution = TotalContribution;
                NotifyGameManager(TotalContribution);
            }

            if (IsMatchEnded != _prevIsMatchEnded)
            {
                _prevIsMatchEnded = IsMatchEnded;
                if (IsMatchEnded) NotifyMatchEnded();
            }
        }

        public void AddContribution(int amount)
        {
            if (!HasStateAuthority) return;
            TotalContribution += amount;
        }

        public void ResetContribution()
        {
            if (!HasStateAuthority) return;
            TotalContribution = 0;
        }

        // 매치 시작 시 호출. true→true는 변경 감지가 안 되므로 다음 매치를 위해 미리 리셋해둔다.
        public void ResetMatchEndFlag()
        {
            if (!HasStateAuthority) return;
            IsMatchEnded = false;
            IsEarlyExit = false;
        }

        public void NotifyMatchEndedToAll(bool isEarlyExit)
        {
            if (!HasStateAuthority) return;
            IsEarlyExit = isEarlyExit;
            IsMatchEnded = true;
        }


        #region 퇴장자 추적
        // Host만 호출. Despawn 직전에 호출해 PlayerIndex를 네트워크로 먼저 기록한다.
        // (Despawn 자체는 전파 타이밍이 보장되지 않아 BuildRankEntries 판단 기준으로 쓸 수 없음)
        public void MarkPlayerLeft(int playerIndex)
        {
            if (!HasStateAuthority) return;

            for (int i = 0; i < LeftPlayerIndices.Length; i++)
            {
                if (LeftPlayerIndices[i] == playerIndex) return; // 이미 기록됨
            }

            for (int i = 0; i < LeftPlayerIndices.Length; i++)
            {
                if (LeftPlayerIndices[i] == 0)
                {
                    LeftPlayerIndices.Set(i, playerIndex);
                    return;
                }
            }

            Debug.LogWarning($"[TotalContributionSync] LeftPlayerIndices 슬롯이 가득 찼습니다. playerIndex={playerIndex}");
        }

        // Host/클라 모두 호출 가능. BuildRankEntries에서 퇴장 여부 판정 기준으로 사용.
        public bool HasPlayerLeft(int playerIndex)
        {
            for (int i = 0; i < LeftPlayerIndices.Length; i++)
            {
                if (LeftPlayerIndices[i] == playerIndex) return true;
            }
            return false;
        }

        // Host만 호출. 매치 시작 시 ResetContribution과 함께 호출해 이전 매치의 퇴장 기록이 섞이지 않게 한다.
        public void ResetLeftPlayers()
        {
            if (!HasStateAuthority) return;

            for (int i = 0; i < LeftPlayerIndices.Length; i++)
            {
                LeftPlayerIndices.Set(i, 0);
            }
        }
        #endregion


        private void NotifyGameManager(int total)
        {
            var gm = FindFirstObjectByType<FishingGameManager>();
            if (gm != null) gm.OnTotalContributionSynced(total);
        }

        private void NotifyMatchEnded()
        {
            var gm = FindFirstObjectByType<FishingGameManager>();
            if (gm != null) gm.OnMatchEndedSynced(IsEarlyExit);
        }
    }
}