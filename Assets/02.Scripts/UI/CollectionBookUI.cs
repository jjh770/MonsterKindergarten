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
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private GameplaySpaceManager _spaceManager;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private MainEndingUI _mainEndingUI;
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
    [Tooltip("장식장에 전시 중일 때만 켜지는 표식입니다. 상세 이미지 위에 둡니다.")]
    [SerializeField] private GameObject _displayRoomBadge;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

    private readonly List<CollectionBookEntryUI> _entries = new();
    private Tween _fadeTween;
    private Tween _scrollTween;
    private Tween _replayDelayTween;
    private ESlimeGrade? _selectedGrade;
    private bool _isOpen;
    private UnityEngine.UI.Button _endingReplayButton;

    public bool IsOpen => _isOpen;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        CreateEntries();
        CreateEndingReplayButton();
        _bookRoot.SetActive(false);
        _openButton.onClick.AddListener(Open);
        _closeButton.onClick.AddListener(Close);
        _previousButton.onClick.AddListener(ShowPrevious);
        _nextButton.onClick.AddListener(ShowNext);
        _spaceManager.SpaceChanged += OnSpaceChanged;
        _gameManager.AllDataInitialized += RefreshOpenButton;
        _gameManager.OnGameplayActivated += RefreshOpenButton;
        TutorialManager.Started += RefreshOpenButton;
        TutorialManager.Finished += RefreshOpenButton;
        _slimeManager.NormalCollectionRegistered += OnNormalCollectionRegistered;
        RefreshLayout();
        RefreshOpenButton();
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _scrollTween?.Kill();
        _replayDelayTween?.Kill();
        _openButton?.onClick.RemoveListener(Open);
        _closeButton?.onClick.RemoveListener(Close);
        _previousButton?.onClick.RemoveListener(ShowPrevious);
        _nextButton?.onClick.RemoveListener(ShowNext);

        if (_spaceManager != null)
        {
            _spaceManager.SpaceChanged -= OnSpaceChanged;
        }

        _gameManager.AllDataInitialized -= RefreshOpenButton;
        TutorialManager.Started -= RefreshOpenButton;
        TutorialManager.Finished -= RefreshOpenButton;
        if (_gameManager != null)
        {
            _gameManager.OnGameplayActivated -= RefreshOpenButton;
        }

        if (_slimeManager != null)
        {
            _slimeManager.NormalCollectionRegistered -= OnNormalCollectionRegistered;
        }
        _endingReplayButton?.onClick.RemoveListener(ReplayEnding);
        DisplayRoomCameraInputGate.Release(this);
        _gameExitManager?.UnregisterBackHandler(this);
        _clicker?.ReleaseMode(this);
        _hudVisibility?.Release(this, animated: false);
        _upgradeUI?.ReleaseStandDown(this, animated: false);
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
                             _displayRoomBadge != null &&
                             _slimeManager != null &&
                             _spaceManager != null &&
                             _gameManager != null &&
                             _mainEndingUI != null;
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
        DisplayRoomCameraInputGate.Push(this);
        transform.SetAsLastSibling();
        _bookRoot.SetActive(true);
        _bookCanvasGroup.alpha = 0f;
        _bookCanvasGroup.interactable = true;
        _bookCanvasGroup.blocksRaycasts = true;
        _clicker.PushMode(
            this,
            ClickerInputMode.Blocked,
            ClickerInputPriority.Modal);
        _upgradeUI.PushStandDown(this);
        _hudVisibility.PushHide(this, EHudParts.All);
        _gameExitManager.RegisterBackHandler(this, TryClose);
        RefreshOpenButton();
        RefreshEntries();
        RefreshEndingReplayButton();

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
        DisplayRoomCameraInputGate.Release(this);
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
        DisplayRoomCameraInputGate.Release(this);
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
        SlimeManager manager = _slimeManager;
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
        SlimeManager manager = _slimeManager;
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

        // 등록 여부가 아니라 지금 전시 중인지를 본다. 꺼내면 등록은 남고 표식만 꺼진다.
        _displayRoomBadge.SetActive(manager.IsDisplayedInDisplayRoom(grade));
    }

    private string BuildRegisteredDetail(
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
            _slimeManager.GetNormalCollectionStats(grade);

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
            RefreshEndingReplayButton();
        }
    }

    private void CreateEndingReplayButton()
    {
        var buttonObject = new GameObject(
            "EndingReplayButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button));
        buttonObject.layer = gameObject.layer;
        buttonObject.transform.SetParent(_safeAreaRoot, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.72f, 0.9f);
        rect.anchorMax = new Vector2(0.72f, 0.9f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 82f);

        UnityEngine.UI.Image image =
            buttonObject.GetComponent<UnityEngine.UI.Image>();
        image.sprite = _closeButton.image != null
            ? _closeButton.image.sprite
            : null;
        image.type = _closeButton.image != null
            ? _closeButton.image.type
            : UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier = _closeButton.image != null
            ? _closeButton.image.pixelsPerUnitMultiplier
            : 1f;

        _endingReplayButton =
            buttonObject.GetComponent<UnityEngine.UI.Button>();
        _endingReplayButton.targetGraphic = image;
        buttonObject.AddComponent<ButtonSFX>();

        var labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.layer = gameObject.layer;
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 8f);
        labelRect.offsetMax = new Vector2(-10f, -8f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = _detailNameText.font;
        label.text = "졸업식";
        label.fontSize = 30f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = 30f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.2f, 0.09f, 0.04f, 1f);
        label.raycastTarget = false;

        _endingReplayButton.onClick.AddListener(ReplayEnding);
        RefreshEndingReplayButton();
    }

    private void RefreshEndingReplayButton()
    {
        if (_endingReplayButton == null) return;

        _endingReplayButton.gameObject.SetActive(
            _slimeManager != null &&
            _slimeManager.IsMainEndingSeen);
    }

    private void ReplayEnding()
    {
        if (!_isOpen || _mainEndingUI == null) return;

        MainEndingUI ending = _mainEndingUI;
        if (!TryClose()) return;

        _replayDelayTween?.Kill();
        _replayDelayTween = DOVirtual.DelayedCall(
            _fadeDuration,
            () =>
            {
                _replayDelayTween = null;
                ending.TryReplay();
            });
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
        // 상점 토글 보이기는 공간(DisplayRoomUI)이 소유한다. 도감은 자신이 물었던
        // 스탠드다운만 풀고, 보이기 값은 건드리지 않는다.
        _upgradeUI.ReleaseStandDown(this, animated);
    }

    private bool CanOpen()
    {
        return GameplayGate.IsDisplayRoomAvailable;
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
