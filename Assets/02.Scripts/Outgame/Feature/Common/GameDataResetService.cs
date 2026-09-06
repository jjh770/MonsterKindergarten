using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Firebase.Auth;
using Firebase.Firestore;
#endif

// 게임 진행도 초기화. 인증 계정과 기기 음량 설정은 삭제하지 않는다.
public static class GameDataResetService
{
    private static bool s_isRunning;
    private static string PendingKey(string userId) => $"{userId}_GameDataResetPending";

    public static bool HasPendingReset(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) &&
               PlayerPrefs.GetInt(PendingKey(userId), 0) == 1;
    }

    public static async UniTask ResetAsync(string userId)
    {
        if (s_isRunning) throw new InvalidOperationException("초기화를 처리하고 있습니다.");
        if (string.IsNullOrWhiteSpace(userId) ||
            AccountManager.Instance == null || AccountManager.Instance.UserId != userId)
        {
            throw new InvalidOperationException("로그인한 계정을 확인할 수 없습니다.");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (FirebaseAuth.DefaultInstance.CurrentUser?.UserId != userId)
            throw new InvalidOperationException("로그인 계정이 변경되었습니다. 다시 로그인해 주세요.");
        if (Application.internetReachability == NetworkReachability.NotReachable)
            throw new InvalidOperationException("데이터 초기화에는 인터넷 연결이 필요합니다.");
#endif

        s_isRunning = true;
        GameplaySaveGate.BeginReset();
        try
        {
            // 서버 삭제 직후 앱이 종료돼도 다음 로그인에서 로컬 정리까지 재개한다.
            PlayerPrefs.SetInt(PendingKey(userId), 1);
            PlayerPrefs.Save();

#if UNITY_ANDROID && !UNITY_EDITOR
            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
            using var timeout = new CancellationTokenSource();
            // 실패 안내도 Unity 메인 스레드에서 이어지도록 PlayerLoop 타이머를 쓴다.
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(20), DelayType.Realtime);
            // 지연 저장은 세대 번호로 폐기하고, 이미 전송한 쓰기는 서버 반영을 기다린다.
            await db.WaitForPendingWritesAsync().AsUniTask()
                .AttachExternalCancellation(timeout.Token);

            // 새 저장 도메인을 추가하면 이 배치와 아래 로컬 삭제를 함께 확장한다.
            // 다른 기기의 로컬 세이브까지 무효화하는 계정 단위 세대 관리는 별도 정책이다.
            WriteBatch batch = db.StartBatch();
            batch.Delete(db.Collection("Currency").Document(userId));
            batch.Delete(db.Collection("SlimeStatus").Document(userId));
            batch.Delete(db.Collection("Upgrade").Document(userId));
            // 진행도는 아니지만 계정에 딸린 문서라 함께 지운다. 남겨도 무해하지만
            // 계정 삭제 뒤 고아 문서가 남는다.
            batch.Delete(db.Collection("TimeSync").Document(userId));
            await batch.CommitAsync().AsUniTask().AttachExternalCancellation(timeout.Token);
#else
            await UniTask.CompletedTask;
#endif

            new LocalCurrencyRepository(userId).Delete();
            new PlayerPrefsSlimeStatusRepository(userId).Delete();
            new PlayerPrefsUpgradeRepository(userId).Delete();
            TutorialProgress.DeleteForUser(userId);
            PlayerPrefs.DeleteKey(PendingKey(userId));
            PlayerPrefs.Save();
        }
        finally
        {
            s_isRunning = false;
            // 성공·실패 모두 현재 게임의 저장 잠금은 유지한다. 로그인 화면에서만 해제한다.
            // 타임아웃은 서버 작업 취소가 아니므로 기존 게임으로 복귀시키면 안 된다.
        }
    }
}
