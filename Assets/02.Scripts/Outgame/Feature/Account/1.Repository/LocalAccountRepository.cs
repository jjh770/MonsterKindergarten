using UnityEngine;

public class LocalAccountRepository : IAccountRepository
{
    public AuthResult Login(string email, string password)
    {
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
        if (myPassword != password)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "아이디와 비밀번호를 확인해주세요.",
            };
        }

        return new AuthResult()
        {
            Success = true,
            Account = new Account(email, password),
        };
    }

    public AuthResult Register(string email, string password)
    {
        // 1. 이메일 중복검사
        if (IsEmailAvailable(email))
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "중복된 계정입니다.",
            };
        }

        // 3. 성공하면 저장!
        PlayerPrefs.SetString(email, password);

        return new AuthResult()
        {
            Success = true,
            Account = new Account(email, password),
        };
    }

    public bool IsEmailAvailable(string email)
    {
        if (PlayerPrefs.HasKey(email))
        {
            return false;
        }
        return true;
    }
    public void Logout()
    {
        Debug.Log("로그아웃 됐습니다.");
    }
}
