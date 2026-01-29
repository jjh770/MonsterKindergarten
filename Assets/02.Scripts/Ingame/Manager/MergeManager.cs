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

    public bool CanMerge(Slime target1, Slime target2)
    {
        return target1 != null && target2 != null && target1 != target2 && target1.Level == target2.Level && target1.Level < _maxLevel;
    }

    public void Merge(Slime keeper, Slime removed)
    {
        if (!CanMerge(keeper, removed)) return;

        keeper.LevelUp();
        keeper.transform.DOPunchScale(Vector3.one, 1f, 10, 1);

        SpawnManager.Instance.Despawn(removed);
    }
}
