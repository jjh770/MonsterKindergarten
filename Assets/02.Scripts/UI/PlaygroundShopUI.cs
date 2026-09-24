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

    private readonly List<PlaygroundShopItemView> _items = new();

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

    // 살 수 있는지는 잔액에 따라 바뀐다. 포인트만 보고 다시 그린다.
    private void OnCurrencyChanged(ECurrencyType type, Currency value)
    {
        if (type != ECurrencyType.Point) return;

        Refresh();
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
                isSoldOut ? "다 모았어요" : entry.Price.ToFormattedString(),
                !isSoldOut && canAfford,
                () => BuyObject(entry));
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
                isOwned ? "가지고 있어요" : entry.Price.ToFormattedString(),
                !isOwned && canAfford,
                () => BuyTheme(entry));
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

    private void ClearItems()
    {
        foreach (PlaygroundShopItemView item in _items)
        {
            if (item != null) Destroy(item.gameObject);
        }

        _items.Clear();
    }
}
