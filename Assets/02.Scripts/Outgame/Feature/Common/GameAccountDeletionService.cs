using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 게임 데이터와 Firebase 인증 계정을 순서대로 삭제한다.
// 인증 UID를 잃기 전에 Firestore 문서와 로컬 데이터를 먼저 정리한다.
public static class GameAccountDeletionService
{
    private static bool s_isRunning;
    private static string PendingKey(string userId) => $"{userId}_GameAccountDeletionPending";
    // 계정과 상관없이 이 기기에 마무리할 계정 삭제가 남았는지. GameDataResetService의 같은 표식과 같은 이유다.
    private const string DevicePendingKey = "GameAccountDeletionPending_Device";

    public static bool HasPendingDeletionOnDevice => PlayerPrefs.GetInt(DevicePendingKey, 0) == 1;

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
            PlayerPrefs.SetInt(DevicePendingKey, 1);
            PlayerPrefs.Save();

            await GameDataResetService.ResetAsync(userId);
            await AccountManager.Instance.DeleteAccount();

            PlayerPrefs.DeleteKey(PendingKey(userId));
            PlayerPrefs.DeleteKey(DevicePendingKey);
            PlayerPrefs.Save();

            // 동의는 계정 삭제로만 철회된다. 삭제가 끝나면 이 기기의 동의 기록도 지워 다음 시작에서 다시 묻는다.
            ConsentRecord.Clear();
            Analytics.SetCollectionEnabled(false);
        }
        finally
        {
            s_isRunning = false;
        }
    }
}
