using UnityEngine;

// 분석 서비스를 한 곳에서 꺼내 쓰게 하는 진입점이다. 기획서 §21.10.
//
// 플랫폼 분기는 Create 한 곳에 있다. Android는 Firebase Analytics, 에디터는 로그만 남기고, 그 밖의 빌드는 보내지
// 않는다. 에디터의 로그 구현이 기기 동작을 대신하지 않게 하려는 것이다.
public static class Analytics
{
    private static IAnalyticsService s_service;

    public static IAnalyticsService Service => s_service ??= Create();

    // 동의 기록이 있을 때만 수집을 켠다. 동의하기 전과 계정 삭제로 동의가 지워진 뒤에는 끈다.
    // 앱이 시작할 때의 수집은 AppConsent.androidlib의 매니페스트가 꺼 둔다.
    public static void ApplyConsent()
    {
        Service.SetCollectionEnabled(ConsentRecord.HasConsented);
    }

    private static IAnalyticsService Create()
    {
#if UNITY_EDITOR
        return new LogAnalyticsService();
#elif UNITY_ANDROID
        return new FirebaseAnalyticsService();
#else
        return new LogAnalyticsService();
#endif
    }

    // 에디터와 지원하지 않는 플랫폼용이다. 이벤트를 보내지 않고 로그만 남긴다.
    private sealed class LogAnalyticsService : IAnalyticsService
    {
        public void SetCollectionEnabled(bool isEnabled)
        {
            Debug.Log($"[Analytics] 수집 {(isEnabled ? "켜짐" : "꺼짐")}");
        }

        public void LogAdEvent(string eventName, string placement, string extraKey = null, string extraText = null, long extraNumber = 0)
        {
            string extra = string.IsNullOrEmpty(extraKey) ? string.Empty : $" {extraKey}={(extraText ?? extraNumber.ToString())}";
            Debug.Log($"[Analytics] {eventName} placement={placement}{extra}");
        }
    }
}
