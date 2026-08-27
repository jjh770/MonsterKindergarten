using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using Utility;

public sealed class SystemUpgradePanel : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private const int CenterSlotIndex = 2;
    private const int RequiredSlotCount = 5;

    [SerializeField] private SystemUpgradeItemUI[] _items;

    [Header("Carousel")]
    [SerializeField, Range(0.2f, 0.5f)] private float _sideOffsetRatio = 0.42f;
    [SerializeField, Range(0.5f, 1f)] private float _sideScale = 0.72f;
    [SerializeField, Range(0f, 1f)] private float _sideAlpha = 0.35f;
    // 중앙에서 멀어질수록 아래로 내려 뒤에서 올라오는 원근을 만든다.
    [SerializeField, Range(0f, 200f)] private float _sideDropDistance = 40f;
    [SerializeField, Min(0f)] private float _rotationDuration = 0.25f;
    [SerializeField, Range(0.1f, 0.5f)] private float _dragThresholdRatio = 0.2f;

    private sealed class CarouselSlot
    {
        public SystemUpgradeItemUI Item;
        public RectTransform Root;
        public CanvasGroup CanvasGroup;
    }

    private readonly Dictionary<EUpgradeType, Upgrade> _upgrades = new();
    private readonly List<EUpgradeType> _orderedTypes = new();
    private readonly List<CarouselSlot> _slots = new();
    private ESlimeGrade _highestGrade;
    private int _selectedIndex;
    private bool _isInitialized;
    private bool _isDragging;
    private Vector2 _dragStartPosition;
    private float _dragOffset;
    private Tween _rotationTween;

    public RectTransform TutorialTarget => transform as RectTransform;
    public event Action RotationCompleted;

    public bool IsSelected(EUpgradeType type)
    {
        return _orderedTypes.Count > 0 &&
               _orderedTypes[_selectedIndex] == type;
    }

    public bool TryFocus(EUpgradeType type)
    {
        if (_orderedTypes.Count == 0 || _rotationTween != null || _isDragging)
        {
            return false;
        }

        int targetIndex = _orderedTypes.IndexOf(type);
        if (targetIndex < 0) return false;
        if (targetIndex == _selectedIndex) return true;

        int forwardDistance = WrapIndex(targetIndex - _selectedIndex);
        int direction = forwardDistance <= _orderedTypes.Count / 2 ? 1 : -1;
        Rotate(direction);
        return true;
    }

    private void Start()
    {
        InitializeCarouselSlots();
        if (_slots.Count != RequiredSlotCount) return;

        foreach (CarouselSlot slot in _slots)
        {
            slot.Item.Pressed += OnItemPressed;
        }

        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        UpgradeManager.OnDataChanged += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged += OnSpawnIntervalChanged;
            SpawnManager.Instance.OnSpawnMaxChanged += OnSpawnMaxChanged;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDisable()
    {
        _rotationTween?.Kill();
        _rotationTween = null;
        _isDragging = false;
        _dragOffset = 0f;
        SetSlotRaycasts(true);
    }

    private void OnEnable()
    {
        if (_slots.Count == RequiredSlotCount)
        {
            RefreshCarouselSlots();
        }
    }

    private void OnDestroy()
    {
        _rotationTween?.Kill();

        foreach (CarouselSlot slot in _slots)
        {
            if (slot.Item != null)
            {
                slot.Item.Pressed -= OnItemPressed;
            }
        }

        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        UpgradeManager.OnDataChanged -= Refresh;
        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged -= OnSpawnIntervalChanged;
            SpawnManager.Instance.OnSpawnMaxChanged -= OnSpawnMaxChanged;
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_slots.Count == RequiredSlotCount &&
            _rotationTween == null &&
            !_isDragging)
        {
            ApplySlotLayouts();
        }
    }

    private void InitializeCarouselSlots()
    {
        _slots.Clear();

        if (_items == null || _items.Length != RequiredSlotCount)
        {
            Debug.LogError($"시스템 업그레이드 캐러셀에 {RequiredSlotCount}개 슬롯이 필요합니다.", this);
            enabled = false;
            return;
        }

        foreach (SystemUpgradeItemUI item in _items)
        {
            RectTransform root = item != null
                ? item.transform.parent as RectTransform
                : null;
            if (root == null)
            {
                Debug.LogError("시스템 업그레이드 캐러셀 슬롯 구성이 올바르지 않습니다.", this);
                enabled = false;
                _slots.Clear();
                return;
            }

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                Debug.LogError($"{root.name}에 CanvasGroup이 없습니다.", root);
                enabled = false;
                _slots.Clear();
                return;
            }

            _slots.Add(new CarouselSlot
            {
                Item = item,
                Root = root,
                CanvasGroup = canvasGroup,
            });
        }

        ApplySlotLayouts();
    }

    private void OnAllDataInitialized()
    {
        if (UpgradeManager.Instance == null || SlimeManager.Instance == null) return;

        _isInitialized = true;
        _highestGrade = SlimeManager.Instance.HighestGrade;
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
            if (type == EUpgradeType.HigherGradeSpawnWeightAdd &&
                (SlimeManager.Instance == null ||
                 !SlimeManager.Instance.IsHigherGradeSpawnUnlocked))
            {
                continue;
            }

            _upgrades[type] = upgrade;
            _orderedTypes.Add(type);
        }

        _orderedTypes.Sort((left, right) => ((int)left).CompareTo((int)right));
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _orderedTypes.Count - 1));
    }

    private void OnItemPressed(SystemUpgradeItemUI item)
    {
        int slotIndex = _slots.FindIndex(slot => slot.Item == item);

        if (slotIndex == CenterSlotIndex)
        {
            OnUpgradeRequested(item.UpgradeType);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_orderedTypes.Count <= 1 || _rotationTween != null) return;
        if (!TryGetLocalPointerPosition(eventData, out _dragStartPosition)) return;

        _isDragging = true;
        _dragOffset = 0f;
        SetSlotRaycasts(false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging ||
            !TryGetLocalPointerPosition(eventData, out Vector2 pointerPosition))
        {
            return;
        }

        float sideOffset = GetSideOffset();
        _dragOffset = Mathf.Clamp(
            pointerPosition.x - _dragStartPosition.x,
            -sideOffset,
            sideOffset);
        ApplyDragLayouts(sideOffset);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        _isDragging = false;
        float sideOffset = GetSideOffset();
        float threshold = sideOffset * _dragThresholdRatio;

        if (Mathf.Abs(_dragOffset) >= threshold)
        {
            Rotate(_dragOffset < 0f ? 1 : -1);
        }
        else
        {
            ReturnToRest();
        }

        _dragOffset = 0f;
    }

    private bool TryGetLocalPointerPosition(
        PointerEventData eventData,
        out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        RectTransform panelRect = transform as RectTransform;
        return panelRect != null &&
               RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   panelRect,
                   eventData.position,
                   eventData.pressEventCamera,
                   out localPosition);
    }

    private void ApplyDragLayouts(float sideOffset)
    {
        for (int slotIndex = 0; slotIndex < RequiredSlotCount; slotIndex++)
        {
            CarouselSlot slot = _slots[slotIndex];
            float basePosition = (slotIndex - CenterSlotIndex) * sideOffset;
            float positionX = basePosition + _dragOffset;
            float normalizedDistance = Mathf.Abs(positionX) / sideOffset;
            float scale = Mathf.Lerp(
                1f,
                _sideScale,
                Mathf.Clamp01(normalizedDistance));
            float alpha = normalizedDistance <= 1f
                ? Mathf.Lerp(1f, _sideAlpha, normalizedDistance)
                : Mathf.Lerp(_sideAlpha, 0f, Mathf.Clamp01(normalizedDistance - 1f));

            ApplySlotLayout(
                slot,
                positionX,
                GetPositionY(normalizedDistance),
                scale,
                alpha);
        }

        int incomingSlotIndex = CenterSlotIndex + (_dragOffset < 0f ? 1 : -1);
        CarouselSlot incomingSlot = _slots[incomingSlotIndex];
        bool isIncomingCentered = Mathf.Abs(_dragOffset) > sideOffset * 0.5f;

        foreach (CarouselSlot slot in _slots)
        {
            slot.Item.SetCentered(
                isIncomingCentered
                    ? slot == incomingSlot
                    : slot == _slots[CenterSlotIndex]);
        }

        if (isIncomingCentered)
        {
            incomingSlot.Root.SetAsLastSibling();
        }
        else
        {
            _slots[CenterSlotIndex].Root.SetAsLastSibling();
        }
    }

    private void ReturnToRest()
    {
        float sideOffset = GetSideOffset();
        SetSlotRaycasts(false);

        Sequence sequence = DOTween.Sequence();
        for (int slotIndex = 0; slotIndex < RequiredSlotCount; slotIndex++)
        {
            CarouselSlot slot = _slots[slotIndex];
            float positionX = (slotIndex - CenterSlotIndex) * sideOffset;
            float normalizedDistance = Mathf.Abs(slotIndex - CenterSlotIndex);
            float scale = GetScale(normalizedDistance);
            float alpha = GetAlpha(normalizedDistance);

            sequence.Join(slot.Root.DOAnchorPos(
                new Vector2(positionX, GetPositionY(normalizedDistance)),
                _rotationDuration));
            sequence.Join(slot.Root.DOScale(Vector3.one * scale, _rotationDuration));
            sequence.Join(slot.CanvasGroup.DOFade(alpha, _rotationDuration));
        }

        _rotationTween = sequence.OnComplete(() =>
        {
            _rotationTween = null;
            RefreshCarouselSlots();
            SetSlotRaycasts(true);
        });
    }

    private void Rotate(int direction)
    {
        if (_orderedTypes.Count <= 1 || _rotationTween != null) return;

        CarouselSlot incomingSlot = _slots[CenterSlotIndex + direction];
        CarouselSlot outgoingSlot = _slots[CenterSlotIndex];
        float sideOffset = GetSideOffset();

        incomingSlot.Item.SetCentered(true);
        outgoingSlot.Item.SetCentered(false);
        incomingSlot.Root.SetAsLastSibling();
        SetSlotRaycasts(false);

        Sequence sequence = DOTween.Sequence();
        for (int slotIndex = 0; slotIndex < RequiredSlotCount; slotIndex++)
        {
            CarouselSlot slot = _slots[slotIndex];
            float targetDistance = slotIndex - CenterSlotIndex - direction;
            float normalizedDistance = Mathf.Abs(targetDistance);

            sequence.Join(slot.Root.DOAnchorPos(
                new Vector2(
                    targetDistance * sideOffset,
                    GetPositionY(normalizedDistance)),
                _rotationDuration));
            sequence.Join(slot.Root.DOScale(
                Vector3.one * GetScale(normalizedDistance),
                _rotationDuration));
            sequence.Join(slot.CanvasGroup.DOFade(
                GetAlpha(normalizedDistance),
                _rotationDuration));
        }

        _rotationTween = sequence.OnComplete(() => CompleteRotation(direction));
    }

    private void CompleteRotation(int direction)
    {
        _selectedIndex = WrapIndex(_selectedIndex + direction);
        _rotationTween = null;
        RefreshCarouselSlots();
        SetSlotRaycasts(true);
        RotationCompleted?.Invoke();
    }

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
        _highestGrade = grade;
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

    private void Refresh()
    {
        if (!_isInitialized || SpawnManager.Instance == null) return;

        RefreshCarouselSlots();
    }

    private void RefreshCarouselSlots()
    {
        if (_orderedTypes.Count == 0 || _slots.Count != RequiredSlotCount) return;

        for (int slotIndex = 0; slotIndex < RequiredSlotCount; slotIndex++)
        {
            CarouselSlot slot = _slots[slotIndex];
            int offset = slotIndex - CenterSlotIndex;
            int dataIndex = WrapIndex(_selectedIndex + offset);
            EUpgradeType type = _orderedTypes[dataIndex];

            slot.Root.gameObject.SetActive(
                slotIndex == CenterSlotIndex || _orderedTypes.Count > 1);
            slot.Item.Bind(type);
            slot.Item.SetCentered(slotIndex == CenterSlotIndex);

            if (!_upgrades.TryGetValue(type, out Upgrade upgrade))
            {
                continue;
            }

            bool isLocked = UpgradeManager.Instance.IsLockedByProgress(upgrade);
            bool isMax = IsMax(upgrade);
            string valueText = BuildValueText(upgrade, isMax, isLocked);
            string costText = BuildCostText(upgrade, isMax, isLocked);
            slot.Item.Refresh(valueText, costText, isMax || isLocked);
        }

        ApplySlotLayouts();
        _slots[CenterSlotIndex].Root.SetAsLastSibling();
    }

    private void ApplySlotLayouts()
    {
        if (_slots.Count != RequiredSlotCount) return;

        float sideOffset = GetSideOffset();

        for (int slotIndex = 0; slotIndex < RequiredSlotCount; slotIndex++)
        {
            float distance = slotIndex - CenterSlotIndex;
            float normalizedDistance = Mathf.Abs(distance);
            ApplySlotLayout(
                _slots[slotIndex],
                distance * sideOffset,
                GetPositionY(normalizedDistance),
                GetScale(normalizedDistance),
                GetAlpha(normalizedDistance));
        }
    }

    private float GetScale(float normalizedDistance)
    {
        return Mathf.Lerp(1f, _sideScale, Mathf.Clamp01(normalizedDistance));
    }

    // 축소·투명도와 같은 거리 값을 쓰므로 드래그 중에도 자연스럽게 이어진다.
    private float GetPositionY(float normalizedDistance)
    {
        return -Mathf.Lerp(0f, _sideDropDistance, Mathf.Clamp01(normalizedDistance));
    }

    private float GetAlpha(float normalizedDistance)
    {
        if (normalizedDistance <= 1f)
        {
            return Mathf.Lerp(1f, _sideAlpha, normalizedDistance);
        }

        return Mathf.Lerp(
            _sideAlpha,
            0f,
            Mathf.Clamp01(normalizedDistance - 1f));
    }

    private static void ApplySlotLayout(
        CarouselSlot slot,
        float positionX,
        float positionY,
        float scale,
        float alpha)
    {
        slot.Root.anchoredPosition = new Vector2(positionX, positionY);
        slot.Root.localScale = Vector3.one * scale;
        slot.CanvasGroup.alpha = alpha;
    }

    private float GetSideOffset()
    {
        RectTransform panelRect = transform as RectTransform;
        float width = panelRect != null ? panelRect.rect.width : 0f;
        return Mathf.Max(250f, width * _sideOffsetRatio);
    }

    private int WrapIndex(int index)
    {
        int count = _orderedTypes.Count;
        return count == 0 ? 0 : (index % count + count) % count;
    }

    private void SetSlotRaycasts(bool isEnabled)
    {
        foreach (CarouselSlot slot in _slots)
        {
            if (slot.CanvasGroup != null)
            {
                slot.CanvasGroup.blocksRaycasts = isEnabled;
            }
        }
    }

    private static bool IsMax(Upgrade upgrade)
    {
        if (upgrade.IsMaxLevel) return true;

        return upgrade.SpecData.Type == EUpgradeType.SpawnTimeSub &&
               SpawnManager.Instance.SpawnInterval <= SpawnManager.Instance.MinSpawnInterval;
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
                $"{icon}{SpawnManager.Instance.SpawnInterval:F1} -> " +
                $"{Mathf.Max(SpawnManager.Instance.MinSpawnInterval, SpawnManager.Instance.SpawnInterval - (float)modifierIncrease):F1}",
            EUpgradeType.MaxCountAdd =>
                $"{icon}{SpawnManager.Instance.MaxActiveCount} -> " +
                $"{SpawnManager.Instance.MaxActiveCount + Mathf.RoundToInt((float)modifierIncrease)}",
            EUpgradeType.HigherGradeSpawnWeightAdd =>
                IsNextSpawnGradeUnlock(upgrade.Level)
                    ? $"{icon}상위 슬라임 추가!"
                    : $"{icon}Lv.{upgrade.Level} -> Lv.{upgrade.Level + 1}",
            _ => $"{icon}{upgrade.Point:N0} -> {upgrade.NextPoint:N0}",
        };
    }

    private static bool IsNextSpawnGradeUnlock(int currentUpgradeLevel)
    {
        return SlimeManager.Instance != null &&
               SlimeManager.Instance.IsSpawnCapRaisedAtNextLevel(currentUpgradeLevel);
    }

    private string BuildCostText(
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
            return $"<sprite name=\"{(int)_highestGrade:00}\">MAX";
        }

        double cost = (double)upgrade.Cost;
        return $"<sprite name=\"{(int)_highestGrade:00}\">{cost.ToFormattedString()}";
    }
}
