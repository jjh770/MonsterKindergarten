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

    public bool CanMerge(ClickTarget target1, ClickTarget target2)
    {
        return target1 != null && target2 != null && target1 != target2 && target1.Level == target2.Level && target1.Level < _maxLevel;
    }

    public void Merge(ClickTarget keeper, ClickTarget removed)
    {
        if (!CanMerge(keeper, removed)) return;

        keeper.LevelUp();
        SpawnManager.Instance.Despawn(removed);
    }
}
