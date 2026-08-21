using UnityEngine;

public class MergeManager : MonoBehaviour
{
    public static MergeManager Instance { get; private set; }
    public static event System.Action<SlimeController, ESlimeGrade, ESlimeGrade> Merged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void Merge(SlimeController keeper, SlimeController removed)
    {
        if (!SlimeManager.Instance.CanMerge(keeper.Slime, removed.Slime)) return;

        ESlimeGrade fromGrade = keeper.Grade;
        ESlimeGrade toGrade = fromGrade + 1;

        Slime nextSlime = SlimeManager.Instance.Get(toGrade);
        keeper.PromoteTo(nextSlime);

        SlimeManager.Instance.TryUpdateHighestLevel(toGrade);
        SlimeManager.Instance.MergeSlime(fromGrade, toGrade);

        SpawnManager.Instance.Despawn(removed);
        Merged?.Invoke(keeper, fromGrade, toGrade);
    }
}
