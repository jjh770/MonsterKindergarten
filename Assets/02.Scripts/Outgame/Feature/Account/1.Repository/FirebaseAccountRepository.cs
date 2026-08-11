#if !UNITY_WEBGL || UNITY_EDITOR

using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using System;

public class FirebaseAccountRepository : IAccountRepository
{
    private readonly FirebaseAuth _auth;

    public FirebaseAccountRepository()
    {
        _auth = FirebaseAuth.DefaultInstance;
    }

    public async UniTask<AccountResult> Login(bool useManualSignIn)
    {
        try
        {
            PlayGamesPlatform playGames = PlayGamesPlatform.Activate();
            SignInStatus signInStatus = await AuthenticatePlayGames(playGames, useManualSignIn);

            if (signInStatus != SignInStatus.Success)
            {
                return new AccountResult
                {
                    Success = false,
                    ErrorMessage = $"Google Play 로그인에 실패했습니다.\n({signInStatus})",
                };
            }

            string authCode = await RequestServerAuthCode(playGames);
            if (string.IsNullOrEmpty(authCode))
            {
                return new AccountResult
                {
                    Success = false,
                    ErrorMessage = "Google Play 인증 코드를 가져오지 못했습니다.",
                };
            }

            Credential credential = PlayGamesAuthProvider.GetCredential(authCode);
            FirebaseUser user = await _auth.SignInWithCredentialAsync(credential).AsUniTask();

            return new AccountResult
            {
                Success = true,
                UserId = user.UserId,
            };
        }
        catch (FirebaseException e)
        {
            return new AccountResult
            {
                Success = false,
                ErrorMessage = e.Message,
            };
        }
        catch (Exception e)
        {
            return new AccountResult
            {
                Success = false,
                ErrorMessage = e.Message,
            };
        }
    }

    public void Logout()
    {
        _auth.SignOut();
    }

    private static UniTask<SignInStatus> AuthenticatePlayGames(
        PlayGamesPlatform playGames,
        bool useManualSignIn)
    {
        var completionSource = new UniTaskCompletionSource<SignInStatus>();

        if (useManualSignIn)
        {
            playGames.ManuallyAuthenticate(status => completionSource.TrySetResult(status));
        }
        else
        {
            playGames.Authenticate(status => completionSource.TrySetResult(status));
        }

        return completionSource.Task;
    }

    private static UniTask<string> RequestServerAuthCode(PlayGamesPlatform playGames)
    {
        var completionSource = new UniTaskCompletionSource<string>();
        playGames.RequestServerSideAccess(
            false,
            authCode => completionSource.TrySetResult(authCode));
        return completionSource.Task;
    }
}
#endif
