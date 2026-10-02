using UnityEngine;

public static class ApplicationSettings
{
    public const int DefaultFrameRate = 60;
    public const int HighFrameRate = 120;

    private const string FrameRateKey = "Options_TargetFrameRate";

    // 기기에 저장하는 표시 설정이다. 계정 진행도가 아니므로 초기화와 계정 삭제가 지우지 않는다.
    public static int TargetFrameRate { get; private set; } = DefaultFrameRate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        TargetFrameRate = Normalize(PlayerPrefs.GetInt(FrameRateKey, DefaultFrameRate));
#if UNITY_ANDROID && !UNITY_EDITOR
        // vSync가 켜져 있으면 targetFrameRate가 무시될 수 있어 함께 끈다.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
#endif
    }

    public static void SetTargetFrameRate(int frameRate)
    {
        TargetFrameRate = Normalize(frameRate);
        PlayerPrefs.SetInt(FrameRateKey, TargetFrameRate);
        PlayerPrefs.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
        Application.targetFrameRate = TargetFrameRate;
#endif
    }

    private static int Normalize(int frameRate)
    {
        return frameRate >= HighFrameRate ? HighFrameRate : DefaultFrameRate;
    }
}
