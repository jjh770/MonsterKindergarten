using UnityEditor;
using UnityEngine;

// 탐험 구현 전에도 사용자가 Editor 로컬 계정에서 배치·복원을 확인할 수 있게 한다.
// Editor 폴더에 있어 Android 플레이어에는 포함되지 않는다.
public static class DecorationPreviewGrant
{
    private const string MenuPath = "Tools/Decorations/Grant Test Decorations (Editor Local Save)";
    private const string UnlockAllPath = "Tools/Decorations/Unlock All Objects";
    private const string LockAllPath = "Tools/Decorations/Lock All Objects";

    // 범퍼와 대포를 포함한 소품 6종을 모두 하나씩 가진 상태로 만든다. 이미 가진 것은 그대로 둔다.
    [MenuItem(UnlockAllPath)]
    private static void UnlockAll()
    {
        if (!CanGrant()) return;
        int changed = SlimeManager.Instance.EditorSetAllPlaygroundObjects(true);
        Debug.Log(changed > 0
            ? $"소품 {changed}종을 새로 열었어요. 이제 소품 6종을 모두 가지고 있어요."
            : "이미 소품을 모두 가지고 있거나 지금은 바꿀 수 없어요.");
    }

    // 소품을 하나도 없는 상태로 되돌린다. 장식장에 놓인 소품도 함께 치운다.
    [MenuItem(LockAllPath)]
    private static void LockAll()
    {
        if (!CanGrant()) return;
        int changed = SlimeManager.Instance.EditorSetAllPlaygroundObjects(false);
        Debug.Log(changed > 0
            ? $"소품 {changed}종을 잠갔어요. 놓여 있던 소품도 모두 치웠어요."
            : "이미 가진 소품이 없거나 지금은 바꿀 수 없어요.");
    }

    [MenuItem(MenuPath)]
    private static void Grant()
    {
        if (!CanGrant()) return;

        SlimeManager manager = SlimeManager.Instance;
        int granted = 0;
        for (EPlaygroundObjectType type = PlaygroundRules.FirstDecoration;
             type <= PlaygroundRules.LastDecoration;
             type++)
        {
            if (manager.TryGrantDecoration(type)) granted++;
        }

        Debug.Log($"Editor 로컬 계정에 테스트 장식 {granted}종을 지급했어요. 이미 보유한 장식은 다시 지급하지 않아요.");
    }

    [MenuItem(MenuPath, true)]
    [MenuItem(UnlockAllPath, true)]
    [MenuItem(LockAllPath, true)]
    private static bool CanGrant()
    {
        return EditorApplication.isPlaying &&
               SlimeManager.Instance != null &&
               SlimeManager.Instance.IsInitialized &&
               !SaveDataLoadGuard.HasFailure;
    }
}
