using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private Clicker _clicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private StageUI _stageUI;
    [SerializeField] private StageTransitionPlayer _transitionPlayer;
    [SerializeField] private SkyIntroDirector _skyIntroDirector;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;

    private EGameStage _currentStage = EGameStage.Ground;
    private bool _isInitializeStarted;
    private bool _isInitialized;

    public EGameStage CurrentStage => _currentStage;
    public event Action<EGameStage> StageChanged;

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
        _skyIntroDirector.SkyTransitionRequested += OnSkyTransitionRequested;
        _skyIntroDirector.InteractionEnableRequested += SetInteractionEnabled;

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

        if (_skyIntroDirector != null)
        {
            _skyIntroDirector.SkyTransitionRequested -= OnSkyTransitionRequested;
            _skyIntroDirector.InteractionEnableRequested -= SetInteractionEnabled;
        }
    }

    public bool IsStageActive(ESlimeGrade grade)
    {
        return !_transitionPlayer.IsTransitioning &&
               GameStageRules.GetStage(grade) == _currentStage;
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _clicker != null &&
                             _upgradeUI != null &&
                             _skyIntroDirector != null &&
                             _stageUI != null &&
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
        StageChanged?.Invoke(_currentStage);
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
                _skyIntroDirector.Prepare(skyTarget);
                _skyIntroDirector.Begin();
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
            !GameStageRules.IsSkyEntryMerge(fromGrade, toGrade))
        {
            return;
        }

        target.PrepareStageTransfer();

        if (SlimeManager.Instance != null &&
            !SlimeManager.Instance.SkyIntroCompleted)
        {
            _skyIntroDirector.Prepare(target);
            SetInteractionEnabled(false);

            // 해금 팝업이 재생 중일 때만 PresentationCompleted가 온다.
            // 이미 해금된 등급이면 팝업이 뜨지 않으므로 바로 인트로를 시작한다.
            if (!_unlockPopupUI.IsPresenting)
            {
                _skyIntroDirector.Begin();
            }

            return;
        }

        _transitionPlayer.PlayRegularSkyTransfer(target, _currentStage);
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        if (!GameStageRules.IsSkyEntryGrade(grade) ||
            !_skyIntroDirector.HasPendingTarget ||
            _transitionPlayer.IsTransitioning)
        {
            return;
        }

        _skyIntroDirector.Begin();
    }

    private void OnSkyTransitionRequested(SlimeController target, Action onArrived)
    {
        StartStageTransition(
            EGameStage.Sky,
            target,
            onArrived,
            saveStage: false);
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

        if (_skyIntroDirector.IsWaitingForStageButton)
        {
            _skyIntroDirector.HideSpotlight();
            StartStageTransition(
                targetStage,
                null,
                _skyIntroDirector.Complete,
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
                StageChanged?.Invoke(_currentStage);
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
