using UnityEngine;
using TMPro;
using System;

public class CurrencyUIController : MonoBehaviour
{
    [Header("Currency UI")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI prismPearlText;
    [SerializeField] private TextMeshProUGUI pirateCoinText;

    [Header("Settings")]
    [SerializeField] private bool autoRefresh = true;
    [SerializeField] private float refreshInterval = 1f;

    private const string DefaultPlayerId = "player1";
    private float _timeSinceLastRefresh = 0f;

    private void OnEnable()
    {
        // 활성화 시 즉시 갱신
        RefreshUI();
    }

    private void Update()
    {
        if (!autoRefresh) return;

        _timeSinceLastRefresh += Time.deltaTime;
        if (_timeSinceLastRefresh >= refreshInterval)
        {
            _timeSinceLastRefresh = 0f;
            RefreshUI();
        }
    }

    /// <summary>
    /// 로컬 JSON 데이터를 읽어 UI 갱신
    /// </summary>
    public void RefreshUI()
    {
        if (PlayFabDataStore.Instance == null)
        {
            Debug.LogWarning("[CurrencyUI] PlayFabDataStore.Instance가 null입니다.");
            return;
        }

        try
        {
            var data = PlayFabDataStore.Instance.Data;
            
            // 플레이어 데이터 존재 확인
            if (data == null || !data.HasPlayer(DefaultPlayerId))
            {
                Debug.LogWarning($"[CurrencyUI] 플레이어 '{DefaultPlayerId}' 데이터가 없습니다.");
                SetDefaultUI();
                return;
            }

            // 화폐 데이터 가져오기
            var playerInfo = data.players[DefaultPlayerId];
            var currency = playerInfo.currency;

            if (currency == null)
            {
                Debug.LogWarning("[CurrencyUI] currency 데이터가 null입니다.");
                SetDefaultUI();
                return;
            }

            // UI 업데이트
            UpdateGoldUI(currency.gold);
            UpdatePrismPearlUI(currency.prismPearl);
            UpdatePirateCoinUI(currency.pirateCoin);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CurrencyUI] UI 갱신 중 오류: {e.Message}\n{e.StackTrace}");
            SetDefaultUI();
        }
    }

    /// <summary>
    /// 골드 UI 업데이트
    /// </summary>
    private void UpdateGoldUI(int gold)
    {
        if (goldText != null)
        {
            goldText.text = FormatCurrency(gold);
        }
    }

    /// <summary>
    /// 프리즘 펄 UI 업데이트
    /// </summary>
    private void UpdatePrismPearlUI(int prismPearl)
    {
        if (prismPearlText != null)
        {
            prismPearlText.text = FormatCurrency(prismPearl);
        }
    }

    /// <summary>
    /// 해적 코인 UI 업데이트
    /// </summary>
    private void UpdatePirateCoinUI(int pirateCoin)
    {
        if (pirateCoinText != null)
        {
            pirateCoinText.text = FormatCurrency(pirateCoin);
        }
    }

    /// <summary>
    /// 화폐 포맷 (천 단위 콤마)
    /// </summary>
    private string FormatCurrency(int amount)
    {
        return amount.ToString("N0");
    }

    /// <summary>
    /// 기본값으로 UI 설정
    /// </summary>
    private void SetDefaultUI()
    {
        UpdateGoldUI(0);
        UpdatePrismPearlUI(0);
        UpdatePirateCoinUI(0);
    }

    /// <summary>
    /// 수동으로 UI 갱신 호출 (외부에서 호출 가능)
    /// </summary>
    public void ForceRefresh()
    {
        RefreshUI();
    }

    #if UNITY_EDITOR
    [ContextMenu("Test/Refresh UI")]
    private void Editor_TestRefreshUI()
    {
        RefreshUI();
    }

    [ContextMenu("Test/Print Currency Data")]
    private void Editor_PrintCurrencyData()
    {
        if (PlayFabDataStore.Instance?.Data?.HasPlayer(DefaultPlayerId) == true)
        {
            var currency = PlayFabDataStore.Instance.Data.players[DefaultPlayerId].currency;
            Debug.Log($"[CurrencyUI] Gold: {currency.gold}, PrismPearl: {currency.prismPearl}, PirateCoin: {currency.pirateCoin}");
        }
        else
        {
            Debug.LogWarning("[CurrencyUI] 플레이어 데이터가 없습니다.");
        }
    }
    #endif
}
