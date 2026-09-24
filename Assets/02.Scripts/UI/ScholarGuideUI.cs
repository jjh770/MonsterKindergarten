using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 학자 슬라임을 눌렀을 때 HUD를 비우고 게임 정보를 고르는 전면 안내를 보여 준다.
// 실제 정보 계산은 기존 팝업과 공유하고, 이 컴포넌트는 표시와 입력 흐름만 소유한다.
public sealed class ScholarGuideUI : MonoBehaviour
{
    [Header("Owners")]
    [SerializeField] private HudVisibility _hudVisibility;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private GameExitManager _gameExitManager;

    [Header("Presentation")]
    [SerializeField] private RectTransform _root;
    [SerializeField] private Image _sourceImage;
    [SerializeField] private Image _scholarImage;
    [SerializeField] private RectTransform _scholarDestination;
    [SerializeField] private CanvasGroup _portraitBackgroundGroup;
    [SerializeField] private CanvasGroup _dialogueGroup;
    [SerializeField, Min(0f)] private float _moveDuration = 0.6f;
    [SerializeField, Min(1f)] private float _centerScale = 1.5f;

    [Header("Menu")]
    [SerializeField] private GameObject _menuRoot;
    [SerializeField] private Button _probabilityButton;
    [SerializeField] private Button _upgradeStatusButton;
    [SerializeField] private Button _closeButton;

    [Header("Detail")]
    [SerializeField] private GameObject _detailRoot;
    [SerializeField] private RectTransform _detailPanel;
    [SerializeField] private TextMeshProUGUI _detailTitle;
    [SerializeField] private TextMeshProUGUI _detailText;
    [SerializeField] private Button _backButton;

    private Tween _scholarTween;
    private Tween _dialogueTween;
    private Vector2 _sourcePosition;
    private Vector2 _sourceSize;
    private bool _isOpen;
    private bool _isTransitioning;
    private bool _presentationRestored;

    public static bool IsAnyOpen { get; private set; }
    public bool IsOpen => _isOpen;
    public RectTransform ProbabilityButtonTarget =>
        _probabilityButton != null ? _probabilityButton.transform as RectTransform : null;
    public RectTransform DetailTarget => _detailPanel;

    public event Action MenuOpened;
    public event Action ProbabilityOpened;
    public event Action Closed;

    private void Awake()
    {
        if (_root == null || _sourceImage == null || _scholarImage == null ||
            _scholarDestination == null || _portraitBackgroundGroup == null ||
            _dialogueGroup == null ||
            _menuRoot == null || _probabilityButton == null ||
            _upgradeStatusButton == null || _closeButton == null ||
            _detailRoot == null || _detailPanel == null || _detailTitle == null ||
            _detailText == null || _backButton == null)
        {
            Debug.LogError("학자 안내 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _probabilityButton.onClick.AddListener(ShowProbability);
        _upgradeStatusButton.onClick.AddListener(ShowUpgradeStatus);
        _closeButton.onClick.AddListener(Close);
        _backButton.onClick.AddListener(ShowMenu);
        _root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _probabilityButton?.onClick.RemoveListener(ShowProbability);
        _upgradeStatusButton?.onClick.RemoveListener(ShowUpgradeStatus);
        _closeButton?.onClick.RemoveListener(Close);
        _backButton?.onClick.RemoveListener(ShowMenu);
        Cleanup(animated: false, notify: false);
    }

    public void Open()
    {
        if (!enabled || _isOpen || _isTransitioning || !GameplayGate.IsActive) return;
        if (GameplaySpaceManager.Instance != null && GameplaySpaceManager.Instance.IsTransitioning) return;

        _isOpen = true;
        IsAnyOpen = true;
        _isTransitioning = true;
        _presentationRestored = false;
        _upgradeUI?.PushStandDown(this);
        _hudVisibility?.PushHide(this, EHudParts.All);
        _clicker?.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        _gameExitManager?.RegisterBackHandler(this, TryHandleBack);

        PrepareScholarImage();
        _root.gameObject.SetActive(true);
        _root.SetAsLastSibling();
        _menuRoot.SetActive(true);
        _detailRoot.SetActive(false);
        _portraitBackgroundGroup.alpha = 1f;
        _dialogueGroup.alpha = 0f;
        _dialogueGroup.interactable = false;
        _dialogueGroup.blocksRaycasts = false;

        Vector2 destination = GetLocalPosition(_scholarDestination);
        _scholarTween?.Kill();
        _scholarTween = DOTween.Sequence()
            .Join(_scholarImage.rectTransform.DOAnchorPos(destination, _moveDuration))
            .Join(_scholarImage.rectTransform.DOScale(_centerScale, _moveDuration))
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                _isTransitioning = false;
                _dialogueGroup.interactable = true;
                _dialogueGroup.blocksRaycasts = true;
                _dialogueTween = _dialogueGroup.DOFade(1f, 0.2f);
                MenuOpened?.Invoke();
            });
    }

    public void Close()
    {
        if (!_isOpen || _isTransitioning) return;

        _isTransitioning = true;
        _dialogueGroup.interactable = false;
        _dialogueGroup.blocksRaycasts = false;
        RestorePresentation(animated: true);
        _dialogueTween?.Kill();
        _dialogueTween = DOTween.Sequence()
            .Join(_dialogueGroup.DOFade(0f, 0.2f))
            .Join(_portraitBackgroundGroup.DOFade(0f, 0.2f));
        _scholarTween?.Kill();
        _scholarTween = DOTween.Sequence()
            .Join(_scholarImage.rectTransform.DOAnchorPos(_sourcePosition, _moveDuration))
            .Join(_scholarImage.rectTransform.DOScale(Vector3.one, _moveDuration))
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => Cleanup(animated: true, notify: true));
    }

    public void ShowProbability()
    {
        if (!_isOpen || SpawnManager.Instance == null) return;

        bool isUnlocked = SlimeManager.Instance != null &&
                          SlimeManager.Instance.IsHigherGradeSpawnUnlocked;
        _detailTitle.text = "자연생성 슬라임 확률";
        _detailText.text = GameplayInfoTextBuilder.BuildSpawnProbabilityText(
            SpawnManager.Instance.GetCurrentSpawnProbabilities(),
            isUnlocked ? SpawnManager.GetSpawnWeightUpgradeLevel() : -1,
            includeTitle: false);
        ShowDetail();
        ProbabilityOpened?.Invoke();
    }

    public void ShowUpgradeStatus()
    {
        if (!_isOpen) return;

        _detailTitle.text = "시스템 업그레이드 현황";
        _detailText.text = GameplayInfoTextBuilder.BuildSystemUpgradeText(
            includeTitle: false);
        ShowDetail();
    }

    public void ShowMenu()
    {
        if (!_isOpen || _isTransitioning) return;

        _detailRoot.SetActive(false);
        _menuRoot.SetActive(true);
    }

    private void ShowDetail()
    {
        _menuRoot.SetActive(false);
        _detailRoot.SetActive(true);
        _detailPanel.SetAsLastSibling();
    }

    private bool TryHandleBack()
    {
        if (!_isOpen) return false;

        if (_detailRoot.activeSelf)
        {
            ShowMenu();
        }
        else
        {
            Close();
        }

        return true;
    }

    private void PrepareScholarImage()
    {
        _scholarImage.sprite = _sourceImage.sprite;
        _scholarImage.overrideSprite = _sourceImage.overrideSprite;
        _scholarImage.color = _sourceImage.color;
        _scholarImage.material = _sourceImage.material;
        _scholarImage.preserveAspect = _sourceImage.preserveAspect;

        RectTransform sourceRect = _sourceImage.rectTransform;
        _sourcePosition = GetLocalPosition(sourceRect);
        _sourceSize = sourceRect.rect.size;
        _scholarImage.rectTransform.anchoredPosition = _sourcePosition;
        _scholarImage.rectTransform.sizeDelta = _sourceSize;
        _scholarImage.rectTransform.localScale = Vector3.one;
        _sourceImage.enabled = false;
    }

    private Vector2 GetLocalPosition(RectTransform target)
    {
        return _root.InverseTransformPoint(target.position);
    }

    private void Cleanup(bool animated, bool notify)
    {
        _scholarTween?.Kill();
        _scholarTween = null;
        _dialogueTween?.Kill();
        _dialogueTween = null;

        if (!_isOpen && !_isTransitioning) return;

        _isOpen = false;
        _isTransitioning = false;
        IsAnyOpen = false;
        if (_sourceImage != null) _sourceImage.enabled = true;
        if (_root != null) _root.gameObject.SetActive(false);
        _gameExitManager?.UnregisterBackHandler(this);
        _clicker?.ReleaseMode(this);
        RestorePresentation(animated);

        if (notify) Closed?.Invoke();
    }

    private void RestorePresentation(bool animated)
    {
        if (_presentationRestored) return;

        _presentationRestored = true;
        _hudVisibility?.Release(this, animated);
        _upgradeUI?.ReleaseStandDown(this, animated);
    }
}
