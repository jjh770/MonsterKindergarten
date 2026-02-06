using Cysharp.Threading.Tasks;
using UnityEngine;

public class LocalAccountRepository : IAccountRepository
{
    private const string SALT = "sh123";
    public UniTask<AccountResult> Login(string email, string password)
    {
        // 2. 가입한적 없다면 실패!
        if (!PlayerPrefs.HasKey(email))
        {
            // return UniTask.FromResult(new AccountResult -> UniTask 팩토리 메서드 사용
            return new UniTask<AccountResult>(new AccountResult // -> UniTask 생성자 사용
            {
                Success = false,
                ErrorMessage = "<color=#FF5F5F>아이디와 비밀번호</color>를\n 확인해주세요.",
            });
        }

        // 3. 비밀번호 틀렸다면 실패.
        string myPassword = PlayerPrefs.GetString(email);
        // 암호화된 비밀번호 확인용
        if (!Crypto.VerifyPassword(password, myPassword, SALT))
        {
            return new UniTask<AccountResult>(new AccountResult
            {
                Success = false,
                ErrorMessage = "<color=#FF5F5F>아이디와 비밀번호</color>를\n 확인해주세요.",
            });
        }

        return new UniTask<AccountResult>(new AccountResult
        {
            Success = true,
            //Account = new Account(email, password),
        });
    }

    public UniTask<AccountResult> Register(string email, string password)
    {
        // 1. 이메일 중복검사
        if (!IsEmailAvailable(email))
        {
            return new UniTask<AccountResult>(new AccountResult
            {
                Success = false,
                ErrorMessage = "<color=#FF5F5F>중복된 계정</color>입니다.",
            });
        }

        // 비밀번호 암호화
        string hashedPassword = Crypto.HashPassword(password, SALT);

        // 3. 성공하면 저장! (+ 암호화 추가)
        PlayerPrefs.SetString(email, hashedPassword);

        return new UniTask<AccountResult>(new AccountResult
        {
            Success = true,
            Account = new Account(email, password),
        });
    }

    private bool IsEmailAvailable(string email)
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
