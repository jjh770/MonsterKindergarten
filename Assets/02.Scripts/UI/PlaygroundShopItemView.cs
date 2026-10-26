using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상점의 항목 한 줄. 프리팹으로 만들어 목록에서 필요한 만큼 찍어 낸다.
public sealed class PlaygroundShopItemView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _priceText;
    [SerializeField] private Button _buyButton;

    [Header("Sold Out")]
    [Tooltip("다 산 항목 위에 찍히는 SOLD OUT 도장입니다.")]
    [SerializeField] private RectTransform _soldOutStamp;
    [Tooltip("막 샀을 때 그림 둘레에서 터지는 반짝이입니다. 평소에는 꺼 둡니다.")]
    [SerializeField] private Image[] _sparkles = Array.Empty<Image>();
    [SerializeField, Min(0f)] private float _sparkleDistance = 150f;

    private Action _onBuy;
    private Sequence _purchase;

    private void Awake()
    {
        if (_buyButton != null)
        {
            _buyButton.onClick.AddListener(OnBuyClicked);
        }
    }

    private void OnDestroy()
    {
        _purchase?.Kill();
        if (_buyButton != null)
        {
            _buyButton.onClick.RemoveListener(OnBuyClicked);
        }
    }

    // priceText가 비어 있으면 값 대신 상태만 보여 준다. 다 산 항목이 그렇다.
    public void Bind(
        Sprite icon,
        string displayName,
        string description,
        string priceLabel,
        bool canBuy,
        bool isSoldOut,
        Action onBuy)
    {
        _onBuy = onBuy;
        if (_soldOutStamp != null) _soldOutStamp.gameObject.SetActive(isSoldOut);
        foreach (Image sparkle in _sparkles)
        {
            if (sparkle != null) sparkle.gameObject.SetActive(false);
        }

        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        if (_nameText != null) _nameText.text = displayName;
        if (_descriptionText != null) _descriptionText.text = description;
        if (_priceText != null) _priceText.text = priceLabel;
        SetInteractable(canBuy);
    }

    public void SetInteractable(bool interactable)
    {
        if (_buyButton != null) _buyButton.interactable = interactable;
    }

    // 방금 산 항목이다. 카드가 출렁이고 그림 둘레에서 반짝이가 터진 뒤 SOLD OUT 도장이 쾅 찍힌다.
    // 구매는 이미 끝났고 카드도 다 산 모습으로 다시 만들어진 뒤라, 보여 주기만 한다.
    public void PlayPurchased()
    {
        _purchase?.Kill();
        _purchase = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _purchase.OnComplete(() => _purchase = null);
        _purchase.Append(transform.DOPunchScale(Vector3.one * .06f, .35f, 6, .5f));

        Vector3 center = _icon != null ? _icon.rectTransform.localPosition : Vector3.zero;
        for (int i = 0; i < _sparkles.Length; i++)
        {
            Image sparkle = _sparkles[i];
            if (sparkle == null) continue;

            RectTransform rect = sparkle.rectTransform;
            float angle = (i + UnityEngine.Random.Range(-.3f, .3f)) * Mathf.PI * 2f / _sparkles.Length;
            float distance = _sparkleDistance * UnityEngine.Random.Range(.7f, 1.1f);
            Vector3 target = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
            float delay = UnityEngine.Random.Range(0f, .08f);
            rect.localPosition = center;
            rect.localScale = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            sparkle.color = Color.white;
            sparkle.gameObject.SetActive(true);
            _purchase.Insert(delay, rect.DOLocalMove(target, .55f).SetEase(Ease.OutCubic));
            _purchase.Insert(delay, rect.DOScale(Vector3.one, .2f).SetEase(Ease.OutBack));
            _purchase.Insert(delay, rect.DOLocalRotate(new Vector3(0f, 0f, 180f), .55f, RotateMode.FastBeyond360));
            _purchase.Insert(delay + .3f, sparkle.DOFade(0f, .25f));
            _purchase.InsertCallback(delay + .55f, () => sparkle.gameObject.SetActive(false));
        }

        if (_soldOutStamp == null) return;

        // 도장은 크게 떠 있다가 내리찍히고, 찍히는 순간 카드가 한 번 더 들썩인다.
        const float StampAt = .3f;
        CanvasGroup stampGroup = _soldOutStamp.GetComponent<CanvasGroup>();
        Vector3 stampScale = _soldOutStamp.localScale;
        _soldOutStamp.gameObject.SetActive(true);
        _soldOutStamp.localScale = stampScale * 2.2f;
        if (stampGroup != null) stampGroup.alpha = 0f;
        if (stampGroup != null) _purchase.Insert(StampAt, stampGroup.DOFade(1f, .12f));
        _purchase.Insert(StampAt, _soldOutStamp.DOScale(stampScale, .18f).SetEase(Ease.InQuad));
        _purchase.InsertCallback(StampAt + .18f, () => AudioManager.Instance?.PlaySFXRandomPitch(EAudioSfx.SlimeLand));
        _purchase.Insert(StampAt + .18f, _soldOutStamp.DOPunchScale(stampScale * .12f, .25f, 6, .5f));
        _purchase.Insert(StampAt + .18f, transform.DOPunchScale(Vector3.one * .04f, .25f, 6, .5f));
    }

    private void OnBuyClicked()
    {
        _onBuy?.Invoke();
    }
}
