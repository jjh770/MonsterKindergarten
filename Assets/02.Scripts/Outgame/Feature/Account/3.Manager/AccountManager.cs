using System;
using UnityEngine;


// 매니저의 역할:
// 1. 도메인 관리 : 생성/조회/수정/삭제와 같은 비즈니스 로직 
// 2. 외부와의 소통 창구
public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }

    private Account _currentAccount = null;
    public bool IsLogin => _currentAccount != null;
    public string Email => _currentAccount?.Email ?? string.Empty;


    private void Awake()
    {
        Instance = this;
    }


    public AuthResult TryLogin(string email, string password)
    {
        Account account = null;

        try
        {
            account = new Account(email, password);
        }
        catch (Exception ex)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }

        // 2. 가입한적 없다면 실패!
        if (!PlayerPrefs.HasKey(email))
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "아이디와 비밀번호를 확인해주세요.",
            };
        }

        // 3. 비밀번호 틀렸다면 실패.
        string myPassword = PlayerPrefs.GetString(email);
        if (myPassword != account.Password)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "아이디와 비밀번호를 확인해주세요.",
            };
        }

        _currentAccount = account;

        return new AuthResult
        {
            Success = true,
            Account = _currentAccount,
        };
    }

    public AuthResult TryRegister(string email, string password)
    {
        // 1. 이메일 중복검사
        if (PlayerPrefs.HasKey(email))
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "중복된 계정입니다.",
            };
        }

        try
        {
            Account account = new Account(email, password);
        }
        catch (Exception ex)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }

        // 3. 성공하면 저장!
        PlayerPrefs.SetString(email, password);

        return new AuthResult()
        {
            Success = true,
        };
    }

    public void Logout()
    {

    }
}
