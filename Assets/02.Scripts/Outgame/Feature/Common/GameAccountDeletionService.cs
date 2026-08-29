using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 게임 데이터와 Firebase 인증 계정을 순서대로 삭제한다.
// 인증 UID를 잃기 전에 Firestore 문서와 로컬 데이터를 먼저 정리한다.
public static class GameAccountDeletionService
{
    private static bool s_isRunning;
    private static string PendingKey(string userId) => $"{userId}_GameAccountDeletionPending";

    public static bool HasPendingDeletion(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) &&
               PlayerPrefs.GetInt(PendingKey(userId), 0) == 1;
    }

    public static async UniTask DeleteAsync(string userId)
    {
        if (s_isRunning) throw new InvalidOperationException("계정 삭제를 처리하고 있습니다.");
        if (string.IsNullOrWhiteSpace(userId) ||
            AccountManager.Instance == null || AccountManager.Instance.UserId != userId)
        {
            throw new InvalidOperationException("로그인한 계정을 확인할 수 없습니다.");
        }

        s_isRunning = true;
        try
        {
            // 데이터 삭제 뒤 앱이 종료돼도 다음 로그인에서 인증 계정 삭제를 재개한다.
            PlayerPrefs.SetInt(PendingKey(userId), 1);
            PlayerPrefs.Save();

            await GameDataResetService.ResetAsync(userId);
            await AccountManager.Instance.DeleteAccount();

            PlayerPrefs.DeleteKey(PendingKey(userId));
            PlayerPrefs.Save();
        }
        finally
        {
            s_isRunning = false;
        }
    }
}
