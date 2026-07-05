using Crew;
using OfflineReward;
using PlayFab;
using PlayFab.ClientModels;
using Runtime;
using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public class PlayFabAuthManager : MonoBehaviour
{
    public static PlayFabAuthManager Instance { get; private set; }

    [Header("PlayFab")]
    [SerializeField] private string _titleId = "180BD6";

    [Header("RuntimeStateController")]
    [SerializeField] private RuntimeStateController _runtimeStateController;

    [Header("Transition After Login Controller")]
    [SerializeField] private TransitionAfterLoginController _transitionAfterLoginController;

    public bool IsLoggedIn { get; private set; }
    public string CurrentPlayFabId { get; private set; }
    public string CurrentUserId { get; private set; }
    public LoginType CurrentLoginType { get; private set; }
    public LoginResult LoginResult { get; private set; }

    private bool _currentAutoLogin;

    public event Action OnLoginSuccessEvent;
    public event Action<string> OnLoginFailedEvent;
    public event Action OnRegisterSuccessEvent;
    public event Action<string> OnRegisterFailedEvent;
    public event Action OnGuestLoginSuccessEvent;
    public event Action<string> OnGuestLoginFailedEvent;
    public event Action OnForceLogoutEvent;
    
    private const string LAST_LOGIN_ID_KEY = "LastLoginID";
    private const string GUEST_ID_KEY = "GuestCustomId";
    private const string LAST_LOGIN_TYPE_KEY = "LastLoginType";
    private const string ACCOUNT_CREATED_KEY = "AccountCreated";
    public string CurrentSessionKey { get; private set; }   // Session 토큰

    private Coroutine _sessionCheckCoroutine;
    private const float SESSION_CHECK_INTERVAL = 30f;

    [Serializable]
    public class GetCurrentSessionResult
    {
        public bool success;
        public bool exists;
        public string sessionKey;
    }

    public enum LoginType
    {
        None,
        Account,
        Guest
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        PlayFabSettings.TitleId = _titleId;

        Debug.Log("PlayFabAuthManager Awake Finished.");
    }
    public void TryAutoLogin()
    {
        AccountData account = AccountDataManager.Instance.GetAutoLoginAccount();

        if (account == null)
        {
            GuestLogin();
            return;
        }

        CurrentUserId = account.UserId;

        switch (account.LoginType)
        {
            case LoginType.Account:

                Debug.Log("자동 로그인 계정 존재");

                // 로그인 UI에서 비밀번호 입력이 필요하므로
                // 자동 로그인을 구현하려면 비밀번호 대신
                // PlayFab Session Ticket 또는 CustomID 방식을 사용해야 합니다.

                break;

            case LoginType.Guest:

                GuestLogin();
                break;
        }
    }

    public LoginType GetLastLoginType()
    {
        return (LoginType)
            PlayerPrefs.GetInt(
                LAST_LOGIN_TYPE_KEY,
                (int)LoginType.None);
    }

    #region Guest Login
    public void GuestLogin()
    {
        Debug.Log("GuestLogin() 진입");

        if (IsLoggedIn)
        {
            Debug.LogWarning("이미 로그인 상태입니다.");
            return;
        }

        string customId = GetOrCreateGuestId();

        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(
            request,
            OnGuestLoginSuccess,
            OnGuestLoginFailed);
    }

    private string GetOrCreateGuestId()
    {
        Debug.Log($"HasKey : {PlayerPrefs.HasKey(GUEST_ID_KEY)}");

        if (PlayerPrefs.HasKey(GUEST_ID_KEY))
        {
            string id = PlayerPrefs.GetString(GUEST_ID_KEY);
            Debug.Log($"기존 Guest ID 사용 : {id}");
            return id;
        }

        string guestId = "Guest_" + Guid.NewGuid().ToString("N");

        Debug.Log($"새 Guest ID 생성 : {guestId}");

        PlayerPrefs.SetString(GUEST_ID_KEY, guestId);
        PlayerPrefs.Save();

        return guestId;
    }

    private void OnGuestLoginSuccess(LoginResult result)
    {
        string guestId = GetOrCreateGuestId();

        CompleteLogin(
            result,
            LoginType.Guest,
            guestId,
            true);
    }

    private void OnGuestLoginFailed(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());

        OnGuestLoginFailedEvent?.Invoke(error.ErrorMessage);
    }
    #endregion

    public void LoginOrRegister(string id, string password, bool autoLogin)
    {
        CurrentUserId = id;
        _currentAutoLogin = autoLogin;

        AccountData account = AccountDataManager.Instance.GetAccount(id);

        if (account != null)
        {
            Login(id, password, autoLogin);
            return;
        }

        Register(id, password, autoLogin);
    }

    #region Register
    // 회원가입 시 마스터 플레이어 계정 생성됨(마스터 플레이어 계정 = Google 로그인/Steam 로그인으로 계정 연동)
    public void Register(string id, string password, bool autoLogin)
    {
        CurrentUserId = id;

        _currentAutoLogin = autoLogin;

        if (string.IsNullOrWhiteSpace(id))
        {
            OnRegisterFailedEvent?.Invoke("아이디를 입력하세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            OnRegisterFailedEvent?.Invoke("비밀번호를 입력하세요.");
            return;
        }

        var request = new RegisterPlayFabUserRequest
        {
            Username = id,
            Password = password,
            RequireBothUsernameAndEmail = false
        };

        PlayFabClientAPI.RegisterPlayFabUser(
            request,
            OnRegisterSuccess,
            OnRegisterFailed);
    }

    private void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        PlayerPrefs.SetInt(ACCOUNT_CREATED_KEY, 1);
        PlayerPrefs.Save();

        OnRegisterSuccessEvent?.Invoke();

        Debug.Log("회원가입 성공");

        AccountDataManager.Instance.SaveAccount(CurrentUserId, LoginType.Account, _currentAutoLogin);
    }

    private void OnRegisterFailed(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());

        OnRegisterFailedEvent?.Invoke(error.GenerateErrorReport());
    }
    #endregion

    #region Login
    public void Login(string id, string password, bool autoLogin)
    {
        CurrentUserId = id;
        _currentAutoLogin = autoLogin;

        if (IsLoggedIn)
        {
            Debug.LogWarning("이미 로그인 상태입니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            OnLoginFailedEvent?.Invoke("아이디를 입력하세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            OnLoginFailedEvent?.Invoke("비밀번호를 입력하세요.");
            return;
        }

        var request = new LoginWithPlayFabRequest
        {
            Username = id,
            Password = password
        };

        PlayFabClientAPI.LoginWithPlayFab(
            request,
            OnLoginSuccess,
            OnLoginFailed);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        CompleteLogin(
            result,
            LoginType.Account,
            CurrentUserId,
            _currentAutoLogin);
    }

    private void OnLoginFailed(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());

        OnLoginFailedEvent?.Invoke(error.GenerateErrorReport());
    }
    #endregion

    #region 로그인 공통 메서드
    private void CompleteLogin(LoginResult result, LoginType loginType, string userId, bool autoLogin)
    {
        CurrentPlayFabId = result.PlayFabId;
        CurrentSessionKey = CreateSessionKey(result.SessionTicket);
        CurrentUserId = userId;

        PlayFabGateway.Instance.Session.SetCurrentSession(
            CurrentSessionKey,
            onSuccess: () =>
            {
                Debug.Log("Session 등록 완료");

                IsLoggedIn = true;

                PlayerPrefs.SetString(LAST_LOGIN_ID_KEY, CurrentUserId);
                PlayerPrefs.SetInt(LAST_LOGIN_TYPE_KEY, (int)loginType);
                PlayerPrefs.Save();

                AccountDataManager.Instance.CheckAccountChanged(CurrentUserId);
                AccountDataManager.Instance.SaveAccount(CurrentUserId, loginType, autoLogin);

                PlayFabGateway.Instance.LoginInit(
                    onSuccess: () =>
                    {
                        Debug.Log("모든 플레이어 데이터 로드 완료");

                        if (_transitionAfterLoginController != null)
                        {
                            _ = _transitionAfterLoginController.CheckNextStateAsync();
                        }

                        CrewManager.Instance.LoadCrewData();

                        StartSessionCheck();

                        if (loginType == LoginType.Account)
                            OnLoginSuccessEvent?.Invoke();
                        else
                            OnGuestLoginSuccessEvent?.Invoke();

                        Debug.Log("로그인 완료");
                    });
            },
            onFail: error =>
            {
                Debug.LogError($"Session 등록 실패 : {error}");
            });

        Debug.Log($"로그인 성공 : {CurrentPlayFabId}");
    }
    #endregion
    #region Logout
    public void Logout()
    {
        if (!IsLoggedIn)
            return;

        StopSessionCheck();

        PlayFabClientAPI.ForgetAllCredentials();

        IsLoggedIn = false;
        CurrentSessionKey = string.Empty;
        CurrentPlayFabId = string.Empty;
        CurrentUserId = string.Empty;

        Debug.Log("로그아웃");

        // 자동로그인 해제하고 싶으면 사용
        // PlayerPrefs.DeleteKey(LAST_LOGIN_ID_KEY);
    }
    #endregion

    #region Auto Login

    public string GetLastLoginId()
    {
        return PlayerPrefs.GetString(
            LAST_LOGIN_ID_KEY,
            string.Empty);
    }

    public bool HasLastLoginId()
    {
        return PlayerPrefs.HasKey(
            LAST_LOGIN_ID_KEY);
    }
    #endregion

    #region Session
    private string CreateSessionKey(string sessionTicket)
    {
        using SHA256 sha = SHA256.Create();

        byte[] bytes = sha.ComputeHash(
            Encoding.UTF8.GetBytes(sessionTicket));

        StringBuilder sb = new StringBuilder();

        foreach (byte b in bytes)
            sb.Append(b.ToString("x2"));

        return sb.ToString();
    }

    private void StartSessionCheck()
    {
        if (_sessionCheckCoroutine != null)
            StopCoroutine(_sessionCheckCoroutine);

        _sessionCheckCoroutine = StartCoroutine(SessionCheckRoutine());
    }
    private IEnumerator SessionCheckRoutine()
    {
        CheckCurrentSession();

        while (IsLoggedIn)
        {
            yield return new WaitForSeconds(SESSION_CHECK_INTERVAL);

            CheckCurrentSession();
        }
    }

    private void CheckCurrentSession()
    {
        PlayFabGateway.Instance.Session.GetCurrentSession(
            onSuccess: response =>
            {
                if (!response.exists)
                    return;

                if (response.sessionKey != CurrentSessionKey)
                {
                    Debug.LogWarning("중복 로그인 감지");

                    ForceLogout();
                }
            },
            onFail: error =>
            {
                Debug.LogError(error);
            });
    }

    private void ForceLogout()
    {
        if (!IsLoggedIn)
            return;

        StopSessionCheck();

        Logout();

        Debug.Log("다른 기기에서 로그인되어 로그아웃됩니다.");

        OnForceLogoutEvent?.Invoke();
    }

    private void StopSessionCheck()
    {
        if (_sessionCheckCoroutine != null)
        {
            StopCoroutine(_sessionCheckCoroutine);
            _sessionCheckCoroutine = null;
        }
    }
    #endregion
}