using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

// 유치원 업그레이드의 목록·구매·표기를 담당한다.
//
// 슬롯을 어떻게 돌리고 배치하는지는 SystemUpgradeCarousel이 갖는다. 그쪽은 항목이
// 무엇인지 모르고 이쪽은 배치 수학을 모른다. 둘을 한 파일에 두었을 때는 업그레이드
// 문구를 고치러 들어와도 트윈과 드래그 수학을 지나가야 했다.
//
// 카루셀은 같은 오브젝트에 있어야 한다. 드래그를 이 패널의 RectTransform으로 받고
// 슬롯 간격도 그 폭에서 끌어내기 때문이다.
public sealed class SystemUpgradePanel : MonoBehaviour
{
    [SerializeField] private SystemUpgradeCarousel _carousel;

    private readonly Dictionary<EUpgradeType, Upgrade> _upgrades = new();
    private readonly List<EUpgradeType> _orderedTypes = new();
    private bool _isInitialized;

    public RectTransform TutorialTarget => transform as RectTransform;
    public event Action RotationCompleted;

    public bool IsSelected(EUpgradeType type)
    {
        return _carousel != null &&
               _orderedTypes.Count > 0 &&
               _orderedTypes[_carousel.SelectedIndex] == type;
    }

    public bool TryFocus(EUpgradeType type)
    {
        return _carousel != null && _carousel.TryFocus(_orderedTypes.IndexOf(type));
    }

    private void Start()
    {
        if (_carousel == null)
        {
            Debug.LogError("시스템 업그레이드 캐러셀 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        if (!_carousel.IsReady) return;

        _carousel.SlotBinding += BindSlot;
        _carousel.CenterPressed += OnCenterPressed;
        _carousel.RotationCompleted += OnRotationCompleted;

        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        UpgradeManager.OnDataChanged += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;
        SlimeManager.OnNormalCollectionCountChanged += OnNormalCollectionCountChanged;
        TutorialManager.Started += OnTutorialAvailabilityChanged;
        TutorialManager.Finished += OnTutorialAvailabilityChanged;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged += OnSpawnIntervalChanged;
            SpawnManager.Instance.OnSpawnMaxChanged += OnSpawnMaxChanged;
        }

        if (AutoMergeManager.Instance != null)
        {
            AutoMergeManager.Instance.IntervalChanged += OnAutoMergeIntervalChanged;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        if (_carousel != null)
        {
            _carousel.SlotBinding -= BindSlot;
            _carousel.CenterPressed -= OnCenterPressed;
            _carousel.RotationCompleted -= OnRotationCompleted;
        }

        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        UpgradeManager.OnDataChanged -= Refresh;
        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
        SlimeManager.OnNormalCollectionCountChanged -= OnNormalCollectionCountChanged;
        TutorialManager.Started -= OnTutorialAvailabilityChanged;
        TutorialManager.Finished -= OnTutorialAvailabilityChanged;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged -= OnSpawnIntervalChanged;
            SpawnManager.Instance.OnSpawnMaxChanged -= OnSpawnMaxChanged;
        }

        if (AutoMergeManager.Instance != null)
        {
            AutoMergeManager.Instance.IntervalChanged -= OnAutoMergeIntervalChanged;
        }
    }

    private void OnAllDataInitialized()
    {
        if (UpgradeManager.Instance == null || SlimeManager.Instance == null) return;

        _isInitialized = true;
        CacheSystemUpgrades();
        Refresh();
    }

    private void CacheSystemUpgrades()
    {
        _upgrades.Clear();
        _orderedTypes.Clear();

        foreach (Upgrade upgrade in UpgradeManager.Instance.GetSystemUpgrades())
        {
            EUpgradeType type = upgrade.SpecData.Type;
            if (!SystemUpgradeVisibility.IsShown(type)) continue;

            _upgrades[type] = upgrade;
            _orderedTypes.Add(type);
        }

        _orderedTypes.Sort((left, right) => ((int)left).CompareTo((int)right));
        _carousel.SetDataCount(_orderedTypes.Count);
    }

    private void Refresh()
    {
        if (!_isInitialized || SpawnManager.Instance == null) return;

        _carousel.Rebuild();
    }

    // 카루셀이 슬롯마다 되묻는다. 무엇을 실을지만 답한다.
    private void BindSlot(SystemUpgradeItemUI item, int dataIndex)
    {
        if (dataIndex < 0 || dataIndex >= _orderedTypes.Count) return;

        EUpgradeType type = _orderedTypes[dataIndex];
        item.Bind(type);

        if (!_upgrades.TryGetValue(type, out Upgrade upgrade)) return;

        bool isLocked = UpgradeManager.Instance.IsLockedByProgress(upgrade);
        bool isMax = IsMax(upgrade);
        bool isDisabled = isMax || isLocked;
        item.Refresh(
            BuildValueText(upgrade, isMax, isLocked),
            BuildCostText(upgrade, isMax, isLocked),
            isDisabled,
            isDisabled ? (Currency?)null : upgrade.Cost);
    }

    private void OnCenterPressed(int dataIndex)
    {
        if (dataIndex < 0 || dataIndex >= _orderedTypes.Count) return;

        OnUpgradeRequested(_orderedTypes[dataIndex]);
    }

    private void OnRotationCompleted() => RotationCompleted?.Invoke();

    private void OnUpgradeRequested(EUpgradeType type)
    {
        if (!_upgrades.TryGetValue(type, out Upgrade upgrade)) return;
        if (UpgradeManager.Instance.IsLockedByProgress(upgrade)) return;

        if (!UpgradeManager.Instance.TryLevelUp(type, ESlimeGrade.None) &&
            !upgrade.IsMaxLevel)
        {
            MessagePopupUI.Instance?.Show();
        }
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        CacheSystemUpgrades();
        Refresh();
    }

    private void OnNormalCollectionCountChanged(int count)
    {
        CacheSystemUpgrades();
        Refresh();
    }

    private void OnTutorialAvailabilityChanged()
    {
        if (!_isInitialized) return;

        CacheSystemUpgrades();
        Refresh();
    }

    private void OnSpawnIntervalChanged(float interval, float minInterval)
    {
        Refresh();
    }

    private void OnSpawnMaxChanged(int maxCount)
    {
        Refresh();
    }

    private void OnAutoMergeIntervalChanged(float interval)
    {
        Refresh();
    }

    private static bool IsMax(Upgrade upgrade)
    {
        if (upgrade.IsMaxLevel) return true;

        return upgrade.SpecData.Type == EUpgradeType.SpawnTimeSub &&
               SpawnManager.Instance.SpawnInterval <= SpawnManager.Instance.MinSpawnInterval ||
               upgrade.SpecData.Type == EUpgradeType.AutoMergeTimeSub &&
               AutoMergeManager.Instance != null &&
               AutoMergeManager.Instance.Interval <= AutoMergeManager.MinimumInterval;
    }

    private static string BuildValueText(
        Upgrade upgrade,
        bool isMax,
        bool isLocked)
    {
        string icon = upgrade.SpecData.SystemIconIndex >= 0
            ? $"<sprite name=\"{upgrade.SpecData.SystemIconIndex:00}\">"
            : string.Empty;

        if (isLocked)
        {
            if (upgrade.SpecData.Type == EUpgradeType.HigherGradeSpawnWeightAdd &&
                IsNextSpawnGradeUnlock(upgrade.Level))
            {
                return $"{icon}상위 슬라임 추가!";
            }

            return string.Empty;
        }

        if (isMax)
        {
            return $"{icon}MAX";
        }

        double modifierIncrease = upgrade.NextPoint - upgrade.Point;

        return upgrade.SpecData.Type switch
        {
            EUpgradeType.SpawnTimeSub =>
                $"{icon}{SpawnManager.Instance.SpawnInterval:F1} → " +
                $"{Mathf.Max(SpawnManager.Instance.MinSpawnInterval, SpawnManager.Instance.SpawnInterval - (float)modifierIncrease):F1}",
            EUpgradeType.MaxCountAdd =>
                $"{icon}{SpawnManager.Instance.MaxActiveCount} → " +
                $"{SpawnManager.Instance.MaxActiveCount + Mathf.RoundToInt((float)modifierIncrease)}",
            EUpgradeType.HigherGradeSpawnWeightAdd =>
                IsNextSpawnGradeUnlock(upgrade.Level)
                    ? $"{icon}상위 슬라임 추가!"
                    : $"{icon}Lv.{upgrade.Level} → Lv.{upgrade.Level + 1}",
            EUpgradeType.AutoMergeTimeSub =>
                BuildAutoMergeValueText(icon, modifierIncrease, upgrade.Level),
            _ => $"{icon}{upgrade.Point:N0} → {upgrade.NextPoint:N0}",
        };
    }

    private static string BuildAutoMergeValueText(
        string icon,
        double modifierIncrease,
        int currentLevel)
    {
        int currentPairCount = AutoMergeManager.GetPairCountForLevel(currentLevel);
        int nextPairCount = AutoMergeManager.GetPairCountForLevel(currentLevel + 1);
        if (currentPairCount != nextPairCount)
        {
            return $"{icon}합성 {currentPairCount}쌍 → {nextPairCount}쌍";
        }

        float current = AutoMergeManager.Instance != null
            ? AutoMergeManager.Instance.Interval
            : AutoMergeManager.BaseInterval;
        float next = Mathf.Max(
            AutoMergeManager.MinimumInterval,
            current - (float)modifierIncrease);
        return $"{icon}{current:F1}초 → {next:F1}초";
    }

    private static bool IsNextSpawnGradeUnlock(int currentUpgradeLevel)
    {
        return SlimeManager.Instance != null &&
               SlimeManager.Instance.IsSpawnCapRaisedAtNextLevel(currentUpgradeLevel);
    }

    private static string BuildCostText(
        Upgrade upgrade,
        bool isMax,
        bool isLocked)
    {
        if (isLocked)
        {
            if (upgrade.SpecData.Type == EUpgradeType.HigherGradeSpawnWeightAdd &&
                SlimeManager.Instance != null)
            {
                ESlimeGrade requiredGrade =
                    SlimeManager.Instance.GetRequiredHighestGradeForSpawnTier(
                        upgrade.Level);
                return $"최고 Lv.{(int)requiredGrade} 해금 필요";
            }

            return $"레벨 {(int)UnlockGrades.SkyStage} 해금 필요";
        }

        if (isMax)
        {
            return $"{CurrencyIcon.Point}MAX";
        }

        double cost = (double)upgrade.Cost;
        return $"{CurrencyIcon.Point}{cost.ToFormattedString()}";
    }
}
