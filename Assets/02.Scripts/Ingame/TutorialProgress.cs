using System.Collections.Generic;
using UnityEngine;

public static class TutorialIds
{
    public const string Main = "Tutorial";
    public const string HigherGradeSpawn = "HigherGradeSpawnTutorial";
    public const string DisplayRoom = "DisplayRoomTutorial";

    // 등록과 삭제가 같은 목록을 본다. 튜토리얼을 추가하면 여기만 늘린다.
    // 저장 키 형식이 바뀌는 변경을 할 때 해당 항목의 버전을 올린다.
    public static readonly (string Id, int Version)[] All =
    {
        (Main, 1),
        (HigherGradeSpawn, 1),
        (DisplayRoom, 1),
    };
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
        bool completeStoredIncomplete = true)
    {
        int version = GetVersion(tutorialId);
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

    public static void DeleteForUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        // 로비에서 중단된 초기화를 재개할 때는 등록된 메모리 상태가 없을 수 있다.
        // 등록과 같은 키 형식을 써야 버전이 올라간 튜토리얼도 함께 지워진다.
        foreach ((string id, int version) in TutorialIds.All)
        {
            PlayerPrefs.DeleteKey(BuildCompletionKey(userId, id, version));
        }

        if (s_userId != userId) return;

        foreach (TutorialState state in s_stateById.Values)
        {
            PlayerPrefs.DeleteKey(state.CompletionKey);
        }
        Initialize(null);
    }

    private static string BuildCompletionKey(string tutorialId, int version)
    {
        return BuildCompletionKey(s_userId, tutorialId, version);
    }

    private static string BuildCompletionKey(
        string userId,
        string tutorialId,
        int version)
    {
        string versionSuffix = version > 1 ? $"_v{version}" : string.Empty;
        return $"{userId}_{tutorialId}Completed{versionSuffix}";
    }

    private static int GetVersion(string tutorialId)
    {
        foreach ((string id, int version) in TutorialIds.All)
        {
            if (id == tutorialId) return version;
        }

        Debug.LogError($"TutorialIds.All에 없는 튜토리얼입니다. : {tutorialId}");
        return 1;
    }

    private static void SaveCompletionFlag(
        string completionKey,
        bool isCompleted)
    {
        PlayerPrefs.SetInt(completionKey, isCompleted ? 1 : 0);
        PlayerPrefs.Save();
    }
}
