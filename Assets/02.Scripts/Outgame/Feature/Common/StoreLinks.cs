using UnityEngine;

// 이 앱의 Play 스토어 페이지를 연다. 저장된 진행도가 앱보다 새 버전이라 들어갈 수 없을 때 업데이트로 보낸다.
//
// Android에서는 market:// 주소로 Play 스토어 앱을 바로 연다. 스토어 앱이 없거나 열지 못하면 같은 페이지의
// 웹 주소로 대신 연다. Application.OpenURL은 실패를 알려 주지 않으므로, 스토어 앱은 인텐트로 열어 예외로 가른다.
// 비공개 테스트 트랙의 테스터도 같은 페이지에서 테스트 버전 업데이트를 받는다.
public static class StoreLinks
{
    public static string WebUrl => "https://play.google.com/store/apps/details?id=" + Application.identifier;

    public static void OpenStorePage()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (TryOpenPlayStoreApp()) return;
#endif
        Application.OpenURL(WebUrl);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool TryOpenPlayStoreApp()
    {
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var uriClass = new AndroidJavaClass("android.net.Uri");
            using var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "market://details?id=" + Application.identifier);
            using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", uri);
            intent.Call<AndroidJavaObject>("setPackage", "com.android.vending");
            // FLAG_ACTIVITY_NEW_TASK
            intent.Call<AndroidJavaObject>("addFlags", 0x10000000);
            activity.Call("startActivity", intent);
            return true;
        }
        catch (AndroidJavaException e)
        {
            Debug.LogWarning($"Play 스토어 앱을 열지 못해 웹 페이지로 엽니다. : {e.Message}");
            return false;
        }
    }
#endif
}
