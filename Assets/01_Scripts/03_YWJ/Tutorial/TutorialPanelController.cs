using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _tutorialPanel;
    [SerializeField] private Image _tutorialImage;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _closeButton;

    private List<Sprite> _currentImages = new();
    private int _currentIndex;
    private Action _onClosed;

    private void Awake()
    {
        _previousButton.onClick.AddListener(OnClickPrevious);
        _nextButton.onClick.AddListener(OnClickNext);
        _closeButton.onClick.AddListener(OnClickClose);

        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(false);
    }

    public void Open(List<Sprite> images, Action onClosed = null)
    {
        if (images == null || images.Count == 0)
            return;

        _currentImages = images;
        _currentIndex = 0;
        _onClosed = onClosed;

        _tutorialPanel.SetActive(true);

        Refresh();
    }

    private void Refresh()
    {
        if (_tutorialImage != null)
            _tutorialImage.sprite = _currentImages[_currentIndex];

        if (_previousButton != null)
            _previousButton.gameObject.SetActive(_currentIndex > 0);

        if (_nextButton != null)
            _nextButton.gameObject.SetActive(_currentIndex < _currentImages.Count - 1);

        if (_closeButton != null)
            _closeButton.gameObject.SetActive(_currentIndex >= _currentImages.Count - 1);
    }

    private void OnClickPrevious()
    {
        if (_currentIndex <= 0)
            return;

        _currentIndex--;
        Refresh();
    }

    private void OnClickNext()
    {
        if (_currentIndex >= _currentImages.Count - 1)
            return;

        _currentIndex++;
        Refresh();
    }

    private void OnClickClose()
    {
        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(false);

        _onClosed?.Invoke();
        _onClosed = null;
    }
}