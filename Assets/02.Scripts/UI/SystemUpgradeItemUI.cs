using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemUpgradeItemUI : MonoBehaviour
{
    [SerializeField] private EUpgradeType _upgradeType;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _valueText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private CurrencyManager _currencyManager;

    [Tooltip("포인트가 모자랄 때 가격 줄의 불투명도입니다.")]
    [SerializeField, Range(0f, 1f)] private float _unaffordableCostAlpha = 0.4f;

    [Header("Upgrade Success Effect")]
    [SerializeField, Range(3, 10)] private int _successArrowCount = 7;
    [SerializeField, Min(0f)] private float _successArrowSpread = 220f;
    [SerializeField, Min(0f)] private float _successArrowRise = 190f;
    [SerializeField, Min(0.1f)] private float _successArrowDuration = 0.65f;
    [SerializeField, Min(0f)] private float _successArrowStagger = 0.045f;
    [SerializeField, Min(1f)] private float _successArrowFontSize = 56f;
    [SerializeField] private Color _successArrowColor =
        new Color(0.48f, 0.72f, 0.24f, 1f);

    private bool _isCentered;
    private bool _canPurchase;
    private Currency? _purchaseCost;
    private readonly List<UpgradeArrow> _successArrows = new();
    private Sequence _successEffectSequence;

    private sealed class UpgradeArrow
    {
        public RectTransform RectTransform { get; }
        public TextMeshProUGUI Text { get; }

        public UpgradeArrow(RectTransform rectTransform, TextMeshProUGUI text)
        {
            RectTransform = rectTransform;
            Text = text;
        }
    }

    public EUpgradeType UpgradeType => _upgradeType;
    public event Action<SystemUpgradeItemUI> Pressed;

    private void Awake()
    {
        if (_currencyManager == null)
        {
            Debug.LogError("업그레이드 항목의 CurrencyManager 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }
        _button?.onClick.AddListener(OnClickUpgrade);
    }

    private void Start()
    {
        _currencyManager.DataChanged += OnCurrencyChanged;

        RefreshAffordability();
    }

    private void OnDestroy()
    {
        _successEffectSequence?.Kill();
        _button?.onClick.RemoveListener(OnClickUpgrade);
        if (_currencyManager != null) _currencyManager.DataChanged -= OnCurrencyChanged;
    }

    private void OnDisable()
    {
        StopSuccessEffect();
    }

    public void Bind(EUpgradeType upgradeType)
    {
        _upgradeType = upgradeType;

        if (_nameText != null)
        {
            _nameText.text = SystemUpgradeNames.Get(upgradeType);
        }
    }

    public void SetCentered(bool isCentered)
    {
        _isCentered = isCentered;
        RefreshInteractable();
    }

    // purchaseCost는 지금 살 수 있는 단계일 때만 넘긴다. 최대 레벨이나 잠김처럼
    // 가격이 아닌 문구를 보여 줄 때는 흐리게 할 이유가 없다.
    public void Refresh(
        string valueText,
        string costText,
        bool isDisabled,
        Currency? purchaseCost = null)
    {
        if (_valueText != null)
        {
            _valueText.text = valueText;
        }

        if (_costText != null)
        {
            _costText.text = costText;
        }

        _canPurchase = !isDisabled;
        _purchaseCost = purchaseCost;
        RefreshInteractable();
        RefreshAffordability();
    }

    private void OnClickUpgrade()
    {
        Pressed?.Invoke(this);
    }

    public void PlayUpgradeSuccessEffect()
    {
        EnsureSuccessArrows();
        StopSuccessEffect();

        _successEffectSequence = DOTween.Sequence();
        int count = _successArrows.Count;
        for (int i = 0; i < count; i++)
        {
            UpgradeArrow arrow = _successArrows[i];
            float normalized = count <= 1 ? 0.5f : i / (float)(count - 1);
            float startX = Mathf.Lerp(
                -_successArrowSpread * 0.5f,
                _successArrowSpread * 0.5f,
                normalized);
            float endX = startX * 1.12f;
            float delay = i * _successArrowStagger;
            float riseOffset = i % 2 == 0 ? 0f : 24f;

            arrow.RectTransform.gameObject.SetActive(true);
            arrow.RectTransform.SetAsLastSibling();
            arrow.RectTransform.anchoredPosition = new Vector2(startX, -30f);
            arrow.RectTransform.localScale = Vector3.one * 0.55f;
            arrow.Text.color = new Color(
                _successArrowColor.r,
                _successArrowColor.g,
                _successArrowColor.b,
                0f);

            _successEffectSequence.Insert(
                delay,
                arrow.RectTransform
                    .DOAnchorPos(
                        new Vector2(endX, _successArrowRise + riseOffset),
                        _successArrowDuration)
                    .SetEase(Ease.OutCubic));
            _successEffectSequence.Insert(
                delay,
                arrow.RectTransform
                    .DOScale(1f, _successArrowDuration * 0.3f)
                    .SetEase(Ease.OutBack));
            _successEffectSequence.Insert(
                delay,
                arrow.Text.DOFade(1f, _successArrowDuration * 0.18f));
            _successEffectSequence.Insert(
                delay + _successArrowDuration * 0.48f,
                arrow.Text.DOFade(0f, _successArrowDuration * 0.52f));
        }

        _successEffectSequence.OnComplete(() =>
        {
            _successEffectSequence = null;
            HideSuccessArrows();
        });
    }

    private void EnsureSuccessArrows()
    {
        if (_successArrows.Count == _successArrowCount) return;

        foreach (UpgradeArrow arrow in _successArrows)
        {
            if (arrow.RectTransform != null)
            {
                Destroy(arrow.RectTransform.gameObject);
            }
        }

        _successArrows.Clear();
        for (int i = 0; i < _successArrowCount; i++)
        {
            var arrowObject = new GameObject(
                $"UpgradeArrow{i + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            arrowObject.layer = gameObject.layer;
            arrowObject.transform.SetParent(transform, false);

            RectTransform rectTransform =
                arrowObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(64f, 72f);

            LayoutElement layoutElement = arrowObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            TextMeshProUGUI arrowText = arrowObject.GetComponent<TextMeshProUGUI>();
            arrowText.raycastTarget = false;
            arrowText.text = "↑";
            arrowText.font = _valueText != null ? _valueText.font : null;
            arrowText.fontSize = _successArrowFontSize;
            arrowText.fontStyle = FontStyles.Bold;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.enableWordWrapping = false;

            arrowObject.SetActive(false);
            _successArrows.Add(new UpgradeArrow(rectTransform, arrowText));
        }
    }

    private void StopSuccessEffect()
    {
        _successEffectSequence?.Kill();
        _successEffectSequence = null;
        HideSuccessArrows();
    }

    private void HideSuccessArrows()
    {
        foreach (UpgradeArrow arrow in _successArrows)
        {
            if (arrow.RectTransform != null)
            {
                arrow.RectTransform.gameObject.SetActive(false);
            }
        }
    }

    private void OnCurrencyChanged(ECurrencyType type, Currency amount)
    {
        if (type == ECurrencyType.Point)
        {
            RefreshAffordability();
        }
    }

    // 포인트가 모자라도 버튼은 눌린다. 누르면 부족 안내가 뜨므로 막지 않고
    // 가격 줄만 흐리게 해 미리 알린다. 카드 전체를 흐리면 선택 안 된 카드와 구분이 안 된다.
    //
    // 카루셀을 다시 그리지 않고 여기서 처리한다. 포인트는 클릭과 자동 생산마다 바뀌고,
    // Rebuild는 슬롯 배치까지 다시 잡아 회전 중인 카드를 제자리로 튕긴다.
    private void RefreshAffordability()
    {
        if (_costText == null) return;

        bool isAffordable = !_purchaseCost.HasValue ||
                            _currencyManager.CanAfford(
                                ECurrencyType.Point,
                                _purchaseCost.Value);
        float alpha = isAffordable ? 1f : _unaffordableCostAlpha;
        if (!Mathf.Approximately(_costText.alpha, alpha))
        {
            _costText.alpha = alpha;
        }
    }

    private void RefreshInteractable()
    {
        if (_button != null)
        {
            _button.interactable = _isCentered && _canPurchase;
        }
    }
}
