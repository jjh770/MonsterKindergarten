using Lean.Pool;
using System.Collections.Generic;
using UnityEngine;

public class SlimeSpawner : MonoBehaviour
{
    public static SlimeSpawner Instance { get; private set; }

    private LeanGameObjectPool _pool;
    private List<SlimeController> _activeTargets = new List<SlimeController>();
    public event System.Action<SlimeController> Spawned;

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

    public SlimeController Spawn(ESlimeGrade slimeGrade, Vector2 position, bool shouldSave = true)
    {
        return Spawn(
            SlimeInstance.Create(slimeGrade),
            position,
            shouldSave);
    }

    public SlimeController Restore(SlimeInstance instance, Vector2 position)
    {
        return Spawn(instance, position, shouldSave: false);
    }

    private SlimeController Spawn(
        SlimeInstance instance,
        Vector2 position,
        bool shouldSave)
    {
        if (instance == null) return null;

        ESlimeGrade slimeGrade = instance.Grade;
        Slime startSlime = SlimeManager.Instance.Get(slimeGrade);
        if (startSlime == null) return null;

        GameObject slimeObject = _pool.Spawn(position, Quaternion.identity);

        SlimeController slimeController = slimeObject.GetComponent<SlimeController>();
        slimeController.Bind(startSlime, instance);
        SlimeManager.Instance.TryUpdateHighestLevel(startSlime.SpecData.Grade);
        slimeController.OnSpawn();

        _activeTargets.Add(slimeController);

        // 새로 스폰된 슬라임만 저장 (초기 로드 시에는 저장하지 않음)
        if (shouldSave)
        {
            SlimeManager.Instance.AddSlime(instance);
        }

        Spawned?.Invoke(slimeController);

        return slimeController;
    }

    public void Despawn(SlimeController target)
    {
        if (target == null) return;

        target.OnDespawn();
        _activeTargets.Remove(target);
        _pool.Despawn(target.gameObject);
    }

    public int GetActiveCount() => _activeTargets.Count;

    public List<SlimeController> GetActiveTargets() => _activeTargets;
}
