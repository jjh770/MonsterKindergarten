using UnityEngine;

public static class TutorialProgress
{
    private const string KeySuffix = "_TutorialCompleted";

    private static string _completionKey;

    public static bool IsInitialized { get; private set; }
    public static bool IsCompleted { get; private set; }
    public static bool ShouldRun => IsInitialized && !IsCompleted;

    public static void Initialize(string userId, bool hasExistingProgress)
    {
        _completionKey = $"{userId}{KeySuffix}";
        IsInitialized = true;

        if (PlayerPrefs.HasKey(_completionKey))
        {
            bool wasCompleted = PlayerPrefs.GetInt(_completionKey, 0) == 1;
            IsCompleted = wasCompleted || hasExistingProgress;

            if (!wasCompleted && IsCompleted)
            {
                SaveCompletionFlag();
            }

            return;
        }

        // 튜토리얼 도입 이전의 저장 데이터는 완료한 사용자로 간주한다.
        IsCompleted = hasExistingProgress;
        SaveCompletionFlag();
    }

    public static void MarkCompleted()
    {
        if (!IsInitialized || IsCompleted) return;

        IsCompleted = true;
        SaveCompletionFlag();
    }

    private static void SaveCompletionFlag()
    {
        PlayerPrefs.SetInt(_completionKey, IsCompleted ? 1 : 0);
        PlayerPrefs.Save();
    }
}
