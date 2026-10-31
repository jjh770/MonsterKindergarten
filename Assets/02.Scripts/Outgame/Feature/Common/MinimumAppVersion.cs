using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Firebase.Firestore;
#endif

// 이 앱이 아직 써도 되는 버전인지 서버에 묻는다. 너무 오래된 버전은 로그인하지 않고 업데이트로 보낸다.
// 로그인 전에 묻으므로 그 문서는 인증 없이 읽힌다(firestore.rules).
//
// 기준은 Firestore의 AppConfig/android 문서에 있는 MinVersionCode 하나다. 콘솔에서 숫자만 올리면 그보다 낮은
// Android Version Code의 앱이 막히므로, 강제 업데이트에 새 빌드가 필요 없다. 숫자는 새 버전이 스토어에서 모두
// 받을 수 있게 된 뒤에 올린다. 검토나 단계적 출시 중에 올리면 아직 업데이트를 받을 수 없는 플레이어가 갇힌다.
//
// 읽지 못하면 막지 않는다. 문서가 없거나, 값이 숫자가 아니거나, 네트워크가 잠깐 끊긴 정상 플레이어를 막는 쪽이
// 오래된 앱 하나를 들여보내는 쪽보다 나쁘다. 오래된 앱이 새 저장 형식을 만나는 경우는 저장소의 버전 검사가
// 따로 막는다.
//
// 이 검사는 이것을 넣은 버전부터 있다. 그 전에 나간 앱은 이 문서를 읽지 않으므로 막을 수 없다.
public static class MinimumAppVersion
{
    private const string ConfigCollectionName = "AppConfig";
    private const string ConfigDocumentName = "android";
    private const string MinVersionCodeFieldName = "MinVersionCode";

    public static async UniTask<bool> IsUpdateRequiredAsync()
    {
        try
        {
            return await IsUpdateRequiredCore();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"최소 버전을 확인하지 못해 그대로 진행합니다: {e.Message}");
            return false;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static async UniTask<bool> IsUpdateRequiredCore()
    {
        // 로그인 화면이 뜨자마자 묻기 때문에 Firebase 준비가 끝나기 전일 수 있다.
        FirebaseInitializer initializer = FirebaseInitializer.Instance;
        if (initializer == null || !await initializer.WaitForInitializationAsync()) return false;

        DocumentSnapshot snapshot = await FirebaseFirestore.DefaultInstance
            .Collection(ConfigCollectionName)
            .Document(ConfigDocumentName)
            .GetSnapshotAsync(Source.Server)
            .AsUniTask();

        if (!snapshot.Exists) return false;
        if (!snapshot.TryGetValue(MinVersionCodeFieldName, out long minVersionCode)) return false;

        long versionCode = GetInstalledVersionCode();
        if (versionCode <= 0) return false;

        return versionCode < minVersionCode;
    }

    // Unity는 실행 중에 Version Code를 알려 주지 않으므로 설치된 패키지 정보에서 읽는다.
    private static long GetInstalledVersionCode()
    {
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
        using var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 0);
        // getLongVersionCode는 API 28부터다. 그 아래에서는 int 필드를 읽는다.
        using var buildVersion = new AndroidJavaClass("android.os.Build$VERSION");
        if (buildVersion.GetStatic<int>("SDK_INT") >= 28) return packageInfo.Call<long>("getLongVersionCode");

        return packageInfo.Get<int>("versionCode");
    }
#else
    // 에디터와 비 Android는 Firestore를 쓰지 않고 스토어 버전도 없다. 개발 전용 경로이므로 막지 않는다.
    private static UniTask<bool> IsUpdateRequiredCore()
    {
        return UniTask.FromResult(false);
    }
#endif
}
