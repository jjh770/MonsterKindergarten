using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private StageUI _stageUI;
    [SerializeField] private TutorialDialogueView _dialoguePrefab;
    [SerializeField] private TutorialSpotlightView _guidePrefab;
    [SerializeField] private TutorialContent _tutorialContent;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private SpriteRenderer _skyBackgroundRenderer;

    [Header("Stage Audio")]
    [SerializeField] private AudioClip _groundBgm;
    [SerializeField] private AudioClip _skyBgm;

    [Header("Transition")]
    [SerializeField, Min(0.1f)] private float _transitionDuration = 1.2f;
    [SerializeField, Min(1f)] private float _cameraTravelDistance = 6f;
    [SerializeField, Min(0f)] private float _firstChargeDuration = 0.8f;

    private EGameStage _currentStage = EGameStage.Ground;
    private Vector3 _cameraBasePosition;
    private TutorialPresentation _tutorialPresentation;
    private SlimeController _pendingFirstSkyTarget;
    private Sequence _transitionSequence;
    private Sequence _chargeSequence;
    private bool _isInitializeStarted;
    private bool _isInitialized;
    private bool _isTransitioning;
    private bool _isFirstIntroStarted;
    private bool _isWaitingForStageButtonTutorial;

    public EGameStage CurrentStage => _currentStage;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _cameraBasePosition = _camera.transform.position;
        _stageUI.ButtonClicked += OnStageButtonClicked;

        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        MergeManager.Merged += OnMerged;
        _unlockPopupUI.PresentationCompleted += OnUnlockPresentationCompleted;

        if (SlimeSpawner.Instance != null)
        {
            SlimeSpawner.Instance.Spawned += OnSlimeSpawned;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        MergeManager.Merged -= OnMerged;

        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted -= OnUnlockPresentationCompleted;
        }

        if (SlimeSpawner.Instance != null)
        {
            SlimeSpawner.Instance.Spawned -= OnSlimeSpawned;
        }

        if (_stageUI != null)
        {
            _stageUI.ButtonClicked -= OnStageButtonClicked;
        }

        _transitionSequence?.Kill();
        _chargeSequence?.Kill();
        _tutorialPresentation?.Dispose();
    }

    public bool IsStageActive(ESlimeGrade grade)
    {
        return !_isTransitioning &&
               GameStageRules.GetStage(grade) == _currentStage;
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _camera != null &&
                             _clicker != null &&
                             _autoClicker != null &&
                             _upgradeUI != null &&
                             _canvas != null &&
                             _stageUI != null &&
                             _dialoguePrefab != null &&
                             _guidePrefab != null &&
                             _tutorialContent != null &&
                             _unlockPopupUI != null &&
                             _skyBackgroundRenderer != null;
        if (!hasReferences)
        {
            Debug.LogError("스테이지 매니저의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnAllDataInitialized()
    {
        if (_isInitializeStarted) return;

        _isInitializeStarted = true;
        InitializeAfterDataAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid InitializeAfterDataAsync(CancellationToken token)
    {
        if (SlimeManager.Instance == null)
        {
            _isInitializeStarted = false;
            return;
        }

        _currentStage = SlimeManager.Instance.IsSkyUnlocked
            ? SlimeManager.Instance.CurrentStage
            : EGameStage.Ground;
        _isInitialized = true;
        _stageUI.SetStage(_currentStage);
        ApplyEnvironment(_currentStage, 0f);
        ApplyAllSlimeVisibility();
        _stageUI.SetButtonVisible(false, false);
        SetInteractionEnabled(false);

        await WaitForGameplayActiveAsync(token);

        SetInteractionEnabled(true);

        if (!SlimeManager.Instance.IsSkyUnlocked)
        {
            _stageUI.SetButtonVisible(false, false);
        }
        else if (SlimeManager.Instance.SkyIntroCompleted)
        {
            _stageUI.SetButtonVisible(true, false);
        }
        else
        {
            SlimeController skyTarget = FindFirstSkySlime();
            if (skyTarget != null)
            {
                _pendingFirstSkyTarget = skyTarget;
                BeginFirstSkyIntro();
            }
            else
            {
                SlimeManager.Instance.UpdateStageProgress(
                    EGameStage.Ground,
                    skyIntroCompleted: true);
                _stageUI.SetButtonVisible(true, false);
            }
        }
    }

    // 오프라인 보상 팝업 등으로 게임플레이가 잠겨 있으면 활성화 이벤트를 기다린다.
    private static async UniTask WaitForGameplayActiveAsync(CancellationToken token)
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.IsGameplayActive) return;

        var completionSource = new UniTaskCompletionSource();
        void OnActivated() => completionSource.TrySetResult();

        gameManager.OnGameplayActivated += OnActivated;
        try
        {
            await completionSource.Task.AttachExternalCancellation(token);
        }
        finally
        {
            gameManager.OnGameplayActivated -= OnActivated;
        }
    }

    private void OnSlimeSpawned(SlimeController target)
    {
        if (!_isInitialized || target == null) return;

        bool isActive = GameStageRules.GetStage(target.Grade) == _currentStage;
        target.SetStagePresentationActive(isActive);
    }

    private void OnMerged(
        SlimeController target,
        ESlimeGrade fromGrade,
        ESlimeGrade toGrade)
    {
        if (target == null ||
            fromGrade != ESlimeGrade.Grade10 ||
            toGrade != ESlimeGrade.Grade11)
        {
            return;
        }

        target.PrepareStageTransfer();

        if (SlimeManager.Instance != null &&
            !SlimeManager.Instance.SkyIntroCompleted)
        {
            _pendingFirstSkyTarget = target;
            SetInteractionEnabled(false);

            // 해금 팝업이 재생 중일 때만 PresentationCompleted가 온다.
            // 이미 해금된 등급이면 팝업이 뜨지 않으므로 바로 인트로를 시작한다.
            if (!_unlockPopupUI.IsPresenting)
            {
                BeginFirstSkyIntro();
            }

            return;
        }

        PlayRegularSkyTransfer(target);
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        if (grade != ESlimeGrade.Grade11 ||
            _pendingFirstSkyTarget == null ||
            _isTransitioning)
        {
            return;
        }

        BeginFirstSkyIntro();
    }

    private void BeginFirstSkyIntro()
    {
        if (_pendingFirstSkyTarget == null ||
            _isTransitioning ||
            _isFirstIntroStarted)
        {
            return;
        }

        _isFirstIntroStarted = true;
        SetInteractionEnabled(false);
        _pendingFirstSkyTarget.PrepareStageTransfer();
        _tutorialPresentation?.Dispose();
        _tutorialPresentation = new TutorialPresentation(
            _canvas,
            _canvas,
            _dialoguePrefab,
            _guidePrefab);
        _tutorialPresentation.ShowDialogue(
            _tutorialContent.SkyIntroDialogue,
            PlayFirstSkyJourney);
    }

    private void PlayFirstSkyJourney()
    {
        SlimeController target = _pendingFirstSkyTarget;
        if (target == null)
        {
            CompleteFirstSkyIntroWithoutTarget();
            return;
        }

        target.PrepareStageTransfer();
        _chargeSequence?.Kill();
        _chargeSequence = DOTween.Sequence();
        _chargeSequence.Append(
            target.transform.DOPunchScale(
                new Vector3(0.25f, -0.18f, 0f),
                Mathf.Max(0.1f, _firstChargeDuration),
                5,
                0.7f));
        _chargeSequence.OnComplete(() =>
        {
            _chargeSequence = null;
            StartStageTransition(
                EGameStage.Sky,
                target,
                OnFirstSkyArrival,
                saveStage: false);
        });
    }

    private void OnFirstSkyArrival()
    {
        SlimeManager.Instance?.UpdateStageProgress(
            EGameStage.Sky,
            skyIntroCompleted: true);
        _pendingFirstSkyTarget = null;
        _isFirstIntroStarted = false;
        _stageUI.SetButtonVisible(true, true);

        RectTransform buttonTarget = _stageUI.ButtonTarget;
        if (_tutorialPresentation == null || buttonTarget == null)
        {
            CompleteStageButtonTutorial();
            return;
        }

        _isWaitingForStageButtonTutorial = true;
        _tutorialPresentation.Spotlight.ShowUiTarget(
            _tutorialContent.StageButtonMessage,
            buttonTarget,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void CompleteFirstSkyIntroWithoutTarget()
    {
        SlimeManager.Instance?.UpdateStageProgress(
            EGameStage.Ground,
            skyIntroCompleted: true);
        _stageUI.SetButtonVisible(true, true);
        CompleteStageButtonTutorial();
    }

    private void OnStageButtonClicked()
    {
        if (!_isInitialized ||
            _isTransitioning ||
            GameManager.Instance == null ||
            !GameManager.Instance.IsGameplayActive ||
            SlimeManager.Instance == null ||
            !SlimeManager.Instance.IsSkyUnlocked)
        {
            return;
        }

        _upgradeUI.TryClose();
        EGameStage targetStage = _currentStage == EGameStage.Ground
            ? EGameStage.Sky
            : EGameStage.Ground;

        if (_isWaitingForStageButtonTutorial)
        {
            _tutorialPresentation?.Spotlight.Hide();
            StartStageTransition(
                targetStage,
                null,
                CompleteStageButtonTutorial,
                saveStage: true);
            return;
        }

        StartStageTransition(
            targetStage,
            null,
            onComplete: null,
            saveStage: true);
    }

    private void StartStageTransition(
        EGameStage targetStage,
        SlimeController travellingSlime,
        Action onComplete,
        bool saveStage)
    {
        if (_isTransitioning || targetStage == _currentStage)
        {
            onComplete?.Invoke();
            return;
        }

        _isTransitioning = true;
        SetInteractionEnabled(false);
        _stageUI.SetButtonInteractable(false);
        _stageUI.BeginOverlay();

        float direction = targetStage == EGameStage.Sky ? 1f : -1f;
        float halfDuration = _transitionDuration * 0.5f;
        Vector2 slimeDestination = SpawnManager.Instance != null
            ? SpawnManager.Instance.GetRandomSpawnPosition()
            : Vector2.zero;
        Vector3 slimeStart = travellingSlime != null
            ? travellingSlime.transform.position
            : Vector3.zero;

        if (travellingSlime != null)
        {
            travellingSlime.PrepareStageTransfer();
        }

        AudioClip targetBgm = targetStage == EGameStage.Ground
            ? _groundBgm
            : _skyBgm;
        AudioManager.Instance?.CrossFadeBGM(targetBgm, _transitionDuration);

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence();
        _transitionSequence.Join(
            _camera.transform.DOMoveY(
                _cameraBasePosition.y + direction * _cameraTravelDistance,
                halfDuration).SetEase(Ease.InQuad));
        _transitionSequence.Join(
            _stageUI.FadeOverlay(1f, halfDuration));

        if (travellingSlime != null)
        {
            _transitionSequence.Join(
                travellingSlime.transform.DOMoveY(
                    slimeStart.y + direction * _cameraTravelDistance,
                    halfDuration).SetEase(Ease.InQuad));
        }

        _transitionSequence.AppendCallback(() =>
        {
            _currentStage = targetStage;
            ApplyEnvironment(targetStage, crossFadeDuration: -1f);
            ApplyAllSlimeVisibility();

            Vector3 cameraPosition = _cameraBasePosition;
            cameraPosition.y -= direction * _cameraTravelDistance;
            _camera.transform.position = cameraPosition;

            if (travellingSlime != null)
            {
                travellingSlime.PrepareStageTransfer();
                travellingSlime.transform.position = new Vector3(
                    slimeDestination.x,
                    slimeDestination.y - direction * _cameraTravelDistance,
                    slimeStart.z);
            }
        });
        _transitionSequence.Append(
            _camera.transform.DOMoveY(
                _cameraBasePosition.y,
                halfDuration).SetEase(Ease.OutQuad));
        _transitionSequence.Join(
            _stageUI.FadeOverlay(0f, halfDuration));

        if (travellingSlime != null)
        {
            _transitionSequence.Join(
                travellingSlime.transform.DOMove(
                    new Vector3(
                        slimeDestination.x,
                        slimeDestination.y,
                        slimeStart.z),
                    halfDuration).SetEase(Ease.OutQuad));
        }

        _transitionSequence.OnComplete(() =>
        {
            _transitionSequence = null;
            _camera.transform.position = _cameraBasePosition;
            _stageUI.EndOverlay();
            _isTransitioning = false;
            ApplyAllSlimeVisibility();

            if (saveStage && SlimeManager.Instance != null)
            {
                SlimeManager.Instance.UpdateStageProgress(
                    _currentStage,
                    SlimeManager.Instance.SkyIntroCompleted);
            }

            _stageUI.SetStage(_currentStage);
            _stageUI.SetButtonInteractable(true);
            SetInteractionEnabled(true);
            onComplete?.Invoke();
        });
    }

    private void PlayRegularSkyTransfer(SlimeController target)
    {
        if (target == null) return;

        target.PrepareStageTransfer();
        Vector3 startPosition = target.transform.position;
        Vector2 destination = SpawnManager.Instance != null
            ? SpawnManager.Instance.GetRandomSpawnPosition()
            : Vector2.zero;
        target.transform.DOMoveY(
                startPosition.y + _cameraTravelDistance,
                Mathf.Min(0.9f, _transitionDuration))
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                if (target == null) return;

                target.transform.position = new Vector3(
                    destination.x,
                    destination.y,
                    startPosition.z);
                bool isActive = _currentStage == EGameStage.Sky;
                target.SetStagePresentationActive(isActive);
            });
    }

    private void CompleteStageButtonTutorial()
    {
        _isWaitingForStageButtonTutorial = false;
        _tutorialPresentation?.Dispose();
        _tutorialPresentation = null;
        SetInteractionEnabled(true);
    }

    private void ApplyAllSlimeVisibility()
    {
        if (SlimeSpawner.Instance == null) return;

        foreach (SlimeController target in SlimeSpawner.Instance.GetActiveTargets())
        {
            if (target == null) continue;

            bool isActive = GameStageRules.GetStage(target.Grade) == _currentStage;
            target.SetStagePresentationActive(isActive);
        }
    }

    private SlimeController FindFirstSkySlime()
    {
        if (SlimeSpawner.Instance == null) return null;

        foreach (SlimeController target in SlimeSpawner.Instance.GetActiveTargets())
        {
            if (target != null &&
                GameStageRules.GetStage(target.Grade) == EGameStage.Sky)
            {
                return target;
            }
        }

        return null;
    }

    private void ApplyEnvironment(
        EGameStage stage,
        float crossFadeDuration)
    {
        _skyBackgroundRenderer.enabled = true;
        _camera.backgroundColor = Color.white;

        if (crossFadeDuration < 0f || AudioManager.Instance == null) return;

        bool isGround = stage == EGameStage.Ground;
        AudioClip targetBgm = isGround ? _groundBgm : _skyBgm;
        AudioManager.Instance.CrossFadeBGM(targetBgm, crossFadeDuration);
    }

    private void SetInteractionEnabled(bool isEnabled)
    {
        _clicker.SetInputMode(isEnabled, isEnabled);
        _upgradeUI.SetToggleInputEnabled(isEnabled);
    }

}
