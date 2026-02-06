using DG.Tweening;
using UnityEngine;

public class MergeManager : MonoBehaviour
{
    public static MergeManager Instance { get; private set; }

    [SerializeField] private int _maxLevel = 10;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Merge(SlimeController keeper, SlimeController removed)
    {
        if (!SlimeManager.Instance.CanMerge(keeper.Slime, removed.Slime)) return;

        ESlimeGrade fromGrade = keeper.Slime.SpecData.Grade;
        ESlimeGrade toGrade = fromGrade + 1;

        Slime nextSlime = SlimeManager.Instance.Get(toGrade);
        keeper.SetSlime(nextSlime);
        keeper.transform.DOPunchScale(Vector3.one, 1f, 10, 1);

        SlimeManager.Instance.TryUpdateHighestLevel(toGrade);
        SlimeManager.Instance.MergeSlime(fromGrade, toGrade);

        SpawnManager.Instance.Despawn(removed);
    }
}
