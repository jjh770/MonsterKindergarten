using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Utility;

// 왼쪽 서랍의 상점. 지금 있는 공간에 맞는 물건을 판다.
//
//  - 장식장 : 놀이터에 놓을 오브젝트
//  - 메인 필드 : 배경 테마
//
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

    // 다 산 것에 값을 그대로 두면 살 수 있어 보인다.
    private const string SoldOutLabel = "SOLD OUT";

    private readonly List<PlaygroundShopItemView> _items = new();
    private readonly List<AffordabilityBinding> _affordabilityBindings = new();

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
        if (_table == null || _itemRoot == null || _itemPrefab == null)
        {
            Debug.LogError("장식장 상점의 필수 참조가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled) return;

        // 세이브가 올라오기 전에 목록을 세우면 가진 것이 없는 것으로 읽힌다.
        // 그 뒤로는 사거나 놓을 때만 다시 세우므로, 이미 산 물건이 값이 붙은 채로
        // 남아 다시 살 수 있어 보인다. 값을 치르고 나서야 거절당한다.
        SlimeManager.OnDataInitialized += Refresh;
        SlimeManager.OnPlaygroundChanged += Refresh;
        SlimeManager.OnBackgroundThemesChanged += Refresh;
        CurrencyManager.OnDataChanged += OnCurrencyChanged;

        if (GameplaySpaceManager.Instance != null)
        {
            GameplaySpaceManager.Instance.SpaceChanged += OnSpaceChanged;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        SlimeManager.OnDataInitialized -= Refresh;
        SlimeManager.OnPlaygroundChanged -= Refresh;
        SlimeManager.OnBackgroundThemesChanged -= Refresh;
        CurrencyManager.OnDataChanged -= OnCurrencyChanged;

        // 종료 순서는 보장되지 않아 매니저가 먼저 사라질 수 있다.
        if (GameplaySpaceManager.Instance == null) return;

        GameplaySpaceManager.Instance.SpaceChanged -= OnSpaceChanged;
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        Refresh();
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

        bool isDisplayRoom = GameplaySpaceManager.Instance != null &&
                             !GameplaySpaceManager.Instance.IsMainFieldActive;

        if (_titleText != null)
        {
            _titleText.text = isDisplayRoom ? "놀이터 상점" : "배경 상점";
        }

        ClearItems();

        int shown = isDisplayRoom ? BuildObjectItems() : BuildThemeItems();

        if (_emptyText != null)
        {
            _emptyText.gameObject.SetActive(shown == 0);
            _emptyText.text = isDisplayRoom
                ? "지금은 살 수 있는 물건이 없어요."
                : "새 배경은 준비 중이에요.";
        }
    }

    private int BuildObjectItems()
    {
        SlimeManager manager = SlimeManager.Instance;
        if (manager == null) return 0;

        int shown = 0;
        foreach (PlaygroundShopTableSO.ObjectEntry entry in _table.Objects)
        {
            if (!PlaygroundRules.IsValid(entry.Type)) continue;

            int owned = manager.GetOwnedPlaygroundObjectCount(entry.Type);
            bool isSoldOut = owned >= PlaygroundRules.MaxPerType;
            bool canAfford = CurrencyManager.Instance != null &&
                             CurrencyManager.Instance.CanAfford(
                                 ECurrencyType.Point, entry.Price);

            PlaygroundShopItemView view = CreateItem();
            view.Bind(
                entry.Icon,
                $"{entry.DisplayName}  {owned}/{PlaygroundRules.MaxPerType}",
                entry.Description,
                isSoldOut ? SoldOutLabel : entry.Price.ToFormattedString(),
                !isSoldOut && canAfford,
                () => BuyObject(entry));
            TrackAffordability(view, entry.Price, !isSoldOut);
            shown++;
        }

        return shown;
    }

    private int BuildThemeItems()
    {
        SlimeManager manager = SlimeManager.Instance;
        if (manager == null) return 0;

        int shown = 0;
        foreach (PlaygroundShopTableSO.ThemeEntry entry in _table.Themes)
        {
            if (!BackgroundThemeRules.IsValid(entry.Theme)) continue;
            // 기본 테마는 팔 것이 아니다. 표에 들어와 있어도 보여 주지 않는다.
            if (BackgroundThemeRules.IsFree(entry.Theme)) continue;

            bool isOwned = manager.IsBackgroundThemeOwned(entry.Theme);
            bool canAfford = CurrencyManager.Instance != null &&
                             CurrencyManager.Instance.CanAfford(
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

    // 값을 먼저 치르고 상태를 바꾼다. 상태 변경이 거절되면 되돌려 준다.
    // UpgradeManager.TryLevelUp과 같은 순서다.
    private void BuyObject(PlaygroundShopTableSO.ObjectEntry entry)
    {
        if (CurrencyManager.Instance == null || SlimeManager.Instance == null) return;

        if (!CurrencyManager.Instance.TrySpend(ECurrencyType.Point, entry.Price))
        {
            _toast?.Show("포인트가 모자라요.");
            return;
        }

        if (!SlimeManager.Instance.TryBuyPlaygroundObject(entry.Type))
        {
            CurrencyManager.Instance.Add(ECurrencyType.Point, entry.Price);
            _toast?.Show("더 살 수 없어요.");
            return;
        }

        _toast?.Show($"{entry.DisplayName}을(를) 샀어요.");
    }

    private void BuyTheme(PlaygroundShopTableSO.ThemeEntry entry)
    {
        if (CurrencyManager.Instance == null || SlimeManager.Instance == null) return;

        if (!CurrencyManager.Instance.TrySpend(ECurrencyType.Point, entry.Price))
        {
            _toast?.Show("포인트가 모자라요.");
            return;
        }

        if (!SlimeManager.Instance.TryAddBackgroundTheme(entry.Theme))
        {
            CurrencyManager.Instance.Add(ECurrencyType.Point, entry.Price);
            _toast?.Show("이미 가지고 있어요.");
            return;
        }

        _toast?.Show($"{entry.DisplayName}을(를) 샀어요.");
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
        CurrencyManager currencyManager = CurrencyManager.Instance;
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
