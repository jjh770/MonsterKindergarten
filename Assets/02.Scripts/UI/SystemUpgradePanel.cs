using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private UpgradeManager _upgradeManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private MessagePopupUI _messagePopup;
    [Tooltip("업그레이드 성공을 손가락이 닿은 자리에서 터뜨리는 효과입니다.")]
    [SerializeField] private UpgradeTouchBurst _touchBurst;

    private readonly Dictionary<EUpgradeType, Upgrade> _upgrades = new();
    private readonly List<EUpgradeType> _orderedTypes = new();
    private bool _isInitialized;

    public RectTransform TutorialTarget => transform as RectTransform;
    public RectTransform SelectedItemTarget => _carousel?.CenterTarget;
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
        if (_carousel == null || _gameManager == null || _upgradeManager == null ||
            _slimeManager == null || _spawnManager == null || _messagePopup == null ||
            _touchBurst == null)
        {
            Debug.LogError("시스템 업그레이드 캐러셀 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        if (!_carousel.IsReady) return;

        _carousel.SlotBinding += BindSlot;
        _carousel.CenterPressed += OnCenterPressed;
        _carousel.RotationCompleted += OnRotationCompleted;

        _gameManager.AllDataInitialized += OnAllDataInitialized;
        _upgradeManager.DataChanged += Refresh;
        _slimeManager.HighestGradeChanged += OnHighestGradeChanged;
        _slimeManager.NormalCollectionCountChanged += OnNormalCollectionCountChanged;
        TutorialManager.Started += OnTutorialAvailabilityChanged;
        TutorialManager.Finished += OnTutorialAvailabilityChanged;

        _spawnManager.OnSpawnIntervalChanged += OnSpawnIntervalChanged;
        _spawnManager.OnSpawnMaxChanged += OnSpawnMaxChanged;


        if (_gameManager.IsAllDataInitialized)
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

        _gameManager.AllDataInitialized -= OnAllDataInitialized;
        _upgradeManager.DataChanged -= Refresh;
        _slimeManager.HighestGradeChanged -= OnHighestGradeChanged;
        _slimeManager.NormalCollectionCountChanged -= OnNormalCollectionCountChanged;
        TutorialManager.Started -= OnTutorialAvailabilityChanged;
        TutorialManager.Finished -= OnTutorialAvailabilityChanged;

        if (_spawnManager != null)
        {
            _spawnManager.OnSpawnIntervalChanged -= OnSpawnIntervalChanged;
            _spawnManager.OnSpawnMaxChanged -= OnSpawnMaxChanged;
        }

    }

    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        CacheSystemUpgrades();
        Refresh();
    }

    private void CacheSystemUpgrades()
    {
        // 재구성 전 선택 타입을 기억한다. 위치 인덱스는 목록이 바뀌면 다른 타입을 가리킨다.
        EUpgradeType? previousType =
            _orderedTypes.Count > 0
                ? _orderedTypes[_carousel.SelectedIndex]
                : (EUpgradeType?)null;

        _upgrades.Clear();
        _orderedTypes.Clear();

        foreach (Upgrade upgrade in _upgradeManager.GetSystemUpgrades())
        {
            EUpgradeType type = upgrade.SpecData.Type;
            if (!SystemUpgradeVisibility.IsShown(type, _slimeManager)) continue;

            _upgrades[type] = upgrade;
            _orderedTypes.Add(type);
        }

        _orderedTypes.Sort((left, right) => ((int)left).CompareTo((int)right));
        _carousel.SetDataCount(_orderedTypes.Count);

        int restored = ResolveRestoredIndex(
            previousType, _orderedTypes, _carousel.SelectedIndex);
        _carousel.SetSelectedIndexImmediate(restored);
    }

    // 선택 타입 보존 규칙. 순수 로직이라 List<EUpgradeType>와 인덱스만으로 검증 가능하다.
    private static int ResolveRestoredIndex(
        EUpgradeType? previousType,
        List<EUpgradeType> orderedTypes,
        int clampedIndex)
    {
        if (previousType.HasValue)
        {
            int found = orderedTypes.IndexOf(previousType.Value);
            if (found >= 0) return found;
        }

        return clampedIndex;
    }

    private void Refresh()
    {
        if (!_isInitialized) return;

        _carousel.Rebuild();
    }

    // 카루셀이 슬롯마다 되묻는다. 무엇을 실을지만 답한다.
    private void BindSlot(SystemUpgradeItemUI item, int dataIndex)
    {
        if (dataIndex < 0 || dataIndex >= _orderedTypes.Count) return;

        EUpgradeType type = _orderedTypes[dataIndex];
        item.Bind(type);

        if (!_upgrades.TryGetValue(type, out Upgrade upgrade)) return;

        bool isLocked = _upgradeManager.IsLockedByProgress(upgrade);
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
        if (_upgradeManager.IsLockedByProgress(upgrade)) return;

        bool upgraded = _upgradeManager.TryLevelUp(
            type,
            ESlimeGrade.None);
        if (upgraded)
        {
            _touchBurst.Play(GetBurstOrigin());
        }
        else if (!upgrade.IsMaxLevel)
        {
            _messagePopup.Show();
        }
    }

    // 손가락이 닿은 자리다. 버튼은 뗄 때 눌리지만 손가락은 거의 그 자리에 있다.
    // 포인터가 없으면 가운데 카드에서 터뜨린다.
    private Vector2 GetBurstOrigin()
    {
        Pointer pointer = Pointer.current;
        if (pointer != null) return pointer.position.ReadValue();

        return RectTransformUtility.WorldToScreenPoint(null, _carousel.CenterTarget.position);
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

    private bool IsMax(Upgrade upgrade)
    {
        if (upgrade.IsMaxLevel) return true;

        return upgrade.SpecData.Type == EUpgradeType.SpawnTimeSub &&
               _spawnManager.SpawnInterval <= _spawnManager.MinSpawnInterval;
    }

    private string BuildValueText(
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
                $"{icon}{_spawnManager.SpawnInterval:F1} → " +
                $"{Mathf.Max(_spawnManager.MinSpawnInterval, _spawnManager.SpawnInterval - (float)modifierIncrease):F1}",
            EUpgradeType.MaxCountAdd =>
                $"{icon}{_spawnManager.MaxActiveCount} → " +
                $"{_spawnManager.MaxActiveCount + Mathf.RoundToInt((float)modifierIncrease)}",
            EUpgradeType.HigherGradeSpawnWeightAdd =>
                IsNextSpawnGradeUnlock(upgrade.Level)
                    ? $"{icon}상위 슬라임 추가!"
                    : $"{icon}Lv.{upgrade.Level} → Lv.{upgrade.Level + 1}",
            EUpgradeType.AutoMergePairAdd =>
                BuildAutoMergeValueText(icon, upgrade.Level),
            EUpgradeType.AllSlimePointPercentAdd =>
                $"{icon}배율 {upgrade.Point:0.#}% → {upgrade.NextPoint:0.#}%",
            _ => $"{icon}{upgrade.Point:N0} → {upgrade.NextPoint:N0}",
        };
    }

    private static string BuildAutoMergeValueText(string icon, int currentLevel)
    {
        int currentPairCount = AutoMergeManager.GetPairCountForLevel(currentLevel);
        int nextPairCount = AutoMergeManager.GetPairCountForLevel(currentLevel + 1);
        return $"{icon}한 번에 {currentPairCount}쌍 → {nextPairCount}쌍";
    }

    private bool IsNextSpawnGradeUnlock(int currentUpgradeLevel)
    {
        return _slimeManager.IsSpawnCapRaisedAtNextLevel(currentUpgradeLevel);
    }

    private string BuildCostText(
        Upgrade upgrade,
        bool isMax,
        bool isLocked)
    {
        if (isLocked)
        {
            if (upgrade.SpecData.Type == EUpgradeType.HigherGradeSpawnWeightAdd &&
                _slimeManager != null)
            {
                ESlimeGrade requiredGrade =
                    _slimeManager.GetRequiredHighestGradeForSpawnTier(
                        upgrade.Level);
                return $"최고 Lv.{(int)requiredGrade} 해금 필요";
            }

            return $"레벨 {(int)UnlockGrades.MaxCountExpansion} 해금 필요";
        }

        if (isMax)
        {
            return $"{CurrencyIcon.Point}MAX";
        }

        double cost = (double)upgrade.Cost;
        return $"{CurrencyIcon.Point}{cost.ToFormattedString()}";
    }
}
