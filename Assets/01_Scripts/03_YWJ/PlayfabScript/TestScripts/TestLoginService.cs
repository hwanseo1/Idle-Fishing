using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using Runtime;




public class PlayFabTestLogin : MonoBehaviour
{
    [Header("PlayFab")]
    [SerializeField] private string titleId = "180BD6"; // PlayFab 개발자 포털에서 발급받은 Title ID

    [Header("Test Login")]
    [SerializeField] private string testCustomId = "GuestAccount";

    [Header("Buttons")]
    [SerializeField] private Button _testLoginButton;

    [Header("RuntimeStateController")]
    [SerializeField] private RuntimeStateController _runtimeStateController;

    private void Awake()
    {
        _testLoginButton.onClick.AddListener(OnTestLoginButtonClicked);
    }

    public void OnTestLoginButtonClicked()
    {
        if (testCustomId == "GuestAccount")
        {
            Debug.LogWarning("테스트용 Custom ID가 설정되어 있습니다. 고유한 Custom ID를 사용해야 합니다.");
            return;
        }

        Login();

        _runtimeStateController.CurrentState = RuntimeState.MAINSTAGE;
    }

    private void Login()
    {
        PlayFabSettings.TitleId = titleId;

        var request = new LoginWithCustomIDRequest
        {
            CustomId = testCustomId,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(
            request,
            OnLoginSuccess,
            OnLoginFailed
        );
    }

    private void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("PlayFab 로그인 성공");
        Debug.Log("PlayFabId: " + result.PlayFabId);
        Debug.Log("SessionTicket: " + result.SessionTicket);

        PlayFabGateway.Instance.LoginInit();
    }

    private void OnLoginFailed(PlayFabError error)
    {
        Debug.LogError("PlayFab 로그인 실패");
        Debug.LogError(error.GenerateErrorReport());
    }
}