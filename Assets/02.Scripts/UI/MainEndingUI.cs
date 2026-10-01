using System.Collections;
using DG.Tweening;
using UnityEngine;

// 일반 도감 20종 완성 뒤의 전체 화면 졸업식 연출을 담당한다.
// 게임 상태와 입력 소유권, 재생 순서만 관리하고 화면 계층은
// MainEndingPresentationView가 소유한다.
public sealed class MainEndingUI : MonoBehaviour
{
    public static MainEndingUI Instance { get; private set; }

    [Header("Legacy Popup References")]
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private GameObject _doNotTouchPanel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _popupRectTransform;
    [SerializeField] private UnityEngine.UI.Button _continueButton;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private GameplaySpaceManager _spaceManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private DisplayRoomUI _displayRoomUI;
    [SerializeField] private HudVisibility _hudVisibility;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private AutoClicker _autoClicker;

    [Header("Ending Presentation")]
    [Tooltip("씬에 작성된 엔딩 화면입니다. 비활성 상태로 두고, 재생할 때만 켭니다.")]
    [SerializeField] private MainEndingPresentationView _view;

    [Header("Ending Artwork")]
    [Tooltip("9:16 축전 이미지를 연결합니다. 비어 있으면 졸업 타이틀 배경으로 대체합니다.")]
    [SerializeField] private Sprite _celebrationImageSprite;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.45f;
    [SerializeField, Min(1f)] private float _creditScrollDuration = 12f;
    [SerializeField, Min(0f)] private float _introHoldDuration = 1.2f;
    [SerializeField, Min(0f)] private float _teaserHoldDuration = 3.5f;

    private Coroutine _playbackCoroutine;
    private Tween _phaseTween;
    private Tween _celebrationZoomTween;
    private Tween _closeTween;
    private bool _isPresenting;
    private bool _isReplay;
    private bool _isFinalScreen;
    private bool _skipToFinalRequested;
    private bool _ownsHiddenPresentation;
    private bool _ownsGameplayPause;

    public bool IsPresenting => _isPresenting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _popupPanel.SetActive(false);
        _doNotTouchPanel.SetActive(false);
        if (!_view.Initialize(_celebrationImageSprite, OnBackgroundPressed, EndPresentation))
        {
            enabled = false;
            return;
        }

        RefreshSafeArea();
    }

    private void Start()
    {
        if (!enabled) return;

        _slimeManager.NormalCollectionCountChanged += OnCollectionCountChanged;
        TutorialManager.Finished += TryShowFirstEnding;
        _gameManager.OnGameplayActivated += TryShowFirstEnding;
        if (_displayRoomUI != null)
        {
            _displayRoomUI.SendModeEnded += TryShowFirstEnding;
            _displayRoomUI.SlimeTransferred += OnSlimeTransferred;
        }

        TryShowFirstEnding();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        _slimeManager.NormalCollectionCountChanged -= OnCollectionCountChanged;
        TutorialManager.Finished -= TryShowFirstEnding;
        if (_gameManager != null)
        {
            _gameManager.OnGameplayActivated -= TryShowFirstEnding;
        }

        if (_displayRoomUI != null)
        {
            _displayRoomUI.SendModeEnded -= TryShowFirstEnding;
            _displayRoomUI.SlimeTransferred -= OnSlimeTransferred;
        }

        StopAllCoroutines();
        _phaseTween?.Kill();
        _celebrationZoomTween?.Kill();
        _closeTween?.Kill();
        if (_view != null) _view.Dispose();
        ReleasePresentationOwnership(animated: false);
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshSafeArea();
    }

    public bool TryReplay()
    {
        if (_isPresenting ||
            _slimeManager == null ||
            !_slimeManager.IsMainEndingSeen ||
            TutorialManager.IsRunning ||
            _spaceManager.IsTransitioning)
        {
            return false;
        }

        BeginPresentation(isReplay: true);
        return true;
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _popupPanel != null &&
                             _doNotTouchPanel != null &&
                             _canvasGroup != null &&
                             _popupRectTransform != null &&
                             _continueButton != null &&
                             _clicker != null &&
                             _gameExitManager != null &&
                             _gameManager != null &&
                             _spaceManager != null &&
                             _slimeManager != null &&
                             _spawnManager != null &&
                             _displayRoomUI != null &&
                             _hudVisibility != null &&
                             _upgradeUI != null &&
                             _autoClicker != null &&
                             _view != null;
        if (!hasReferences)
        {
            Debug.LogError("메인 엔딩 UI의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnCollectionCountChanged(int count)
    {
        if (count < NormalCollectionRules.MainEndingCount) return;

        // 보내기 모드 중에 20종째가 등록되면 지금은 화면을 비우지 않는다. 보내기 모드의 버튼이
        // 하단 HUD 안에 있어서, 여기서 HUD를 치우면 모드는 남아 있는데 취소할 길이 사라진다.
        // 전송이 끝나는 순간 OnSlimeTransferred가 보내기 모드를 끝내고 엔딩을 이어 간다.
        if (_displayRoomUI != null && _displayRoomUI.IsSendMode) return;

        ReserveHiddenPresentation();
        TryShowFirstEnding();
    }

    // 20종째를 장식장에 보내면 선택 모드를 닫고 엔딩으로 넘어간다. 계속 고르게 두면 엔딩이
    // 모드가 끝날 때까지 밀려난다. 전송 연출이 끝난 뒤에 불리므로 취소해도 안전하다.
    private void OnSlimeTransferred(SlimeController target)
    {
        if (_isPresenting || _slimeManager == null) return;
        if (_slimeManager.NormalCollectionCount < NormalCollectionRules.MainEndingCount) return;
        if (_slimeManager.IsMainEndingSeen) return;

        _displayRoomUI.CancelSendMode();
    }

    private void TryShowFirstEnding()
    {
        if (_isPresenting || _slimeManager == null) return;
        if (_slimeManager.NormalCollectionCount < NormalCollectionRules.MainEndingCount) return;
        if (_slimeManager.IsMainEndingSeen) return;

        // 보내기 모드가 끝나면(SendModeEnded) 다시 불린다. 그 전에 화면을 비우지 않는다.
        if (_displayRoomUI != null && _displayRoomUI.IsSendMode) return;

        if (TutorialManager.IsRunning || !_gameManager.IsGameplayActive) return;
        if (_spaceManager.IsTransitioning) return;

        BeginPresentation(isReplay: false);
    }

    private void BeginPresentation(bool isReplay)
    {
        _isPresenting = true;
        _isReplay = isReplay;
        _isFinalScreen = false;
        _skipToFinalRequested = false;

        ReserveHiddenPresentation();
        _clicker.PushMode(
            this,
            ClickerInputMode.Blocked,
            ClickerInputPriority.Modal);
        _gameExitManager.RegisterBackHandler(this, HandleBack);
        _spawnManager.PushSpawnPause(this);
        _autoClicker?.PushPause(this);
        _ownsGameplayPause = true;

        transform.SetAsLastSibling();
        RefreshSafeArea();
        _phaseTween?.Kill();
        _celebrationZoomTween?.Kill();
        _closeTween?.Kill();
        _phaseTween = null;
        _celebrationZoomTween = null;
        _closeTween = null;
        _view.RefreshSlimeSprites(_slimeManager);
        _view.ResetVisuals();
        _view.SetActive(true);
        _playbackCoroutine = StartCoroutine(PlayEnding());
    }

    private void ReserveHiddenPresentation()
    {
        if (_ownsHiddenPresentation) return;

        _ownsHiddenPresentation = true;
        _hudVisibility?.PushHide(this, EHudParts.All);
        _upgradeUI?.PushStandDown(this);
    }

    private IEnumerator PlayEnding()
    {
        yield return PlayPhase(BuildOpeningFade());
        if (!_skipToFinalRequested)
        {
            yield return PlayPhase(BuildSlimeIntroduction());
        }

        if (!_skipToFinalRequested)
        {
            yield return PlayPhase(BuildCredits());
        }

        if (!_skipToFinalRequested)
        {
            yield return PlayPhase(BuildSpecialSlimeTeaser());
        }

        if (!_skipToFinalRequested)
        {
            yield return PlayPhase(BuildGraduationFinale());
        }

        yield return PlayPhase(BuildCelebrationReveal());
        EnterFinalScreen();
        _playbackCoroutine = null;
    }

    private IEnumerator PlayPhase(Tween tween)
    {
        _phaseTween = tween;
        while (_phaseTween != null && _phaseTween.IsActive())
        {
            yield return null;
        }

        _phaseTween = null;
    }

    private Tween BuildOpeningFade() => _view.BuildOpeningFade(_fadeDuration);

    private Tween BuildSlimeIntroduction() => _view.BuildSlimeIntroduction(
        _fadeDuration,
        _introHoldDuration);

    private Tween BuildCredits()
    {
        string credits = MainEndingCreditsTextBuilder.Build(
            _slimeManager,
            ServerClock.TrustedUtcNow);
        return _view.BuildCredits(
            credits,
            _creditScrollDuration,
            _fadeDuration);
    }

    private Tween BuildSpecialSlimeTeaser() => _view.BuildSpecialSlimeTeaser(
        _fadeDuration,
        _teaserHoldDuration);

    private Tween BuildGraduationFinale() => _view.BuildGraduationFinale(
        _fadeDuration,
        () => AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock));

    private Tween BuildCelebrationReveal() => _view.BuildCelebrationReveal();

    private void EnterFinalScreen()
    {
        _isFinalScreen = true;
        _view.ShowFinalScreen();
        if (!_isReplay)
        {
            _slimeManager.TryMarkMainEndingSeen();
        }

        _celebrationZoomTween?.Kill();
        _celebrationZoomTween = _view.BuildCelebrationZoom();
    }

    private void OnBackgroundPressed()
    {
        if (!_isPresenting || _isFinalScreen) return;
        _phaseTween?.Complete(withCallbacks: true);
    }

    private bool HandleBack()
    {
        if (!_isPresenting) return false;
        if (_isFinalScreen)
        {
            EndPresentation();
            return true;
        }

        _skipToFinalRequested = true;
        _phaseTween?.Complete(withCallbacks: true);
        return true;
    }

    private void EndPresentation()
    {
        if (!_isPresenting || !_isFinalScreen) return;

        _view.SetContinueInteractable(false);
        _celebrationZoomTween?.Kill();
        _celebrationZoomTween = null;
        _closeTween?.Kill();
        _closeTween = _view.BuildClose(_fadeDuration)
            .OnComplete(() =>
            {
                _closeTween = null;
                _view.SetActive(false);
                _isPresenting = false;
                _isReplay = false;
                _isFinalScreen = false;
                ReleasePresentationOwnership(animated: true);
            });
    }

    private void ReleasePresentationOwnership(bool animated)
    {
        _gameExitManager?.UnregisterBackHandler(this);
        _clicker?.ReleaseMode(this);
        if (_ownsHiddenPresentation)
        {
            _hudVisibility?.Release(this, animated);
            _upgradeUI?.ReleaseStandDown(this, animated);
            _ownsHiddenPresentation = false;
        }

        if (_ownsGameplayPause)
        {
            _spawnManager.ReleaseSpawnPause(this);
            _autoClicker?.ReleasePause(this);
            _ownsGameplayPause = false;
        }
    }

    private void RefreshSafeArea()
    {
        _view?.RefreshSafeArea(transform as RectTransform);
    }
}
