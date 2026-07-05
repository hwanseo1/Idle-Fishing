using System;
using System.Collections.Generic;
using static PlayFabAuthManager;

[Serializable]
public class AccountData
{
    public string UserId;
    public LoginType LoginType;
    public long LastLoginTime;
    public bool AutoLogin;
}
[Serializable]
public class RecentAccountList
{
    public List<AccountData> Accounts = new();
}