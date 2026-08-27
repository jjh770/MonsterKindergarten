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

    public void Merge(SlimeController keeper, SlimeController removed)
    {
        if (!SlimeManager.Instance.CanMerge(keeper.Slime, removed.Slime)) return;

        ESlimeGrade fromGrade = keeper.Grade;
        ESlimeGrade toGrade = fromGrade + 1;

        Slime nextSlime = SlimeManager.Instance.Get(toGrade);
        SlimeManager.Instance.TryUpdateHighestLevel(toGrade);
        SlimeManager.Instance.MergeSlime(
            keeper.InstanceId,
            removed.InstanceId,
            toGrade);
        keeper.PromoteTo(nextSlime);

        SpawnManager.Instance.Despawn(removed);
        Merged?.Invoke(keeper, fromGrade, toGrade);
    }
}
