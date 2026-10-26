using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 사용자가 직접 실행하는 로컬 검증 메뉴다. Android 빌드에는 포함되지 않는다.
public static class ExpeditionPreviewTools
{
    private const string StartPath = "Tools/Expeditions/Start Local Test (10 seconds)";
    private const string ClaimPath = "Tools/Expeditions/Claim Local Test Reward";
    private const string RecoverPath = "Tools/Expeditions/Recover Local Settlement";

    private const string MediumPath = "Tools/Expeditions/Start Local Test (20 seconds)";
    private const string LongPath = "Tools/Expeditions/Start Local Test (30 seconds)";
    private const string FinishPath = "Tools/Expeditions/Finish All Now";

    [MenuItem(StartPath)]
    private static void StartShort() => StartTest("expedition-short", 10);
    [MenuItem(MediumPath)]
    private static void StartMedium() => StartTest("expedition-medium", 20);
    [MenuItem(LongPath)]
    private static void StartLong() => StartTest("expedition-long", 30);

    private static void StartTest(string destinationId, int seconds)
    {
        if (!CanUse()) return;
        string[] ids = SlimeManager.Instance.ActiveSlimes
            .Where(slime => slime.Location == ESlimeLocation.DisplayRoom &&
                            !SlimeManager.Instance.IsOnExpedition(slime.InstanceId))
            .Take(1).Select(slime => slime.InstanceId).ToArray();
        var service = new ExpeditionService(SlimeManager.Instance, CurrencyManager.Instance,
            duration: TimeSpan.FromSeconds(seconds));
        bool started = service.TryStart(destinationId, ids,
            new ExpeditionReward(100d, 1, EPlaygroundObjectType.FireflyLantern));
        Debug.Log(started
            ? $"Editor 로컬 테스트 탐험 {destinationId}를 1마리로 출발했어요. {seconds}초 뒤 수령하세요. 보상은 포인트 100·뽑기권 1·랜턴이에요."
            : "출발하지 못했어요. Lv.7 해금, 대기 중인 장식장 슬라임, 같은 탐험지 점유와 정산 상태를 확인하세요.");
    }

    // 진행 중인 탐험을 모두 지금 도착한 것으로 바꾼다. 실제 화면에서 보낸 탐험도 기다리지 않고 받을 수 있다.
    [MenuItem(FinishPath)]
    private static void FinishAllNow()
    {
        if (!CanUse()) return;
        int finished = SlimeManager.Instance.EditorFinishExpeditions(ServerClock.TrustedUtcNow);
        Debug.Log(finished > 0
            ? $"진행 중이던 탐험 {finished}개를 지금 도착한 것으로 바꿨어요. 탐험 화면에서 보상을 받을 수 있어요."
            : "진행 중인 탐험이 없거나 지금은 바꿀 수 없어요.");
    }

    [MenuItem(ClaimPath)]
    private static void ClaimTest()
    {
        if (!CanUse()) return;
        ExpeditionRun ready = SlimeManager.Instance.ActiveExpeditions
            .LastOrDefault(run => run.IsReadyToReturn(ServerClock.TrustedUtcNow));
        bool claimed = ready != null && CreateService().TryClaim(ready.RunId, out _);
        Debug.Log(claimed
            ? $"탐험 {ready.DestinationId} 보상을 받았어요. 남은 탐험: {SlimeManager.Instance.ActiveExpeditions.Count}개예요."
            : "도착 전이거나 수령할 탐험이 없어요. 정산 상태도 확인하세요.");
    }

    [MenuItem(RecoverPath)]
    private static void Recover()
    {
        if (!CanUse()) return;
        Debug.Log(CreateService().TryRecoverSettlement()
            ? "탐험 누적 지급 기록을 맞췄어요. 이미 지급한 재화는 다시 더하지 않아요."
            : "탐험 지급 기록을 맞출 수 없어요.");
    }

    [MenuItem(StartPath, true)]
    [MenuItem(MediumPath, true)]
    [MenuItem(LongPath, true)]
    [MenuItem(ClaimPath, true)]
    [MenuItem(FinishPath, true)]
    [MenuItem(RecoverPath, true)]
    private static bool CanUse() => EditorApplication.isPlaying && GameplayGate.IsReady &&
                                    SlimeManager.Instance != null && CurrencyManager.Instance != null &&
                                    GameplaySaveGate.IsSavingEnabled && !SaveDataLoadGuard.HasFailure;

    private static ExpeditionService CreateService() => new(
        SlimeManager.Instance, CurrencyManager.Instance, duration: TimeSpan.FromSeconds(10));
}
