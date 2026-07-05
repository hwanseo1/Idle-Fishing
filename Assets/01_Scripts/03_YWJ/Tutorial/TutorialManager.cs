using System;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialType
{
    FirstLogin,
    Collection,
    Equipment,
    Ship,
    Crew,
    Cooking,
    Inventory,
    Recruit,
    Shop,
    MoveToStage,
    Multiplay
}

[Serializable]
public class TutorialContentData
{
    public TutorialType tutorialType;

    [Header("Tutorial Images")]
    public List<Sprite> tutorialImages = new();
}

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial Contents")]
    [SerializeField] private List<TutorialContentData> _tutorialContents = new();

    [Header("Panel Controller")]
    [SerializeField] private TutorialPanelController _panelController;

    private readonly Dictionary<TutorialType, TutorialContentData> _tutorialDictionary = new();

    private void Awake()
    {
        BuildDictionary();
    }

    private void BuildDictionary()
    {
        _tutorialDictionary.Clear();

        foreach (var content in _tutorialContents)
        {
            if (content == null)
                continue;

            if (_tutorialDictionary.ContainsKey(content.tutorialType))
            {
                Debug.LogWarning($"[TutorialManager] 중복 튜토리얼 타입: {content.tutorialType}");
                continue;
            }

            _tutorialDictionary.Add(content.tutorialType, content);
        }
    }

    public void TryShowTutorial(TutorialType tutorialType)
    {
        if (HasShownTutorial(tutorialType))
            return;

        if (!_tutorialDictionary.TryGetValue(tutorialType, out TutorialContentData content))
        {
            Debug.LogWarning($"[TutorialManager] 튜토리얼 콘텐츠가 없습니다: {tutorialType}");
            return;
        }

        if (content.tutorialImages == null || content.tutorialImages.Count == 0)
        {
            Debug.LogWarning($"[TutorialManager] 튜토리얼 이미지가 없습니다: {tutorialType}");
            return;
        }

        if (_panelController == null)
        {
            Debug.LogError("[TutorialManager] TutorialPanelController가 없습니다.");
            return;
        }

        _panelController.Open(
            content.tutorialImages,
            () =>
            {
                MarkTutorialShown(tutorialType);
            });
    }

    private bool HasShownTutorial(TutorialType tutorialType)
    {
        if (PlayFabDataStore.Instance == null)
            return false;

        PlayerInfo playerInfo = PlayFabDataStore.Instance.GetPlayerInfo();

        if (playerInfo == null || playerInfo.tutorialData == null)
            return false;

        if (playerInfo.tutorialData.shownTutorials == null)
            return false;

        string key = tutorialType.ToString();

        return playerInfo.tutorialData.shownTutorials.TryGetValue(key, out bool shown)
            && shown;
    }

    private void MarkTutorialShown(TutorialType tutorialType)
    {
        string key = tutorialType.ToString();

        if (PlayFabDataStore.Instance != null)
        {
            PlayerInfo playerInfo = PlayFabDataStore.Instance.GetPlayerInfo();

            if (playerInfo.tutorialData == null)
                playerInfo.tutorialData = new TutorialJSONModel();

            if (playerInfo.tutorialData.shownTutorials == null)
                playerInfo.tutorialData.shownTutorials = new Dictionary<string, bool>();

            playerInfo.tutorialData.shownTutorials[key] = true;

            PlayFabDataStore.Instance.UpdateTutorial(playerInfo.tutorialData);
        }

        if (PlayFabGateway.Instance?.Tutorial != null)
        {
            PlayFabGateway.Instance.Tutorial.MarkTutorialShown(
                key,
                _ => Debug.Log($"[TutorialManager] 튜토리얼 서버 저장 완료: {key}"),
                error => Debug.LogError($"[TutorialManager] 튜토리얼 서버 저장 실패: {error}"));
        }
    }
}