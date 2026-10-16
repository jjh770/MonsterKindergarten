using UnityEditor;
using UnityEngine;

// 탐험 구현 전에도 사용자가 Editor 로컬 계정에서 배치·복원을 확인할 수 있게 한다.
// Editor 폴더에 있어 Android 플레이어에는 포함되지 않는다.
public static class DecorationPreviewGrant
{
    private const string MenuPath = "Tools/Decorations/Grant Test Decorations (Editor Local Save)";

    [MenuItem(MenuPath)]
    private static void Grant()
    {
        if (!CanGrant()) return;

        SlimeManager manager = SlimeManager.Instance;
        int granted = 0;
        for (EPlaygroundObjectType type = EPlaygroundObjectType.FlowerPatch;
             type <= EPlaygroundObjectType.FireflyLantern;
             type++)
        {
            if (manager.TryGrantDecoration(type)) granted++;
        }

        Debug.Log($"Editor 로컬 계정에 테스트 장식 {granted}종을 지급했어요. 이미 보유한 장식은 다시 지급하지 않아요.");
    }

    [MenuItem(MenuPath, true)]
    private static bool CanGrant()
    {
        return EditorApplication.isPlaying &&
               SlimeManager.Instance != null &&
               SlimeManager.Instance.IsInitialized &&
               !SaveDataLoadGuard.HasFailure;
    }
}
