using Lean.Pool;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SlimeSpawner : MonoBehaviour
{
    public static SlimeSpawner Instance { get; private set; }

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
        GameObject slimeObject = _pool.Spawn(position, Quaternion.identity);
        Slime slime = slimeObject.GetComponent<Slime>();
        slime.OnSpawn();
        slime.OnLevelChanged += OnSlimeLevelChanged;

        _activeTargets.Add(slime);
        CheckHighestLevel();
        return slime;
    }

    public void Despawn(Slime target)
    {
        if (target == null) return;

        target.OnLevelChanged -= OnSlimeLevelChanged;
        target.OnDespawn();
        _activeTargets.Remove(target);
        _pool.Despawn(target.gameObject);
        CheckHighestLevel();
    }

    private void OnSlimeLevelChanged(int level)
    {
        CheckHighestLevel();
    }

    private void CheckHighestLevel()
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
