using DG.Tweening;
using Lean.Pool;
using System.Collections.Generic;
using UnityEngine;
public class SlimeSpawner : MonoBehaviour
{
    public static SlimeSpawner Instance { get; private set; }

    [SerializeField] private float _dropHeight = 3f;
    [SerializeField] private float _dropDuration = 0.5f;
    [SerializeField] private Ease _dropEase = Ease.OutBounce;

    private LeanGameObjectPool _pool;
    private List<SlimeController> _activeTargets = new List<SlimeController>();

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

        _pool = GetComponent<LeanGameObjectPool>();
    }

    public SlimeController Spawn(ESlimeGrade slimeGrade, Vector2 position)
    {
        Vector2 startPosition = new Vector2(position.x, position.y + _dropHeight);
        GameObject slimeObject = _pool.Spawn(startPosition, Quaternion.identity);

        SlimeController slimeController = slimeObject.GetComponent<SlimeController>();
        Slime startSlime = SlimeManager.Instance.Get(slimeGrade);

        slimeController.SetSlime(startSlime);
        SlimeManager.Instance.TryUpdateHighestLevel(startSlime.SpecData.Grade);
        slimeController.OnSpawn();

        // 위에서 떨어지는 효과
        slimeObject.transform.DOMoveY(position.y, _dropDuration).SetEase(_dropEase);

        _activeTargets.Add(slimeController);
        return slimeController;
    }

    public void Despawn(SlimeController target)
    {
        if (target == null) return;

        ESlimeGrade despawnedGrade = target.Grade;
        target.OnDespawn();
        _activeTargets.Remove(target);
        _pool.Despawn(target.gameObject);
    }

    public int GetActiveCount() => _activeTargets.Count;

    public List<SlimeController> GetActiveTargets() => _activeTargets;
}
