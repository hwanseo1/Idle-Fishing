using System;
using System.IO;
using System.Linq;
using UnityEngine;

public class AccountDataManager : MonoBehaviour
{
    public static AccountDataManager Instance;

    private string _path;
    private RecentAccountList _accountList = new();

    private const string CACHE_OWNER_KEY = "CacheOwnerId";

    private void Awake()
    {
        Instance = this;

        _path = Path.Combine(Application.persistentDataPath, "Accounts.json");

        Load();
    }

    public void Load()
    {
        if (!File.Exists(_path))
        {
            _accountList = new RecentAccountList();
            Save();
            return;
        }

        string json = File.ReadAllText(_path);
        _accountList = JsonUtility.FromJson<RecentAccountList>(json);

        if (_accountList == null)
            _accountList = new RecentAccountList();
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(_accountList, true);
        File.WriteAllText(_path, json);
    }

    public AccountData GetAccount(string id)
    {
        return _accountList.Accounts.Find(x => x.UserId == id);
    }

    public void SaveAccount(string id, PlayFabAuthManager.LoginType loginType, bool autoLogin)
    {
        AccountData account = GetAccount(id);

        if (account == null)
        {
            account = new AccountData();
            _accountList.Accounts.Add(account);
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError("SaveAccount : id가 비어 있습니다.");
            return;
        }

        account.UserId = id;
        account.LoginType = loginType;
        account.AutoLogin = autoLogin;
        account.LastLoginTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Save();
    }

    public AccountData GetAutoLoginAccount()
    {
        return _accountList.Accounts
            .Where(x => x.AutoLogin)
            .OrderByDescending(x => x.LastLoginTime)
            .FirstOrDefault();
    }

    public void CheckAccountChanged(string currentUserId)
    {
        string lastUserId = PlayerPrefs.GetString(CACHE_OWNER_KEY, string.Empty);

        // 첫 로그인
        if (string.IsNullOrEmpty(lastUserId))
        {
            PlayerPrefs.SetString(CACHE_OWNER_KEY, currentUserId);
            PlayerPrefs.Save();
            return;
        }

        // 같은 계정
        if (lastUserId == currentUserId)
            return;

        Debug.Log($"계정 변경 : {lastUserId} -> {currentUserId}");

        DeleteLocalCache();

        PlayerPrefs.SetString(CACHE_OWNER_KEY, currentUserId);
        PlayerPrefs.Save();
    }

    private void DeleteLocalCache()
    {
        DeleteFile("Player_Playfab.json");
    }

    private void DeleteFile(string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);

        if (!File.Exists(path))
            return;

        File.Delete(path);

        Debug.Log($"{fileName} 삭제");
    }
}