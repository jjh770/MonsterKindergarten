using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using Firebase.Firestore;
#endif

// 기기 시계 대신 쓸 신뢰 시각.
//
// 오프라인 보상은 경과 시간으로 보상을 정하므로, 기기 시계를 앞으로 돌리면 상한만큼
// 반복해서 받을 수 있다. 서버가 찍은 시각을 한 번 받아 기기 시계와의 차이를 기억하고,
// 이후에는 그 보정값을 더해 쓴다.
//
// 이 SDK에는 쓰기 응답이나 스냅샷에서 서버 시각을 얻는 길이 없어, 표식을 쓰고 다시
// 읽는다.
//
// 표식은 진행도 문서가 아니라 전용 컬렉션에 둔다. 진행도 문서에 얹으면 신규 계정에서
// 그 문서를 먼저 만들어 버려, 재화 배열이 없는 문서가 생기고 로드가 차단된다.
// 세 저장 문서는 튜토리얼을 마칠 때 함께 만들어져야 한다.
public static class ServerClock
{
    private const string SyncCollectionName = "TimeSync";
    private const string SyncFieldName = "ServerSyncTime";

    private static TimeSpan _offset;
    private static bool s_isSyncing;

    // 동기화 전에는 보상을 계산하지 않는다. 확인하지 못한 시각으로 지급하면
    // 기기 시계를 돌린 만큼 그대로 준다.
    public static bool IsSynced { get; private set; }

    public static DateTime TrustedUtcNow => DateTime.UtcNow + _offset;

    // 로그인 화면으로 돌아갈 때 호출한다. 계정이 바뀌면 이전 보정값은 의미가 없다.
    public static void Clear()
    {
        IsSynced = false;
        _offset = TimeSpan.Zero;
        s_isSyncing = false;
    }

    // force는 앱 복귀 때 쓴다. 보정값은 기기 시계와의 차이라, 실행 중에 시계가 바뀌면
    // 함께 어긋난다. 강제 갱신이 실패해도 기존 보정값은 버리지 않는다. 버리면 오프라인
    // 상태로 돌아온 정직한 플레이어가 쌓인 보상을 잃는다.
    public static async UniTask<bool> TrySync(string userId, bool force = false)
    {
        if (IsSynced && !force) return true;
        if (s_isSyncing) return false;
        if (string.IsNullOrWhiteSpace(userId)) return false;
        // 초기화 중에는 문서를 건드리지 않는다.
        if (GameplaySaveGate.IsResetting) return false;

        s_isSyncing = true;
        try
        {
            return await SyncCore(userId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"서버 시각을 확인하지 못했습니다: {e.Message}");
            return false;
        }
        finally
        {
            s_isSyncing = false;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static async UniTask<bool> SyncCore(string userId)
    {
        DocumentReference document = FirebaseFirestore.DefaultInstance
            .Collection(SyncCollectionName)
            .Document(userId);

        await document.SetAsync(
            new Dictionary<string, object> { { SyncFieldName, FieldValue.ServerTimestamp } },
            SetOptions.MergeAll).AsUniTask();

        // 캐시가 아니라 서버 값을 받아야 한다.
        DocumentSnapshot snapshot = await document
            .GetSnapshotAsync(Source.Server)
            .AsUniTask();

        if (!snapshot.TryGetValue(SyncFieldName, out Timestamp serverTime))
        {
            Debug.LogWarning("서버 시각 표식을 읽지 못했습니다.");
            return false;
        }

        _offset = serverTime.ToDateTime() - DateTime.UtcNow;
        IsSynced = true;
        return true;
    }
#else
    // 에디터와 비 Android는 Firestore를 쓰지 않는다. 개발 전용 경로이므로
    // 기기 시각을 그대로 신뢰 시각으로 쓴다.
    private static UniTask<bool> SyncCore(string userId)
    {
        _offset = TimeSpan.Zero;
        IsSynced = true;
        return UniTask.FromResult(true);
    }
#endif
}
