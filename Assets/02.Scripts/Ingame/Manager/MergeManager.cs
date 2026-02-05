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
        // UI나 게임 오브젝트에는 규칙이 들어가면 안된다.
        // 규칙의 표현에 있어서만 로직이 들어간다.

        if (!SlimeManager.Instance.CanMerge(keeper.Slime, removed.Slime)) return;

        Slime nextSlime = SlimeManager.Instance.Get(keeper.Slime.SpecData.Grade + 1);
        keeper.SetSlime(nextSlime);
        keeper.transform.DOPunchScale(Vector3.one, 1f, 10, 1);
        SlimeManager.Instance.TryUpdateHighestLevel(nextSlime.SpecData.Grade);
        SpawnManager.Instance.Despawn(removed);
    }
}
