using DG.Tweening;
using Lean.Pool;
using System;
using System.Collections.Generic;
using UnityEngine;
public class SlimeSpawner : MonoBehaviour
{
    public static SlimeSpawner Instance { get; private set; }

    [SerializeField] private float _dropHeight = 3f;
    [SerializeField] private float _dropDuration = 0.5f;
    [SerializeField] private Ease _dropEase = Ease.OutBounce;

    public event Action<int> OnHighestLevelChanged;
    public int HighestLevel { get; private set; } = 1;

    private LeanGameObjectPool _pool;
    private List<Slime> _activeTargets = new List<Slime>();

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

    public Slime Spawn(Vector2 position)
    {
        Vector2 startPosition = new Vector2(position.x, position.y + _dropHeight);
        GameObject slimeObject = _pool.Spawn(startPosition, Quaternion.identity);
        Slime slime = slimeObject.GetComponent<Slime>();
        slime.OnSpawn();
        slime.OnLevelChanged += OnSlimeLevelChanged;

        // 위에서 떨어지는 효과
        slimeObject.transform.DOMoveY(position.y, _dropDuration).SetEase(_dropEase);

        _activeTargets.Add(slime);
        return slime;
    }

    public void Despawn(Slime target)
    {
        if (target == null) return;

        int despawnedLevel = target.Level;
        target.OnLevelChanged -= OnSlimeLevelChanged;
        target.OnDespawn();
        _activeTargets.Remove(target);
        _pool.Despawn(target.gameObject);

        if (despawnedLevel >= HighestLevel)
        {
            RecalculateHighestLevel();
        }
    }

    private void OnSlimeLevelChanged(int level)
    {
        if (level > HighestLevel)
        {
            HighestLevel = level;
            OnHighestLevelChanged?.Invoke(HighestLevel);
        }
    }

    private void RecalculateHighestLevel()
    {
        int highest = 1;
        foreach (var slime in _activeTargets)
        {
            if (slime != null && slime.Level > highest)
            {
                highest = slime.Level;
            }
        }

        if (highest != HighestLevel)
        {
            HighestLevel = highest;
            OnHighestLevelChanged?.Invoke(HighestLevel);
        }
    }

    public int GetActiveCount() => _activeTargets.Count;

    public List<Slime> GetActiveTargets() => _activeTargets;
}
