using System.Collections.Generic;
using UnityEngine;

public class AutoClicker : MonoBehaviour
{
    [SerializeField] private Vector3 _floaterOffset = new Vector3(0, 0.5f, 0);

    private Dictionary<ClickTarget, float> _timers = new Dictionary<ClickTarget, float>();
    private HashSet<ClickTarget> _activeTargetsCache = new HashSet<ClickTarget>();

    private void Update()
    {
        if (SpawnManager.Instance == null) return;

        var activeTargets = SpawnManager.Instance.GetActiveTargets();

        // 비활성화된 타겟 타이머 정리
        CleanupTimers(activeTargets);

        // 각 타겟별 자동 클릭 처리
        foreach (var target in activeTargets)
        {
            if (target == null || target.IsDragging) continue;

            float interval = target.AutoClickInterval;
            if (interval <= 0f) continue;

            // 타이머 초기화
            if (!_timers.ContainsKey(target))
            {
                _timers[target] = 0f;
            }

            _timers[target] += Time.deltaTime;

            if (_timers[target] >= interval)
            {
                _timers[target] = 0f;
                AutoClick(target);
            }
        }
    }

    private void AutoClick(ClickTarget target)
    {
        int point = target.Point;

        // 포인트 적립
        GameManager.Instance.AddPoint(point);

        // 포인트 플로팅만 표시
        if (PointFloaterPool.Instance != null)
        {
            Vector3 spawnPos = target.transform.position + _floaterOffset;
            PointFloater floater = PointFloaterPool.Instance.Spawn(spawnPos);
            floater.Play(point, spawnPos);
        }
    }

    private void CleanupTimers(List<ClickTarget> activeTargets)
    {
        // 매 프레임마다 HashSet을 생성하지 않고 미리 캐싱해둔 HashSet을 재사용
        _activeTargetsCache.Clear();
        foreach (var target in activeTargets)
        {
            // List를 HashSet으로 변환하여 O(1) 탐색 효율 확보
            _activeTargetsCache.Add(target);
        }

        var keysToRemove = new List<ClickTarget>();

        foreach (var key in _timers.Keys)
        {
            if (key == null || !_activeTargetsCache.Contains(key))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _timers.Remove(key);
        }
    }

    public void ResetTimer(ClickTarget target)
    {
        if (_timers.ContainsKey(target))
        {
            _timers[target] = 0f;
        }
    }
}
