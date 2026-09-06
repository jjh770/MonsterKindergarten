using System;
using UnityEngine;

public class MergeManager : MonoBehaviour
{
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
        if (!SlimeManager.Instance.CanMerge(keeper.Slime, removed.Slime)) return;

        ESlimeGrade fromGrade = keeper.Grade;
        ESlimeGrade toGrade = fromGrade + 1;

        Slime nextSlime = SlimeManager.Instance.Get(toGrade);
        if (nextSlime == null) return;

        try
        {
            SlimeManager.Instance.MergeSlime(
                keeper.InstanceId,
                removed.InstanceId,
                toGrade);
        }
        catch (Exception e) when (e is InvalidOperationException ||
                                  e is ArgumentException)
        {
            Debug.LogWarning($"슬라임을 합성할 수 없습니다: {e.Message}");
            return;
        }

        SlimeManager.Instance.TryUpdateHighestLevel(toGrade);
        keeper.PromoteTo(nextSlime);

        SpawnManager.Instance.Despawn(removed);
        Merged?.Invoke(keeper, fromGrade, toGrade);
    }
}
