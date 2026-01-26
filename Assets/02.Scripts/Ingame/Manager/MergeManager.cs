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

    public bool CanMerge(ClickTarget target_1, ClickTarget target_2)
    {
        return target_1 != null && target_2 != null && target_1 != target_2 && target_1.Level == target_2.Level && target_1.Level < _maxLevel;
    }

    public void Merge(ClickTarget keeper, ClickTarget removed)
    {
        if (!CanMerge(keeper, removed)) return;

        keeper.LevelUp();
        Destroy(removed.gameObject);
    }
}
