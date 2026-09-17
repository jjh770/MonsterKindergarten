using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

// 가로 카루셀의 회전·드래그·배치만 담당한다.
//
// 무엇이 실려 있는지는 모른다. 항목 수는 밖에서 알려 주고, 슬롯에 무엇을 채울지는
// SlotBinding으로 되묻는다. 업그레이드 규칙과 이 배치 수학은 함께 바뀔 일이 없는데
// 한 파일에 있으면 어느 한쪽을 고칠 때마다 나머지를 읽고 지나가야 했다.
//
// 드래그를 받으려면 패널과 같은 오브젝트에 있어야 한다. 좌우 간격을 그 RectTransform
// 폭에서 끌어내므로 다른 오브젝트로 옮기면 슬롯 간격이 함께 달라진다.
public sealed class SystemUpgradeCarousel : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public const int CenterSlotIndex = 2;
    public const int RequiredSlotCount = 5;

    [SerializeField] private SystemUpgradeItemUI[] _items;

    [Header("Layout")]
    [SerializeField, Range(0.2f, 0.5f)] private float _sideOffsetRatio = 0.42f;
    [SerializeField, Range(0.5f, 1f)] private float _centerScale = 0.9f;
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

    private readonly List<CarouselSlot> _slots = new();
    private int _selectedIndex;
    private int _dataCount;
    private bool _isDragging;
    private Vector2 _dragStartPosition;
    private float _dragOffset;
    private Tween _rotationTween;

    public bool IsReady => _slots.Count == RequiredSlotCount;
    public int SelectedIndex => _selectedIndex;
    private bool IsBusy => _rotationTween != null || _isDragging;

    // 가운데 슬롯을 눌렀다. 항목 인덱스를 준다.
    public event Action<int> CenterPressed;

    // 이 슬롯에 이 항목을 실어 달라는 요청. 배치가 바뀔 때마다 슬롯 수만큼 온다.
    public event Action<SystemUpgradeItemUI, int> SlotBinding;

    public event Action RotationCompleted;

    private void Awake()
    {
        InitializeSlots();
        if (!IsReady) return;

        foreach (CarouselSlot slot in _slots)
        {
            slot.Item.Pressed += OnItemPressed;
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
        if (IsReady && _dataCount > 0)
        {
            Rebuild();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (IsReady && _rotationTween == null && !_isDragging)
        {
            ApplySlotLayouts();
        }
    }

    // 항목 수가 바뀌면 선택 위치가 범위를 벗어날 수 있다. 여기서 맞춘다.
    public void SetDataCount(int count)
    {
        _dataCount = Mathf.Max(0, count);
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _dataCount - 1));
    }

    private int GetDataIndex(int slotIndex)
    {
        return WrapIndex(_selectedIndex + slotIndex - CenterSlotIndex);
    }

    public bool TryFocus(int dataIndex)
    {
        if (_dataCount == 0 || IsBusy) return false;
        if (dataIndex < 0 || dataIndex >= _dataCount) return false;
        if (dataIndex == _selectedIndex) return true;

        int forwardDistance = WrapIndex(dataIndex - _selectedIndex);
        Rotate(forwardDistance <= _dataCount / 2 ? 1 : -1);
        return true;
    }

    // 슬롯을 지금 선택 위치에 맞춰 다시 채우고 제자리에 놓는다.
    public void Rebuild()
    {
        if (_dataCount == 0 || !IsReady) return;

        for (int slotIndex = 0; slotIndex < RequiredSlotCount; slotIndex++)
        {
            CarouselSlot slot = _slots[slotIndex];

            slot.Root.gameObject.SetActive(
                slotIndex == CenterSlotIndex || _dataCount > 1);
            slot.Item.SetCentered(slotIndex == CenterSlotIndex);
            SlotBinding?.Invoke(slot.Item, GetDataIndex(slotIndex));
        }

        ApplySlotLayouts();
        _slots[CenterSlotIndex].Root.SetAsLastSibling();
    }

    private void InitializeSlots()
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

    private void OnItemPressed(SystemUpgradeItemUI item)
    {
        int slotIndex = _slots.FindIndex(slot => slot.Item == item);
        if (slotIndex != CenterSlotIndex) return;

        CenterPressed?.Invoke(GetDataIndex(slotIndex));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_dataCount <= 1 || _rotationTween != null) return;
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
            float scale = GetScale(normalizedDistance);
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
            Rebuild();
            SetSlotRaycasts(true);
        });
    }

    private void Rotate(int direction)
    {
        if (_dataCount <= 1 || _rotationTween != null) return;

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
        Rebuild();
        SetSlotRaycasts(true);
        RotationCompleted?.Invoke();
    }

    private void ApplySlotLayouts()
    {
        if (!IsReady) return;

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
        return Mathf.Lerp(
            _centerScale,
            _sideScale,
            Mathf.Clamp01(normalizedDistance));
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
        return _dataCount == 0 ? 0 : (index % _dataCount + _dataCount) % _dataCount;
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
}
