using UnityEngine;

public static class ApplicationSettings
{
    private const int AndroidTargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Application.targetFrameRate = AndroidTargetFrameRate;
#endif
    }
}
