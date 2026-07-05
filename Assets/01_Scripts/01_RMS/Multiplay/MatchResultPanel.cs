using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace RMS.Multiplay
{
    // 멀티플레이 매치 정산 결과 팝업.
    // FishingGameManager.ShowMatchResult()가 넘긴 데이터를 화면에 표시만 하는 순수 뷰.
    // 확인 버튼 클릭 시 전달받은 콜백으로 복귀 시작을 알린다.
    //
    // 보상: Show() 시점엔 placeholder 문구를 먼저 띄우고, GiveMultiplayReward 응답이 오면
    // ApplyRewardOutcome으로 실제 지급 내역으로 교체한다(서버 응답 대기로 팝업 자체가 늦게 뜨지 않게).
    public class MatchResultPanel : MonoBehaviour
    {
        #region 데이터 구조 (FishingGameManager가 채워서 넘김)
        public struct RankEntry
        {
            public int PlayerIndex;
            public int Score;
            public bool IsLocalPlayer;
        }

        public struct MatchResultData
        {
            public bool IsEarlyExit;
            public List<RankEntry> Entries; // 점수 내림차순 정렬된 상태로 전달됨
            public int TotalContribution;
            public int TotalContributionGoal;
            public bool GoalReached;
        }
        #endregion


        #region Inspector
        [Header("패널 루트")]
        [Tooltip("비워두면 이 컴포넌트가 붙은 GameObject를 그대로 사용한다.")]
        [SerializeField] private GameObject panelRoot;

        [Header("상단 안내문")]
        [SerializeField] private TMP_Text titleText;

        [Header("개인 결과")]
        [SerializeField] private TMP_Text myScoreText;

        [Header("순위 (1~3위 슬롯, 점수 내림차순으로 채워짐)")]
        [SerializeField] private RankSlot[] rankSlots;

        [Header("전체 기여도")]
        [SerializeField] private TMP_Text totalContribText;

        [Header("보상 내역 (안내문 / 실패 문구용)")]
        [Tooltip("보상이 없거나, 중도 종료 안내, 통신 실패 메시지를 표시. 실제 보상 항목은 아래 아이콘 슬롯이 담당.")]
        [SerializeField] private TMP_Text rewardText;

        [Header("보상 내역 (아이콘 슬롯)")]
        [Tooltip("아이콘(Image) + 수량(TMP_Text)으로 구성된 RewardSlotView 프리팹.")]
        [SerializeField] private RewardSlotView rewardSlotPrefab;
        [Tooltip("슬롯들이 배치될 부모. Horizontal Layout Group 또는 Grid Layout Group을 붙여둔다.")]
        [SerializeField] private Transform rewardSlotParent;
        [Tooltip("itemId/currencyCode -> 아이콘, 표시 이름 조회용 레지스트리.")]
        [SerializeField] private ItemIconRegistry itemIconRegistry;

        [Header("확인 버튼")]
        [SerializeField] private Button confirmButton;
        #endregion


        // 순위 한 줄. rankLabelText/nameText/scoreText 중 비워둔 칸은 그냥 건너뛴다.
        [System.Serializable]
        public class RankSlot
        {
            [Tooltip("한 줄로 합쳐서 보여줄 경우 이것만 채우면 됨. 예: '1위  P2 (나)  120'")]
            public TMP_Text lineText;

            public TMP_Text rankLabelText;
            public TMP_Text nameText;
            public TMP_Text scoreText;
        }


        private static readonly string[] RANK_LABELS = { "1위", "2위", "3위" };
        private const string REWARD_PLACEHOLDER_NORMAL = "보상은 추후\n정산 후 지급됩니다.";
        private const string REWARD_PLACEHOLDER_EARLY_EXIT = "중도 종료로\n보상이 지급되지 않습니다.";

        private Action _onConfirm;


        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(HandleConfirmClicked);
        }

        private void OnDisable()
        {
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        }


        // FishingGameManager가 매치 종료 시점에 호출. onConfirm은 확인 버튼 클릭 시 1회 실행된다.
        public void Show(MatchResultData data, Action onConfirm)
        {
            _onConfirm = onConfirm;

            panelRoot.SetActive(true);

            RefreshTitle(data.IsEarlyExit);
            RefreshMyScore(data.Entries);
            RefreshRanks(data.Entries);
            RefreshTotalContribution(data);
            RefreshReward(data.IsEarlyExit);
        }


        // 메인씬 복귀 전 인벤토리 동기화 실패 시 FishingGameManager가 호출. 확인 버튼은 재시도용으로 그대로 둔다.
        public void ShowInventorySyncFailure()
        {
            if (titleText != null)
                titleText.text = "인벤토리 동기화 실패 - 다시 확인 버튼을 눌러주세요.";
        }


        // GiveMultiplayReward 응답 도착 시 FishingGameManager가 호출.
        // itemId/currencyCode를 ItemIconRegistry로 조회해 아이콘 슬롯을 동적으로 채운다.
        public void ApplyRewardOutcome(MultiplayGrantedItem[] grantedItems, MultiplayGrantedCurrency[] grantedCurrencies)
        {
            ClearRewardSlots();

            bool hasItems = grantedItems != null && grantedItems.Length > 0;
            bool hasCurrencies = grantedCurrencies != null && grantedCurrencies.Length > 0;

            if (!hasItems && !hasCurrencies)
            {
                if (rewardText != null) rewardText.text = "지급된 보상이 없습니다.";
                return;
            }

            if (rewardText != null) rewardText.text = "";

            if (rewardSlotPrefab == null || rewardSlotParent == null)
            {
                Debug.LogWarning("[MatchResultPanel] rewardSlotPrefab 또는 rewardSlotParent가 비어 있어 아이콘 슬롯을 표시할 수 없습니다.");
                return;
            }

            if (hasItems)
            {
                foreach (var item in grantedItems)
                {
                    var icon = itemIconRegistry != null ? itemIconRegistry.GetIcon(item.itemId) : null;
                    var slot = Instantiate(rewardSlotPrefab, rewardSlotParent);
                    slot.Set(icon, item.count);
                }
            }

            if (hasCurrencies)
            {
                foreach (var currency in grantedCurrencies)
                {
                    var icon = itemIconRegistry != null ? itemIconRegistry.GetIcon(currency.currencyCode) : null;
                    var slot = Instantiate(rewardSlotPrefab, rewardSlotParent);
                    slot.Set(icon, currency.amount);
                }
            }
        }

        // GiveMultiplayReward 요청 자체가 실패(통신 오류)했을 때 호출.
        public void ShowRewardFailure()
        {
            ClearRewardSlots();
            if (rewardText == null) return;
            rewardText.text = "보상 지급 확인에 실패했습니다. 잠시 후 인벤토리를\n확인해주세요.";
        }


        private void RefreshTitle(bool isEarlyExit)
        {
            if (titleText == null) return;
            //titleText.text = isEarlyExit ? "중도 종료" : "정상 종료";
        }

        private void RefreshMyScore(List<RankEntry> entries)
        {
            if (myScoreText == null || entries == null) return;

            foreach (var entry in entries)
            {
                if (entry.IsLocalPlayer)
                {
                    myScoreText.text = $"내 기여도: {entry.Score}";
                    return;
                }
            }

            myScoreText.text = "내 기여도: -";
        }

        private void RefreshRanks(List<RankEntry> entries)
        {
            if (rankSlots == null || entries == null) return;

            for (int i = 0; i < rankSlots.Length; i++)
            {
                var slot = rankSlots[i];
                if (slot == null) continue;

                if (i >= entries.Count)
                {
                    SetRankSlot(slot, "", "", "");
                    continue;
                }

                var entry = entries[i];
                string rankLabel = i < RANK_LABELS.Length ? RANK_LABELS[i] : $"{i + 1}위";
                string name = entry.IsLocalPlayer ? $"P{entry.PlayerIndex} (나)" : $"P{entry.PlayerIndex}";
                string score = entry.Score.ToString();

                SetRankSlot(slot, rankLabel, name, score);
            }
        }

        private void SetRankSlot(RankSlot slot, string rankLabel, string name, string score)
        {
            if (slot.lineText != null)
                slot.lineText.text = string.IsNullOrEmpty(rankLabel) ? "" : $"{rankLabel}  {name}  {score}";

            if (slot.rankLabelText != null) slot.rankLabelText.text = rankLabel;
            if (slot.nameText != null) slot.nameText.text = name;
            if (slot.scoreText != null) slot.scoreText.text = score;
        }

        private void RefreshTotalContribution(MatchResultData data)
        {
            if (totalContribText == null) return;

            string goalSuffix = data.GoalReached ? " (목표 달성!)" : "";
            totalContribText.text = $"전체 기여도 {data.TotalContribution}/{data.TotalContributionGoal}{goalSuffix}";
        }

        private void RefreshReward(bool isEarlyExit)
        {
            // 이전 매치의 보상 슬롯이 남아있지 않도록 Show() 시점에도 정리한다.
            ClearRewardSlots();

            if (rewardText == null) return;
            rewardText.text = isEarlyExit ? REWARD_PLACEHOLDER_EARLY_EXIT : REWARD_PLACEHOLDER_NORMAL;
        }

        private void ClearRewardSlots()
        {
            if (rewardSlotParent == null) return;

            for (int i = rewardSlotParent.childCount - 1; i >= 0; i--)
                Destroy(rewardSlotParent.GetChild(i).gameObject);
        }

        private void HandleConfirmClicked()
        {
            if (confirmButton != null) confirmButton.interactable = false;

            panelRoot.SetActive(false);
            _onConfirm?.Invoke();
        }
    }
}