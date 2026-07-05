using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
    [Header("Input Field")]
    [SerializeField] private TMP_InputField _inputId;
    [SerializeField] private TMP_InputField _inputPassWord;

    [Header("Buttons")]
    [SerializeField] private Button _autoLoginButton;
    [SerializeField] private Button _registerButton;
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _guestLoginButton;

    [Header("Image")]
    [SerializeField] private Image _autoLoginCheckBox;

    [Header("Popup")]
    [SerializeField] private GameObject _registerPopup;
    [SerializeField] private Button _confirmButton;

    [Header("Logout")]
    [SerializeField] private GameObject _forceLogoutPanel;
    [SerializeField] private Button _LogoutConfirm;

    private bool _isAutoLogin = false;

    private void Awake()
    {
        if (_autoLoginButton != null) _autoLoginButton.onClick.AddListener(OnAutoLoginClicked);
        if (_registerButton != null) _registerButton.onClick.AddListener(OnRegisterClicked);
        if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginClicked);
        if (_guestLoginButton != null) _guestLoginButton.onClick.AddListener(OnGuestLoginClicked);
        if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
        if (_LogoutConfirm != null) _LogoutConfirm.onClick.AddListener(OnLogoutConfirmClicked);
    }
    private void OnDestroy()
    {
        if (PlayFabAuthManager.Instance == null)
            return;
    }

    private void OnEnable()
    {
        PlayFabAuthManager.Instance.OnRegisterSuccessEvent += OnRegisterSuccess;
        PlayFabAuthManager.Instance.OnForceLogoutEvent += OnForceLogout;
        PlayFabAuthManager.Instance.OnRegisterFailedEvent += OnRegisterFailed;
    }

    private void OnDisable()
    {
        PlayFabAuthManager.Instance.OnRegisterSuccessEvent -= OnRegisterSuccess;
        PlayFabAuthManager.Instance.OnForceLogoutEvent -= OnForceLogout;
        PlayFabAuthManager.Instance.OnRegisterFailedEvent -= OnRegisterFailed;
    }
    #region UI 변경
    private void AutoLoginCheckBox()
    {
        if (_isAutoLogin)
        {
            _autoLoginCheckBox.color = Color.white;
        }
        else
        {
            _autoLoginCheckBox.color = Color.green;
        }
    }

    private void OnRegisterSuccess()
    {
        _registerPopup.SetActive(true);
        _registerPopup.GetComponentInChildren<TextMeshProUGUI>().text = "회원가입이 완료되었습니다. \n 해당 계정으로 로그인을 진행합니다.";
    }

    private void OnForceLogout()
    {
        _forceLogoutPanel.SetActive(true);
    }

    private void OnRegisterFailed(string error)
    {
        _registerPopup.SetActive(true);
        _registerPopup.GetComponentInChildren<TextMeshProUGUI>().text = $"error: {error}";
    }
    #endregion

    #region Button OnClick 이벤트
    private void OnAutoLoginClicked()
    {
        _isAutoLogin = !_isAutoLogin;
        AutoLoginCheckBox();
    }
    private void OnRegisterClicked()
    {
        string id = _inputId.text;
        string password = _inputPassWord.text;

        PlayFabAuthManager.Instance.Register(id, password, _isAutoLogin);
    }
    private void OnLoginClicked()
    {
        string id = _inputId.text;
        string password = _inputPassWord.text;

        PlayFabAuthManager.Instance.Login(id, password, _isAutoLogin);
    }

    private void OnGuestLoginClicked()
    {
        PlayFabAuthManager.Instance.GuestLogin();
    }

    private void OnConfirmClicked()
    {
        _registerPopup.SetActive(false);
        string id = _inputId.text;
        string password = _inputPassWord.text;
        PlayFabAuthManager.Instance.Login(id, password, _isAutoLogin);
    }
    private void OnLogoutConfirmClicked()
    {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }
    #endregion
}