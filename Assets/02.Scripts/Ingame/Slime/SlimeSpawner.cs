using Lean.Pool;
using System.Collections.Generic;
using UnityEngine;

public class SlimeSpawner : MonoBehaviour
{
    public static SlimeSpawner Instance { get; private set; }

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

    public SlimeController Spawn(ESlimeGrade slimeGrade, Vector2 position, bool shouldSave = true)
    {
        GameObject slimeObject = _pool.Spawn(position, Quaternion.identity);

        SlimeController slimeController = slimeObject.GetComponent<SlimeController>();
        Slime startSlime = SlimeManager.Instance.Get(slimeGrade);

        slimeController.SetSlime(startSlime);
        SlimeManager.Instance.TryUpdateHighestLevel(startSlime.SpecData.Grade);
        slimeController.OnSpawn();

        _activeTargets.Add(slimeController);

        // 새로 스폰된 슬라임만 저장 (초기 로드 시에는 저장하지 않음)
        if (shouldSave)
        {
            SlimeManager.Instance.AddSlime(slimeGrade);
        }

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
