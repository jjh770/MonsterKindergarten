using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using Utility;
using UnityEngine;
using UnityEngine.UI;

public sealed class CollectionBookUI : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private HudVisibility _hudVisibility;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private Button _openButton;

    [Header("Book")]
    [SerializeField] private GameObject _bookRoot;
    [SerializeField] private CanvasGroup _bookCanvasGroup;
    [SerializeField] private RectTransform _safeAreaRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;

    [Header("Entries")]
    [Tooltip("항목 복제본이 배치되는 컨테이너입니다. 항상 활성 상태로 둡니다.")]
    [SerializeField] private RectTransform _entriesRoot;
    [Tooltip("런타임 복제 원본입니다. 프리팹에서는 비활성 상태로 둡니다.")]
    [SerializeField] private CollectionBookEntryUI _entryTemplate;
    [Tooltip("책갈피가 담긴 스크롤 뷰입니다.")]
    [SerializeField] private ScrollRect _entriesScroll;

    [Header("Detail")]
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TextMeshProUGUI _detailNumberText;
    [SerializeField] private TextMeshProUGUI _detailNameText;
    [SerializeField] private TextMeshProUGUI _detailDescriptionText;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

    private readonly List<CollectionBookEntryUI> _entries = new();
    private Tween _fadeTween;
    private Tween _scrollTween;
    private ESlimeGrade? _selectedGrade;
    private bool _isOpen;
    private bool _wasUpgradeToggleInputEnabled;

    public bool IsOpen => _isOpen;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        CreateEntries();
        _bookRoot.SetActive(false);
        _openButton.onClick.AddListener(Open);
        _closeButton.onClick.AddListener(Close);
        _previousButton.onClick.AddListener(ShowPrevious);
        _nextButton.onClick.AddListener(ShowNext);
        StageManager.Instance.SpaceChanged += OnSpaceChanged;
        GameManager.OnAllDataInitialized += RefreshOpenButton;
        GameManager.Instance.OnGameplayActivated += RefreshOpenButton;
        SlimeManager.OnNormalCollectionRegistered += OnNormalCollectionRegistered;
        RefreshLayout();
        RefreshOpenButton();
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _scrollTween?.Kill();
        _openButton?.onClick.RemoveListener(Open);
        _closeButton?.onClick.RemoveListener(Close);
        _previousButton?.onClick.RemoveListener(ShowPrevious);
        _nextButton?.onClick.RemoveListener(ShowNext);

        if (StageManager.Instance != null)
        {
            StageManager.Instance.SpaceChanged -= OnSpaceChanged;
        }

        GameManager.OnAllDataInitialized -= RefreshOpenButton;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated -= RefreshOpenButton;
        }

        SlimeManager.OnNormalCollectionRegistered -= OnNormalCollectionRegistered;
        _gameExitManager?.UnregisterBackHandler(this);
        _clicker?.ReleaseMode(this);
        _hudVisibility?.Release(this, animated: false);
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _gameExitManager != null &&
                             _clicker != null &&
                             _hudVisibility != null &&
                             _upgradeUI != null &&
                             _openButton != null &&
                             _bookRoot != null &&
                             _bookCanvasGroup != null &&
                             _safeAreaRoot != null &&
                             _closeButton != null &&
                             _previousButton != null &&
                             _nextButton != null &&
                             _entriesRoot != null &&
                             _entryTemplate != null &&
                             _entriesScroll != null &&
                             _detailIcon != null &&
                             _detailNumberText != null &&
                             _detailNameText != null &&
                             _detailDescriptionText != null &&
                             GameManager.Instance != null &&
                             StageManager.Instance != null;
        if (!hasReferences)
        {
            Debug.LogError("도감 UI의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_safeAreaRoot != null)
        {
            RefreshLayout();
        }
    }

    private void CreateEntries()
    {
        _entryTemplate.gameObject.SetActive(false);
        for (int i = 0; i < SlimeStatusSaveData.NormalCollectionSize; i++)
        {
            CollectionBookEntryUI entry = Instantiate(
                _entryTemplate,
                _entriesRoot);
            entry.gameObject.name = $"CollectionEntry{i + 1}";
            entry.gameObject.SetActive(true);
            _entries.Add(entry);
        }
    }

    private void Open()
    {
        if (_isOpen || !CanOpen()) return;

        _isOpen = true;
        transform.SetAsLastSibling();
        _bookRoot.SetActive(true);
        _bookCanvasGroup.alpha = 0f;
        _bookCanvasGroup.interactable = true;
        _bookCanvasGroup.blocksRaycasts = true;
        _clicker.PushMode(
            this,
            ClickerInputMode.Blocked,
            ClickerInputPriority.Modal);
        _wasUpgradeToggleInputEnabled = _upgradeUI.IsToggleInputEnabled;
        _upgradeUI.SetToggleInputEnabled(false);
        _upgradeUI.SetToggleVisible(false);
        _hudVisibility.PushHide(this, EHudParts.All);
        _gameExitManager.RegisterBackHandler(this, TryClose);
        RefreshOpenButton();
        RefreshEntries();

        _fadeTween?.Kill();
        _fadeTween = _bookCanvasGroup
            .DOFade(1f, _fadeDuration)
            .OnComplete(() => _fadeTween = null);

    }

    private void Close()
    {
        TryClose();
    }

    private bool TryClose()
    {
        if (!_isOpen) return false;

        _isOpen = false;
        _bookCanvasGroup.interactable = false;
        _gameExitManager.UnregisterBackHandler(this);
        _clicker.ReleaseMode(this);
        RestoreUpgradeToggle();
        _hudVisibility.Release(this);

        _fadeTween?.Kill();
        _fadeTween = _bookCanvasGroup
            .DOFade(0f, _fadeDuration)
            .OnComplete(() =>
            {
                _fadeTween = null;
                _bookRoot.SetActive(false);
                RefreshOpenButton();
            });
        return true;
    }

    private void ForceClose()
    {
        if (!_isOpen) return;

        _isOpen = false;
        _fadeTween?.Kill();
        _fadeTween = null;
        _bookCanvasGroup.alpha = 0f;
        _bookCanvasGroup.interactable = false;
        _bookRoot.SetActive(false);
        _gameExitManager.UnregisterBackHandler(this);
        _clicker.ReleaseMode(this);
        RestoreUpgradeToggle(animated: false);
        _hudVisibility.Release(this, animated: false);
        RefreshOpenButton();
    }

    private void RefreshEntries()
    {
        SlimeManager manager = SlimeManager.Instance;
        if (manager == null) return;

        // 선택이 없으면 첫 장을 편다. 비워 두면 이전·다음이 둘 다 잠겨,
        // 도감을 처음 연 사람에게 죽은 버튼만 보인다.
        _selectedGrade ??= ESlimeGrade.Grade1;

        for (int i = 0; i < _entries.Count; i++)
        {
            ESlimeGrade grade = (ESlimeGrade)(
                (int)ESlimeGrade.Grade1 + i);
            SlimeSpecData specData = manager.Get(grade)?.SpecData;
            bool isRegistered = manager.IsNormalCollectionRegistered(grade);
            CollectionBookEntryUI entry = _entries[i];
            entry.Bind(
                grade,
                specData,
                isRegistered,
                () => OnEntryClicked(grade));
            entry.SetSelected(_selectedGrade == grade);
        }

        ShowDetail(_selectedGrade.Value);

        RefreshNavigationButtons();
    }

    private void OnEntryClicked(ESlimeGrade grade)
    {
        _selectedGrade = grade;
        RefreshEntries();
    }

    private void ShowPrevious()
    {
        SelectRelativeEntry(-1);
    }

    private void ShowNext()
    {
        SelectRelativeEntry(1);
    }

    private void SelectRelativeEntry(int offset)
    {
        if (!_selectedGrade.HasValue) return;

        int selectedIndex = (int)_selectedGrade.Value -
                            (int)ESlimeGrade.Grade1;
        int targetIndex = selectedIndex + offset;
        if (targetIndex < 0 || targetIndex >= _entries.Count) return;

        _selectedGrade = (ESlimeGrade)(
            (int)ESlimeGrade.Grade1 + targetIndex);
        RefreshEntries();
        ScrollToSelected();
    }

    // 이전·다음으로 옮긴 책갈피가 스크롤 밖이면 보이지 않는다. 뷰포트 가운데로 옮긴다.
    //
    // 정규화 위치를 인덱스 비율로 잡으면 항목 폭과 뷰포트 폭이 다를 때 어긋나므로,
    // 콘텐츠 안에서의 실제 좌표로 계산한다. 앵커나 피벗 설정에 기대지 않도록
    // 월드 좌표를 거쳐 변환한다.
    //
    // 책갈피를 직접 누른 경우에는 부르지 않는다. 이미 보이는 것을 누른 것이다.
    private void ScrollToSelected()
    {
        if (!_selectedGrade.HasValue) return;

        int index = (int)_selectedGrade.Value - (int)ESlimeGrade.Grade1;
        if (index < 0 || index >= _entries.Count) return;

        RectTransform content = _entriesScroll.content;
        RectTransform viewport = _entriesScroll.viewport;
        if (content == null || viewport == null) return;

        // 콘텐츠가 뷰포트 안에 다 들어가면 움직일 것이 없다.
        float scrollable = content.rect.width - viewport.rect.width;
        if (scrollable <= 0f) return;

        RectTransform entry = _entries[index].RectTransform;
        Vector3 centerWorld = entry.TransformPoint(entry.rect.center);
        float centerInContent =
            content.InverseTransformPoint(centerWorld).x - content.rect.xMin;
        float target =
            (centerInContent - viewport.rect.width * 0.5f) / scrollable;

        _scrollTween?.Kill();
        _scrollTween = _entriesScroll
            .DOHorizontalNormalizedPos(Mathf.Clamp01(target), _fadeDuration)
            .OnComplete(() => _scrollTween = null);
    }

    private void RefreshNavigationButtons()
    {
        // RefreshEntries가 먼저 선택을 채우므로 여기서는 항상 값이 있다.
        int selectedIndex =
            (int)_selectedGrade.Value - (int)ESlimeGrade.Grade1;
        _previousButton.interactable = selectedIndex > 0;
        _nextButton.interactable = selectedIndex < _entries.Count - 1;
    }

    private void ShowDetail(ESlimeGrade grade)
    {
        SlimeManager manager = SlimeManager.Instance;
        if (manager == null) return;

        SlimeSpecData specData = manager.Get(grade)?.SpecData;
        bool isRegistered = manager.IsNormalCollectionRegistered(grade);
        _detailIcon.sprite = specData?.Sprite;
        _detailIcon.color = isRegistered
            ? Color.white
            : new Color(0.08f, 0.08f, 0.1f, 0.92f);
        _detailNumberText.text = isRegistered
            ? $"No.{(int)grade:00}"
            : "No.??";
        _detailNameText.text = isRegistered
            ? specData?.Name ?? string.Empty
            : "??? 슬라임";
        _detailDescriptionText.text = isRegistered
            ? BuildRegisteredDetail(grade, specData)
            : "장식장에 데려오면\n도감에 자동 등록돼요.";
    }

    private static string BuildRegisteredDetail(
        ESlimeGrade grade,
        SlimeSpecData specData)
    {
        double manualPoint = PointCalculator.Calculate(
            specData?.Point ?? 0,
            grade,
            EClickType.Manual);
        double autoPoint = PointCalculator.Calculate(
            specData?.Point ?? 0,
            grade,
            EClickType.Auto);
        float autoInterval = specData?.AutoClickInterval ?? 0f;
        NormalSlimeCollectionStatsSnapshot stats =
            SlimeManager.Instance.GetNormalCollectionStats(grade);

        return $"{specData?.Description ?? string.Empty}\n\n" +
               "현재 능력\n" +
               $"터치 포인트 {manualPoint.ToFormattedString()}\n" +
               $"자동 포인트 {autoPoint.ToFormattedString()} | " +
               $"{autoInterval:0.#}초\n\n" +
               "나의 기록\n" +
               $"최초 등록 {FormatRegisteredAt(stats.FirstRegisteredAt)}\n" +
               $"자연 출현 {stats.NaturalSpawnCount:N0} | " +
               $"합성 탄생 {stats.MergeCreatedCount:N0}\n" +
               $"유효 터치 {stats.ManualTouchCount:N0} | " +
               $"누적 생산 {stats.ProducedPointTotal.ToFormattedString()}";
    }

    private static string FormatRegisteredAt(string registeredAt)
    {
        if (!DateTime.TryParse(
                registeredAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed))
        {
            return "기록 이전";
        }

        return parsed.ToLocalTime().ToString("yyyy.MM.dd");
    }

    private void OnNormalCollectionRegistered(ESlimeGrade grade)
    {
        if (_isOpen)
        {
            RefreshEntries();
        }
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        if (space != EGameplaySpace.DisplayRoom)
        {
            ForceClose();
        }

        RefreshOpenButton();
    }

    private void RefreshOpenButton()
    {
        if (_openButton == null) return;

        _openButton.gameObject.SetActive(CanOpen());
    }

    private void RestoreUpgradeToggle(bool animated = true)
    {
        bool isMainStage = StageManager.Instance != null &&
                           StageManager.Instance.IsMainStageActive;
        _upgradeUI.SetToggleVisible(isMainStage, animated);
        _upgradeUI.SetToggleInputEnabled(_wasUpgradeToggleInputEnabled);
    }

    private bool CanOpen()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.IsAllDataInitialized &&
               GameManager.Instance.IsGameplayActive &&
               SlimeManager.Instance != null &&
               SlimeManager.Instance.IsDisplayRoomUnlocked;
    }

    private void RefreshLayout()
    {
        RectTransform root = transform as RectTransform;
        if (root == null || _safeAreaRoot == null) return;

        SafeAreaInsets insets = SafeAreaUtility.GetInsets(root);
        _safeAreaRoot.offsetMin = new Vector2(insets.Left, insets.Bottom);
        _safeAreaRoot.offsetMax = new Vector2(-insets.Right, -insets.Top);
    }
}
