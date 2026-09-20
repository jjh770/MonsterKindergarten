using System;
using UnityEngine;

public class MergeManager : MonoBehaviour
{
    public readonly struct MergeTargetPair
    {
        public SlimeController Keeper { get; }
        public SlimeController Removed { get; }

        public MergeTargetPair(SlimeController keeper, SlimeController removed)
        {
            Keeper = keeper;
            Removed = removed;
        }
    }

    public static MergeManager Instance { get; private set; }
    public static event System.Action<SlimeController, ESlimeGrade, ESlimeGrade> Merged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 저장 상태를 먼저 옮기고, 성공한 뒤에만 최고 등급과 화면을 따라가게 한다.
    //
    // MergeSlime()은 저장 상태에 없는 개체나 어긋난 등급에서 예외를 던진다.
    // 최고 등급 갱신을 앞에 두면 그 예외가 났을 때 저장에는 등급이 올라가 있고
    // 화면에는 두 마리가 남아 서로 어긋난다. 되돌릴 수 없는 갱신을 검증 뒤로 미루면
    // 실패해도 아무것도 바뀌지 않으므로 롤백이 필요 없다.
    // StageManager.TryRelocateSlime()과 같은 처리 방식이다.
    public void Merge(SlimeController keeper, SlimeController removed)
    {
        MergeBatch(new[] { new MergeTargetPair(keeper, removed) });
    }

    // 실제로 합성한 쌍이 있으면 true. 자동 합성은 이 결과로 다음 등급을 시도할지
    // 정하므로, 저장이 거절한 쌍 하나가 뒤의 멀쩡한 쌍까지 막지 않는다.
    public bool MergeBatch(
        System.Collections.Generic.IReadOnlyList<MergeTargetPair> pairs)
    {
        if (pairs == null || pairs.Count == 0 || SlimeManager.Instance == null) return false;

        var validPairs = new System.Collections.Generic.List<MergeTargetPair>(pairs.Count);
        var requests = new System.Collections.Generic.List<SlimeMergeRequest>(pairs.Count);
        foreach (MergeTargetPair pair in pairs)
        {
            if (pair.Keeper == null || pair.Removed == null ||
                !SlimeManager.Instance.CanMerge(pair.Keeper.Slime, pair.Removed.Slime))
            {
                continue;
            }

            ESlimeGrade toGrade = pair.Keeper.Grade + 1;
            if (SlimeManager.Instance.Get(toGrade) == null) continue;

            validPairs.Add(pair);
            requests.Add(new SlimeMergeRequest(
                pair.Keeper.InstanceId,
                pair.Removed.InstanceId,
                toGrade));
        }

        if (requests.Count == 0) return false;

        try
        {
            SlimeManager.Instance.MergeSlimesBatch(requests);
        }
        catch (Exception e) when (e is InvalidOperationException ||
                                  e is ArgumentException)
        {
            Debug.LogWarning($"슬라임을 합성할 수 없습니다: {e.Message}");
            return false;
        }

        foreach (MergeTargetPair pair in validPairs)
        {
            ESlimeGrade fromGrade = pair.Keeper.Grade;
            ESlimeGrade toGrade = fromGrade + 1;
            Slime nextSlime = SlimeManager.Instance.Get(toGrade);
            pair.Keeper.PromoteTo(nextSlime);
            SpawnManager.Instance.Despawn(pair.Removed);
            Merged?.Invoke(pair.Keeper, fromGrade, toGrade);
        }

        return true;
    }
}
