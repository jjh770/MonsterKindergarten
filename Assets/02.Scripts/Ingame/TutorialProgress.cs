using System.Collections.Generic;
using UnityEngine;

public static class TutorialIds
{
    public const string Main = "Tutorial";
    public const string HigherGradeSpawn = "HigherGradeSpawnTutorial";
    public const string DisplayRoom = "DisplayRoomTutorial";
}

public static class TutorialProgress
{
    private struct TutorialState
    {
        public string CompletionKey;
        public bool IsCompleted;
    }

    private static readonly Dictionary<string, TutorialState> s_stateById =
        new Dictionary<string, TutorialState>();

    private static string s_userId;

    public static bool IsInitialized { get; private set; }

    public static void Initialize(string userId)
    {
        s_userId = userId;
        s_stateById.Clear();
        IsInitialized = !string.IsNullOrWhiteSpace(userId);
    }

    public static void Register(
        string tutorialId,
        bool completeByDefault,
        bool completeStoredIncomplete = true,
        int version = 1)
    {
        if (!IsInitialized)
        {
            Debug.LogError("사용자 ID 없이 튜토리얼 진행 상태를 초기화할 수 없습니다.");
            return;
        }

        string completionKey = BuildCompletionKey(tutorialId, version);
        if (PlayerPrefs.HasKey(completionKey))
        {
            bool wasCompleted = PlayerPrefs.GetInt(completionKey, 0) == 1;
            bool isCompleted = wasCompleted ||
                               (completeStoredIncomplete && completeByDefault);
            s_stateById[tutorialId] = new TutorialState
            {
                CompletionKey = completionKey,
                IsCompleted = isCompleted,
            };

            if (!wasCompleted && isCompleted)
            {
                SaveCompletionFlag(completionKey, isCompleted);
            }

            return;
        }

        s_stateById[tutorialId] = new TutorialState
        {
            CompletionKey = completionKey,
            IsCompleted = completeByDefault,
        };
        SaveCompletionFlag(completionKey, completeByDefault);
    }

    public static bool IsRegistered(string tutorialId)
    {
        return IsInitialized && s_stateById.ContainsKey(tutorialId);
    }

    public static bool IsCompleted(string tutorialId)
    {
        return IsRegistered(tutorialId) && s_stateById[tutorialId].IsCompleted;
    }

    public static bool ShouldRun(string tutorialId)
    {
        return IsRegistered(tutorialId) && !s_stateById[tutorialId].IsCompleted;
    }

    public static void MarkCompleted(string tutorialId)
    {
        if (!s_stateById.TryGetValue(
                tutorialId,
                out TutorialState tutorialState) ||
            tutorialState.IsCompleted)
        {
            return;
        }

        tutorialState.IsCompleted = true;
        s_stateById[tutorialId] = tutorialState;
        SaveCompletionFlag(
            tutorialState.CompletionKey,
            isCompleted: true);
    }

    private static string BuildCompletionKey(string tutorialId, int version)
    {
        string versionSuffix = version > 1 ? $"_v{version}" : string.Empty;
        return $"{s_userId}_{tutorialId}Completed{versionSuffix}";
    }

    private static void SaveCompletionFlag(
        string completionKey,
        bool isCompleted)
    {
        PlayerPrefs.SetInt(completionKey, isCompleted ? 1 : 0);
        PlayerPrefs.Save();
    }
}
