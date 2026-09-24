using System;
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

    private Action _onBuy;

    private void Awake()
    {
        if (_buyButton != null)
        {
            _buyButton.onClick.AddListener(OnBuyClicked);
        }
    }

    private void OnDestroy()
    {
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
        Action onBuy)
    {
        _onBuy = onBuy;

        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        if (_nameText != null) _nameText.text = displayName;
        if (_descriptionText != null) _descriptionText.text = description;
        if (_priceText != null) _priceText.text = priceLabel;
        if (_buyButton != null) _buyButton.interactable = canBuy;
    }

    private void OnBuyClicked()
    {
        _onBuy?.Invoke();
    }
}
