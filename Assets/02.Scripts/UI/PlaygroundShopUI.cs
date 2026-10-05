using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

// 왼쪽 서랍의 상점. 탭 두 개로 장식장 오브젝트와 배경 테마를 나눠 판다.
//
//  - 장식장 탭 : 장식장에 놓을 오브젝트
//  - 배경 탭 : 배경 테마
//
// 처음 열 때의 탭은 지금 있는 공간을 따른다(장식장이면 장식장 탭, 메인 필드면 배경 탭).
// 어느 공간에서든 두 탭을 오갈 수 있다. 공간으로만 갈랐을 때는 메인 필드에서 서랍을 여는
// 플레이어가 장식장 물건을 볼 길이 없었다.
//
// 서랍은 최고 Lv.9를 넘기 전까지 손잡이를 치워 둔다(UnlockGrades.Shop).

// 서랍 자체의 여닫기와 위치는 UpgradeUI가 소유한다. 여기서는 안에 무엇을
// 보여 줄지만 정한다.
public sealed class PlaygroundShopUI : MonoBehaviour
{
    [SerializeField] private PlaygroundShopTableSO _table;
    [SerializeField] private RectTransform _itemRoot;
    [SerializeField] private PlaygroundShopItemView _itemPrefab;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _emptyText;
    [SerializeField] private ToastMessageUI _toast;
    [SerializeField] private GameplaySpaceManager _spaceManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private CurrencyManager _currencyManager;
    [SerializeField] private ShopUI _drawer;
    [Tooltip("탭 두 개를 담은 줄입니다. 튜토리얼이 이 줄을 짚습니다.")]
    [SerializeField] private RectTransform _tabsRoot;

    [Header("Tabs")]
    [SerializeField] private Button _objectsTabButton;
    [SerializeField] private Button _themesTabButton;
    [Tooltip("선택하지 않은 탭에 곱하는 색입니다. 선택한 탭은 원래 색 그대로입니다.")]
    [SerializeField] private Color _unselectedTabTint = new Color(0.72f, 0.72f, 0.72f, 1f);

    [Header("Purchase")]
    [Tooltip("구매 토스트는 다음에 할 일까지 적어서 기본보다 오래 보여 줍니다.")]
    [SerializeField, Min(0f)] private float _purchaseToastDuration = 2.4f;

    // 다 산 것에 값을 그대로 두면 살 수 있어 보인다.
    private const string SoldOutLabel = "SOLD OUT";

    private enum ShopTab
    {
        Objects,
        Themes,
    }

    private ShopTab _selectedTab;

    public RectTransform TabsTarget => _tabsRoot;
    public RectTransform ObjectsTabTarget =>
        _objectsTabButton != null ? _objectsTabButton.transform as RectTransform : null;
    public event Action ObjectsTabSelected;

    private readonly List<PlaygroundShopItemView> _items = new();
    private readonly List<AffordabilityBinding> _affordabilityBindings = new();
    private PlaygroundShopPurchaseService _purchaseService;

    private sealed class AffordabilityBinding
    {
        public PlaygroundShopItemView View { get; }
        public Currency Price { get; }
        public bool IsPurchasable { get; }

        public AffordabilityBinding(
            PlaygroundShopItemView view,
            Currency price,
            bool isPurchasable)
        {
            View = view;
            Price = price;
            IsPurchasable = isPurchasable;
        }
    }

    private void Awake()
    {
        if (_table == null || _itemRoot == null || _itemPrefab == null ||
            _spaceManager == null || _slimeManager == null ||
            _currencyManager == null || _drawer == null ||
            _objectsTabButton == null || _themesTabButton == null ||
            _tabsRoot == null)
        {
            Debug.LogError("장식장 상점의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _purchaseService = new PlaygroundShopPurchaseService(
            _currencyManager,
            _slimeManager);
    }

    private void Start()
    {
        if (!enabled) return;

        // 세이브가 올라오기 전에 목록을 세우면 가진 것이 없는 것으로 읽힌다.
        // 그 뒤로는 사거나 놓을 때만 다시 세우므로, 이미 산 물건이 값이 붙은 채로
        // 남아 다시 살 수 있어 보인다. 값을 치르고 나서야 거절당한다.
        _slimeManager.DataInitialized += OnDataInitialized;
        _slimeManager.PlaygroundChanged += Refresh;
        _slimeManager.BackgroundThemesChanged += Refresh;
        _slimeManager.HighestGradeChanged += OnHighestGradeChanged;
        _currencyManager.DataChanged += OnCurrencyChanged;

        _spaceManager.SpaceChanged += OnSpaceChanged;
        _objectsTabButton.onClick.AddListener(SelectObjectsTab);
        _themesTabButton.onClick.AddListener(SelectThemesTab);

        _selectedTab = GetTabForCurrentSpace();
        ApplyShopUnlock(animated: false);

        Refresh();
    }

    private void OnDestroy()
    {
        _slimeManager.DataInitialized -= OnDataInitialized;
        _slimeManager.PlaygroundChanged -= Refresh;
        _slimeManager.BackgroundThemesChanged -= Refresh;
        _slimeManager.HighestGradeChanged -= OnHighestGradeChanged;
        _currencyManager.DataChanged -= OnCurrencyChanged;
        if (_objectsTabButton != null) _objectsTabButton.onClick.RemoveListener(SelectObjectsTab);
        if (_themesTabButton != null) _themesTabButton.onClick.RemoveListener(SelectThemesTab);

        // 종료 순서는 보장되지 않아 매니저가 먼저 사라질 수 있다.
        if (_spaceManager != null) _spaceManager.SpaceChanged -= OnSpaceChanged;
    }

    private void OnDataInitialized()
    {
        ApplyShopUnlock(animated: false);
        Refresh();
    }

    // 손잡이는 최고 Lv.9를 넘기는 순간 나타난다. 그 순간 뜨는 해금 팝업이 끝난 뒤
    // 튜토리얼이 서랍을 안내한다.
    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        ApplyShopUnlock(animated: true);
    }

    private void ApplyShopUnlock(bool animated)
    {
        bool unlocked = _slimeManager.IsShopUnlocked;
        _drawer.SetContentAvailable(unlocked, animated);
        if (unlocked)
        {
            // 서랍을 켜기만 했으므로, 공간과 튜토리얼이 정하는 입력 상태를 다시 적용한다.
            _spaceManager.RefreshInteraction();
        }
    }

    // 공간을 옮기면 그 공간에 맞는 탭으로 돌아간다. 같은 공간에 머무는 동안의 선택은 건드리지 않는다.
    private void OnSpaceChanged(EGameplaySpace space)
    {
        _selectedTab = GetTabForCurrentSpace();
        Refresh();
    }

    private ShopTab GetTabForCurrentSpace()
    {
        return _spaceManager.IsMainFieldActive ? ShopTab.Themes : ShopTab.Objects;
    }

    private void SelectObjectsTab()
    {
        SelectTab(ShopTab.Objects);
        ObjectsTabSelected?.Invoke();
    }

    private void SelectThemesTab()
    {
        SelectTab(ShopTab.Themes);
    }

    private void SelectTab(ShopTab tab)
    {
        if (_selectedTab == tab) return;

        _selectedTab = tab;
        Refresh();
    }

    private void RefreshTabVisuals()
    {
        SetTabTint(_objectsTabButton, _selectedTab == ShopTab.Objects);
        SetTabTint(_themesTabButton, _selectedTab == ShopTab.Themes);
    }

    private void SetTabTint(Button button, bool isSelected)
    {
        if (button.targetGraphic == null) return;

        button.targetGraphic.color = isSelected ? Color.white : _unselectedTabTint;
    }

    // 잔액은 자주 바뀐다. 카드를 다시 만들면 자동 생산 때마다 화면이 깜빡이므로
    // 구매 버튼의 활성 상태만 갱신한다.
    private void OnCurrencyChanged(ECurrencyType type, Currency value)
    {
        if (type != ECurrencyType.Point) return;

        RefreshAffordability();
    }

    private void Refresh()
    {
        if (!enabled) return;

        bool isObjectsTab = _selectedTab == ShopTab.Objects;

        if (_titleText != null)
        {
            _titleText.text = isObjectsTab ? "장식장 상점" : "배경 상점";
        }

        ClearItems();

        int shown = isObjectsTab ? BuildObjectItems() : BuildThemeItems();
        RefreshTabVisuals();

        if (_emptyText != null)
        {
            _emptyText.gameObject.SetActive(shown == 0);
            _emptyText.text = isObjectsTab
                ? UiMessages.ShopObjectsEmpty
                : UiMessages.ShopThemesEmpty;
        }
    }

    private int BuildObjectItems()
    {
        SlimeManager manager = _slimeManager;
        if (manager == null) return 0;

        int shown = 0;
        foreach (PlaygroundShopTableSO.ObjectEntry entry in _table.Objects)
        {
            if (!PlaygroundRules.IsValid(entry.Type)) continue;

            bool isOwned = manager.GetOwnedPlaygroundObjectCount(entry.Type) > 0;
            bool canAfford = _currencyManager.CanAfford(
                                 ECurrencyType.Point, entry.Price);

            PlaygroundShopItemView view = CreateItem();
            view.Bind(
                entry.Icon,
                entry.DisplayName,
                entry.Description,
                isOwned ? SoldOutLabel : entry.Price.ToFormattedString(),
                !isOwned && canAfford,
                () => BuyObject(entry));
            TrackAffordability(view, entry.Price, !isOwned);
            shown++;
        }

        return shown;
    }

    private int BuildThemeItems()
    {
        SlimeManager manager = _slimeManager;
        if (manager == null) return 0;

        int shown = 0;
        foreach (PlaygroundShopTableSO.ThemeEntry entry in _table.Themes)
        {
            if (!BackgroundThemeRules.IsValid(entry.Theme)) continue;
            // 무료 기본 테마는 팔 것이 아니다. 표에 들어와 있어도 보여 주지 않는다.
            if (BackgroundThemeRules.IsFree(entry.Theme)) continue;

            bool isOwned = manager.IsBackgroundThemeOwned(entry.Theme);
            bool canAfford = _currencyManager.CanAfford(
                                 ECurrencyType.Point, entry.Price);

            PlaygroundShopItemView view = CreateItem();
            view.Bind(
                entry.Icon,
                entry.DisplayName,
                entry.Description,
                isOwned ? SoldOutLabel : entry.Price.ToFormattedString(),
                !isOwned && canAfford,
                () => BuyTheme(entry));
            TrackAffordability(view, entry.Price, !isOwned);
            shown++;
        }

        return shown;
    }

    private void BuyObject(PlaygroundShopTableSO.ObjectEntry entry)
    {
        EPlaygroundShopPurchaseResult result = _purchaseService.TryBuyObject(
            entry.Type,
            entry.Price);
        PresentPurchaseResult(
            result,
            $"{KoreanParticle.Object(entry.DisplayName)} 샀어요! 장식장의 배치 버튼으로 놓아 보세요.");
    }

    private void BuyTheme(PlaygroundShopTableSO.ThemeEntry entry)
    {
        EPlaygroundShopPurchaseResult result = _purchaseService.TryBuyTheme(
            entry.Theme,
            entry.Price);
        PresentPurchaseResult(
            result,
            $"{entry.DisplayName} 배경을 샀어요! 이동 메뉴의 배경 버튼에서 바꿀 수 있어요.");
    }

    private void PresentPurchaseResult(
        EPlaygroundShopPurchaseResult result,
        string successMessage)
    {
        switch (result)
        {
            case EPlaygroundShopPurchaseResult.Success:
                PlayPurchaseSound();
                _toast?.Show(successMessage, _purchaseToastDuration);
                break;

            case EPlaygroundShopPurchaseResult.InsufficientPoints:
                _toast?.Show(UiMessages.NotEnoughPoints);
                break;

            case EPlaygroundShopPurchaseResult.Unavailable:
                _toast?.Show(SoldOutLabel);
                break;

            default:
                throw new System.ArgumentOutOfRangeException(
                    nameof(result), result, null);
        }
    }

    private void PlayPurchaseSound()
    {
        AudioManager.Instance?.PlaySFX(EAudioSfx.ShopPurchase);
    }

    private PlaygroundShopItemView CreateItem()
    {
        PlaygroundShopItemView view = Instantiate(_itemPrefab, _itemRoot);
        view.gameObject.SetActive(true);
        _items.Add(view);
        return view;
    }

    private void TrackAffordability(
        PlaygroundShopItemView view,
        Currency price,
        bool isPurchasable)
    {
        _affordabilityBindings.Add(
            new AffordabilityBinding(view, price, isPurchasable));
    }

    private void RefreshAffordability()
    {
        CurrencyManager currencyManager = _currencyManager;
        foreach (AffordabilityBinding binding in _affordabilityBindings)
        {
            if (binding.View == null) continue;

            bool canAfford = currencyManager != null &&
                             currencyManager.CanAfford(
                                 ECurrencyType.Point, binding.Price);
            binding.View.SetInteractable(binding.IsPurchasable && canAfford);
        }
    }

    // 내가 만든 목록만 믿지 않고 자리에 남은 항목까지 치운다.
    //
    // 개발 중에 스크립트를 고치면 플레이 도중 도메인이 다시 올라오는데, 그때
    // 직렬화되지 않는 _items는 비워지고 이미 만들어 둔 항목은 화면에 남는다.
    // 그러면 상점을 열 때마다 같은 물건이 한 줄씩 쌓인다.
    private void ClearItems()
    {
        _items.Clear();
        _affordabilityBindings.Clear();

        for (int i = _itemRoot.childCount - 1; i >= 0; --i)
        {
            Transform child = _itemRoot.GetChild(i);
            if (child.GetComponent<PlaygroundShopItemView>() == null) continue;

            Destroy(child.gameObject);
        }
    }
}
