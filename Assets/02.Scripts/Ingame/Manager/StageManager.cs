using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private StageUI _stageUI;
    [SerializeField] private StageTransitionPlayer _transitionPlayer;
    [SerializeField] private TutorialDialogueView _dialoguePrefab;
    [SerializeField] private TutorialSpotlightView _guidePrefab;
    [SerializeField] private TutorialContent _tutorialContent;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;

    [Header("Intro")]
    [SerializeField, Min(0f)] private float _firstChargeDuration = 0.8f;

    private EGameStage _currentStage = EGameStage.Ground;
    private TutorialPresentation _tutorialPresentation;
    private SlimeController _pendingFirstSkyTarget;
    private Sequence _chargeSequence;
    private bool _isInitializeStarted;
    private bool _isInitialized;
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

        _chargeSequence?.Kill();
        _tutorialPresentation?.Dispose();
    }

    public bool IsStageActive(ESlimeGrade grade)
    {
        return !_transitionPlayer.IsTransitioning &&
               GameStageRules.GetStage(grade) == _currentStage;
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _clicker != null &&
                             _autoClicker != null &&
                             _upgradeUI != null &&
                             _canvas != null &&
                             _stageUI != null &&
                             _dialoguePrefab != null &&
                             _guidePrefab != null &&
                             _tutorialContent != null &&
                             _unlockPopupUI != null &&
                             _transitionPlayer != null;
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
        _transitionPlayer.ApplyEnvironment(_currentStage, 0f);
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

        _transitionPlayer.PlayRegularSkyTransfer(target, _currentStage);
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        if (grade != ESlimeGrade.Grade11 ||
            _pendingFirstSkyTarget == null ||
            _transitionPlayer.IsTransitioning)
        {
            return;
        }

        BeginFirstSkyIntro();
    }

    private void BeginFirstSkyIntro()
    {
        if (_pendingFirstSkyTarget == null ||
            _transitionPlayer.IsTransitioning ||
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
            _transitionPlayer.IsTransitioning ||
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
        if (_transitionPlayer.IsTransitioning || targetStage == _currentStage)
        {
            onComplete?.Invoke();
            return;
        }

        SetInteractionEnabled(false);

        _transitionPlayer.Play(
            targetStage,
            travellingSlime,
            onStageSwitched: () =>
            {
                _currentStage = targetStage;
                ApplyAllSlimeVisibility();
            },
            onCompleted: () =>
            {
                ApplyAllSlimeVisibility();

                if (saveStage && SlimeManager.Instance != null)
                {
                    SlimeManager.Instance.UpdateStageProgress(
                        _currentStage,
                        SlimeManager.Instance.SkyIntroCompleted);
                }

                _stageUI.SetStage(_currentStage);
                SetInteractionEnabled(true);
                onComplete?.Invoke();
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


    private void SetInteractionEnabled(bool isEnabled)
    {
        _clicker.SetInputMode(isEnabled, isEnabled);
        _upgradeUI.SetToggleInputEnabled(isEnabled);
    }

}
