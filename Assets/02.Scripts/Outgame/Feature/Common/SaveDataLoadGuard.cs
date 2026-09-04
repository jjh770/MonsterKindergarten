using System;
using UnityEngine;

// 저장 데이터를 확인하지 못한 세션을 잠근다.
//
// 한 도메인이라도 읽기에 실패하면 그 세션은 저장된 진행도를 모르는 상태다.
// 그대로 플레이하면 첫 저장이 남아 있던 원본을 덮어쓰므로, 실패를 확인하는
// 즉시 저장을 막고 게임에 들어가지 않는다.
//
// 매니저마다 씬 전환을 호출하면 세 번 겹치므로, 신고는 여기로 모으고
// 실제 처리는 GameManager 한 곳이 맡는다.
public static class SaveDataLoadGuard
{
    public static bool HasFailure { get; private set; }
    public static ESaveLoadFailure Failure { get; private set; }

    // 첫 실패에만 발화한다. 뒤따르는 실패는 안내 문구를 바꾸지 않는다.
    public static event Action Failed;

    public static void Report(ESaveLoadFailure failure, string context)
    {
        if (failure == ESaveLoadFailure.None)
        {
            throw new ArgumentException(
                "실패가 아닌 결과는 신고할 수 없습니다.", nameof(failure));
        }

        // 저장은 첫 실패 즉시 막는다. 이미 잠긴 뒤에도 다시 확인해 둔다.
        GameplaySaveGate.SetSavingEnabled(false);
        Debug.LogError($"[SaveDataLoadGuard] 저장 데이터를 불러오지 못했습니다. : {context}");

        if (HasFailure) return;

        HasFailure = true;
        Failure = failure;
        Failed?.Invoke();
    }

    // 로그인 화면에서만 호출한다. 게임 세션이 없는 곳에서 잠금을 푼다.
    public static void Clear()
    {
        HasFailure = false;
        Failure = ESaveLoadFailure.None;
        GameplaySaveGate.SetSavingEnabled(true);
    }
}
