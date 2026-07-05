using RMS.Multiplay;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace RMS.UI
{
    // 보스 클리어 후 물고기 보상 목록을 표시하는 패널.
    // BossSettlementService.SettleClear()가 반환한 결과를 받아서 슬롯을 동적으로 채운다.
    public class BossRewardPanel : MonoBehaviour
    {
        [Header("패널 루트")]
        [Tooltip("비워두면 이 컴포넌트가 붙은 GameObject를 사용한다.")]
        [SerializeField] private GameObject _panelRoot;

        [Header("슬롯")]
        [Tooltip("FishIcon(Image) + AmountText(TMP_Text)로 구성된 RewardSlot 프리팹")]
        [SerializeField] private BossRewardSlot _slotPrefab;

        [Tooltip("슬롯이 배치될 부모. Vertical 또는 Grid Layout Group 권장.")]
        [SerializeField] private Transform _slotContainer;

        [Header("텍스트")]
        [SerializeField] private TMP_Text _titleText;

        [Header("버튼")]
        [SerializeField] private Button _confirmButton;


        private void Awake()
        {
            if (_panelRoot == null) _panelRoot = gameObject;
            _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(Hide);
        }

        private void OnDisable()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(Hide);
        }


        private System.Action _onConfirm;

        // BossSettlementService.SettleClear() 결과를 받아 패널을 표시한다.
        // onConfirm: 확인 버튼 클릭 시 1회 실행 (씬 복귀 등 후속 동작 연결용)
        public void Show(List<BossRewardResult> results, System.Action onConfirm = null)
        {
            _onConfirm = onConfirm;
            ClearSlots();

            //if (_titleText != null)
            //    _titleText.text = "보스 클리어 보상";

            if (results != null && _slotPrefab != null && _slotContainer != null)
            {
                foreach (BossRewardResult r in results)
                {
                    BossRewardSlot slot = Instantiate(_slotPrefab, _slotContainer);
                    slot.Set(r.Icon, r.FishId, r.Amount, r.IsLegendary);
                }
            }

            _panelRoot.SetActive(true);

            if (_confirmButton != null)
                _confirmButton.interactable = true;
        }


        public void Hide()
        {
            _panelRoot.SetActive(false);
            var cb = _onConfirm;
            _onConfirm = null;
            cb?.Invoke();
        }

        private void ClearSlots()
        {
            if (_slotContainer == null) return;
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);
        }
    }
}