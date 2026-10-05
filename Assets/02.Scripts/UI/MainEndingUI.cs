using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

// 장식장에 20종이 실제로 모두 모인 뒤, 플레이어가 장식장에 입장했을 때의
// 전체 화면 졸업식 연출을 담당한다.
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
    [SerializeField] private HudVisibility _hudVisibility;
    [FormerlySerializedAs("_upgradeUI")]
    [SerializeField] private ShopUI _shopUI;
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
    [SerializeField, Min(1f)] private float _holdFastForwardScale = 4f;

    private Coroutine _playbackCoroutine;
    private Tween _phaseTween;
    private Tween _closeTween;
    private bool _isPresenting;
    private bool _isReplay;
    private bool _isFinalScreen;
    private bool _skipToFinalRequested;
    private bool _ownsHiddenPresentation;
    private bool _ownsGameplayPause;
    private Vector2[] _slimeIntroductionOrigins;
    private bool _isFastForwardHeld;
    private bool _isAwaitingAdvance;
    private bool _advanceRequested;

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
        if (!_view.Initialize(
                _celebrationImageSprite,
                StartFastForward,
                StopFastForward,
                RequestAdvance))
        {
            enabled = false;
            return;
        }

        RefreshSafeArea();
    }

    private void Start()
    {
        if (!enabled) return;

        _spaceManager.SpaceTransitionCompleted += OnSpaceTransitionCompleted;
        TutorialManager.Finished += TryShowFirstEnding;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_spaceManager != null)
        {
            _spaceManager.SpaceTransitionCompleted -= OnSpaceTransitionCompleted;
        }
        TutorialManager.Finished -= TryShowFirstEnding;

        StopAllCoroutines();
        _phaseTween?.Kill();
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
                             _hudVisibility != null &&
                             _shopUI != null &&
                             _autoClicker != null &&
                             _view != null;
        if (!hasReferences)
        {
            Debug.LogError("메인 엔딩 UI의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnSpaceTransitionCompleted()
    {
        if (_spaceManager == null || _spaceManager.IsMainFieldActive) return;
        TryShowFirstEnding();
    }

    private void TryShowFirstEnding()
    {
        if (_isPresenting || _slimeManager == null) return;
        if (!_slimeManager.IsGraduationDisplayComplete) return;
        if (_slimeManager.IsMainEndingSeen) return;

        if (TutorialManager.IsRunning || !_gameManager.IsGameplayActive) return;
        if (_spaceManager.IsTransitioning) return;
        if (_spaceManager.IsMainFieldActive) return;

        BeginPresentation(isReplay: false);
    }

    private void BeginPresentation(bool isReplay)
    {
        _isPresenting = true;
        _isReplay = isReplay;
        _isFinalScreen = false;
        _skipToFinalRequested = false;
        _isFastForwardHeld = false;
        _isAwaitingAdvance = false;
        _advanceRequested = false;

        // 최초 재생 직전에 한 번만 고정한다. 이후 다시보기는 이 값만 사용한다.
        _slimeManager.TryCaptureGraduationSnapshot(ServerClock.TrustedUtcNow);

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
        _closeTween?.Kill();
        _phaseTween = null;
        _closeTween = null;
        _view.RefreshSlimeSprites(_slimeManager);
        _slimeIntroductionOrigins = isReplay
            ? null
            : BuildSlimeIntroductionOrigins();
        _view.ResetVisuals();
        _view.SetActive(true);
        _playbackCoroutine = StartCoroutine(PlayEnding());
    }

    private void ReserveHiddenPresentation()
    {
        if (_ownsHiddenPresentation) return;

        _ownsHiddenPresentation = true;
        _hudVisibility?.PushHide(this, EHudParts.All);
        _shopUI?.PushStandDown(this);
    }

    // 슬라임 입장 -> 게임 기록 -> 축전과 감사 인사 -> 특별한 슬라임 예고. 예고가 마지막 화면이라
    // 거기서 터치하면 졸업식이 끝난다. 건너뛰기는 예고 화면으로 넘어간다.
    private IEnumerator PlayEnding()
    {
        yield return PlayPhase(BuildOpeningFade());
        if (!_skipToFinalRequested)
        {
            yield return PlayPhase(BuildSlimeIntroduction());
            yield return WaitForAdvance();
            yield return PlayPhase(_view.HideSlimeIntroduction(_fadeDuration));
        }

        if (!_skipToFinalRequested)
        {
            yield return PlayPhase(BuildCredits());
            yield return WaitForAdvance();
            yield return PlayPhase(_view.HideCredits(_fadeDuration));
        }

        if (!_skipToFinalRequested)
        {
            yield return PlayPhase(_view.BuildThanks(_fadeDuration));
            yield return WaitForAdvance();
            yield return PlayPhase(_view.HideThanks(_fadeDuration));
        }

        yield return PlayPhase(BuildSpecialSlimeTeaser());
        EnterFinalScreen();
        yield return WaitForAdvance();

        _playbackCoroutine = null;
        EndPresentation();
    }

    private IEnumerator PlayPhase(Tween tween)
    {
        _phaseTween = tween;
        if (_phaseTween != null)
            _phaseTween.timeScale = _isFastForwardHeld ? _holdFastForwardScale : 1f;
        while (_phaseTween != null && _phaseTween.IsActive())
        {
            yield return null;
        }

        _phaseTween = null;
    }

    private IEnumerator WaitForAdvance()
    {
        _isAwaitingAdvance = true;
        _advanceRequested = false;
        _view.SetAdvanceHint(true);
        while (!_advanceRequested && !_skipToFinalRequested)
        {
            yield return null;
        }
        _view.SetAdvanceHint(false);
        _isAwaitingAdvance = false;
    }

    private Tween BuildOpeningFade() => _view.BuildOpeningFade(_fadeDuration);

    private Tween BuildSlimeIntroduction() => _view.BuildSlimeIntroduction(
        _slimeIntroductionOrigins,
        _introHoldDuration,
        PlaySlimeArrivalSound,
        () => AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock));

    private Vector2[] BuildSlimeIntroductionOrigins()
    {
        int count = SlimeStatusSaveData.NormalCollectionSize;
        var origins = new Vector2[count];
        var found = new bool[count];
        Camera worldCamera = Camera.main;
        Rect column = PortraitColumn.GetPixelRect();

        if (worldCamera != null && _spawnManager != null)
        {
            foreach (SlimeController target in _spawnManager.GetActiveTargets())
            {
                if (target == null ||
                    target.Location != ESlimeLocation.DisplayRoom)
                {
                    continue;
                }

                int index = (int)target.Grade - (int)ESlimeGrade.Grade1;
                if (index < 0 || index >= count || found[index]) continue;

                Vector3 screen = worldCamera.WorldToScreenPoint(target.transform.position);
                origins[index] = new Vector2(
                    Mathf.Clamp(screen.x, column.xMin + column.width * 0.08f, column.xMin + column.width * 0.92f),
                    Mathf.Clamp(screen.y, Screen.height * 0.16f, Screen.height * 0.84f));
                found[index] = true;
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (found[i]) continue;

            float x = Mathf.Lerp(column.xMin + column.width * 0.14f, column.xMin + column.width * 0.86f,
                (i % 4) / 3f);
            origins[i] = new Vector2(x, Screen.height * 0.18f);
        }

        return origins;
    }

    private static void PlaySlimeArrivalSound()
    {
        // 화면을 건너뛰어 콜백이 한 프레임에 몰려도 소리가 겹쳐 터지지 않게 한다.
        AudioManager.Instance?.PlaySFXRandomPitchWithCooldown(
            EAudioSfx.SlimeBounce, 0.055f, 0.88f, 1.12f);
    }

    private Tween BuildCredits()
    {
        MainEndingCreditsTextBuilder.BuildGraduationColumns(
            _slimeManager,
            out string left,
            out string right);
        return _view.BuildCredits(
            left,
            right,
            _creditScrollDuration);
    }

    private Tween BuildSpecialSlimeTeaser() => _view.BuildSpecialSlimeTeaser(
        _fadeDuration);

    // 마지막 화면(예고)에 닿았다. 건너뛰기로 왔더라도 이 화면은 보여 주고 터치를 기다린다.
    // 시청 기록은 여기서 남긴다. 크레딧 도중에 앱을 끄면 다음에 다시 나온다.
    private void EnterFinalScreen()
    {
        _isFinalScreen = true;
        _skipToFinalRequested = false;
        _advanceRequested = false;
        if (!_isReplay)
        {
            _slimeManager.TryMarkMainEndingSeen();
        }
    }

    private void StartFastForward()
    {
        if (!_isPresenting || _isFinalScreen || _isAwaitingAdvance) return;
        _isFastForwardHeld = true;
        if (_phaseTween != null) _phaseTween.timeScale = _holdFastForwardScale;
    }

    private void StopFastForward()
    {
        _isFastForwardHeld = false;
        if (_phaseTween != null) _phaseTween.timeScale = 1f;
    }

    private void RequestAdvance()
    {
        if (!_isPresenting || !_isAwaitingAdvance) return;
        _advanceRequested = true;
        AudioManager.Instance?.PlaySFX(EAudioSfx.UIClick);
    }

    private bool HandleBack()
    {
        if (!_isPresenting) return false;
        if (_closeTween != null) return true;

        // 마지막 화면에서는 터치와 같다. 끝내면 코루틴이 이어서 닫는다.
        if (_isFinalScreen)
        {
            _advanceRequested = true;
            return true;
        }

        _skipToFinalRequested = true;
        _advanceRequested = true;
        _view.SetAdvanceHint(false);
        _phaseTween?.Complete(withCallbacks: true);
        return true;
    }

    private void EndPresentation()
    {
        if (!_isPresenting || _closeTween != null) return;

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
            _shopUI?.ReleaseStandDown(this, animated);
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
