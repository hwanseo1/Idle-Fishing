using RMS.Data;
using RMS.Fishing;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageProgressUIButtons : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button _pauseStageButton;
    [SerializeField] private Button _nextStageButton;
    [SerializeField] private Button _previousStageButton;
    [SerializeField] private Button _moveStageButton;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI _currentStageText;

    [Header("Max Values")]
    [SerializeField] private int _maxMap = 3;
    [SerializeField] private int _maxStage = 5;

    [Header("Values")]
    [SerializeField] private int _currentMap = 1;
    [SerializeField] private int _currentStage = 1;

    [Header("MoveToNextStage")]
    [SerializeField] private bool _canMoveToNextStage = true;

    [Header("Sprites")]
    [SerializeField] private Sprite _pauseSprite;
    [SerializeField] private Sprite _playSprite;

    [Header("Unlock Button Manager")]
    [SerializeField] private UnlockButtonManager _unlockButtonManager;

    private FishSpawnManager _fishSpawnManager;
    private StageDictionaryConvertor _stageDictionaryConvertor;

    private void Awake()
    {
        _pauseStageButton.onClick.AddListener(OnPauseStageButtonClicked);
        _nextStageButton.onClick.AddListener(OnNextStageButtonClicked);
        _previousStageButton.onClick.AddListener(OnPreviousStageButtonClicked);
        _moveStageButton.onClick.AddListener(OnMoveStageButtonClicked);

        _fishSpawnManager = FindFirstObjectByType<FishSpawnManager>();
        _stageDictionaryConvertor = FindFirstObjectByType<StageDictionaryConvertor>();
        _unlockButtonManager = FindFirstObjectByType<UnlockButtonManager>();
    }


    private void OnPauseStageButtonClicked()
    {
        if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.MoveToStage))
        {
            Debug.Log("MoveToStage 기능이 잠겨있습니다.");
            return;
        }

        _canMoveToNextStage = !_canMoveToNextStage;

        _pauseStageButton.image.sprite = _canMoveToNextStage ? _playSprite : _pauseSprite;

        // CanMoveToNextStage 값을 FishSpawnManager에 전달하여 진행 여부를 제어
        _fishSpawnManager.CanMoveToNextStage = _canMoveToNextStage;

    }

    private void OnNextStageButtonClicked()
    {
        if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.MoveToStage))
        {
            Debug.Log("MoveToStage 기능이 잠겨있습니다.");
            return;
        }

        string maxStageId =
            PlayFabDataStore.Instance.GetPlayerInfo().stage.maxStageId;

        if (!TryParseStageId(maxStageId, out int maxMap, out int maxStage))
        {
            Debug.LogWarning($"maxStageId 파싱 실패: {maxStageId}");
            return;
        }

        int nextMap = _currentMap;
        int nextStage = _currentStage + 1;

        if (nextStage > _maxStage)
        {
            nextStage = 1;
            nextMap++;
        }

        if (nextMap > maxMap ||
            (nextMap == maxMap && nextStage > maxStage))
        {
            Debug.Log($"최대 스테이지 초과: {nextMap}-{nextStage}, max: {maxStageId}");
            return;
        }

        _currentMap = nextMap;
        _currentStage = nextStage;

        _currentStageText.text = $"{_currentMap}-{_currentStage}";
    }

    private void OnPreviousStageButtonClicked()
    {
        if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.MoveToStage))
        {
            Debug.Log("MoveToStage 기능이 잠겨있습니다.");
            return;
        }

        if (_currentStage <= 1 && _currentMap <= 1)
        {
            return;
        }

        _currentStage--;
        
        if (_currentStage <= 0)
        {
            _currentStage = 5;
            _currentMap--;
        }

        _currentStageText.text = $"{_currentMap}-{_currentStage}";
    }

    private void OnMoveStageButtonClicked()
    {
        if (!_unlockButtonManager.IsUnlocked(UnlockableFeature.MoveToStage))
        {
            Debug.Log("MoveToStage 기능이 잠겨있습니다.");
            return;
        }

        if (_stageDictionaryConvertor == null)
        {
            Debug.Log("StageDictionaryConvertor is not found.");
            return;
        }

        if (_fishSpawnManager == null)
        {
            Debug.Log("FishSpawnManager is not found.");
            return;
        }

        string stageKey = $"{_currentMap}-{_currentStage}";
        Debug.Log($"Move Stage to {stageKey}");

        StageData stageData = _stageDictionaryConvertor.GetStageDataById(stageKey);

        _fishSpawnManager.MoveToStage(stageData);
    }


    private bool TryParseStageId(string stageId, out int map, out int stage)
    {
        map = 1;
        stage = 1;

        if (string.IsNullOrEmpty(stageId))
            return false;

        string[] parts = stageId.Split('-');

        if (parts.Length != 2)
            return false;

        return int.TryParse(parts[0], out map)
            && int.TryParse(parts[1], out stage);
    }
}
