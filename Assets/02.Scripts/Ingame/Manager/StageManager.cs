using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum EGameplaySpace
{
    MainStage,
    DisplayRoom,
}

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
    private EGameplaySpace _currentSpace = EGameplaySpace.MainStage;
    private bool _isInitializeStarted;
    private bool _isInitialized;

    public EGameStage CurrentStage => _currentStage;
    public EGameplaySpace CurrentSpace => _currentSpace;
    public bool IsMainStageActive => _currentSpace == EGameplaySpace.MainStage;
    public bool IsTransitioning => _transitionPlayer != null &&
                                   _transitionPlayer.IsTransitioning;
    public event Action<EGameStage> StageChanged;
    public event Action StageTransitionCompleted;
    public event Action<EGameplaySpace> SpaceChanged;

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
        return IsMainStageActive &&
               !_transitionPlayer.IsTransitioning &&
               GameStageRules.GetStage(grade) == _currentStage;
    }

    public bool TryEnterDisplayRoom()
    {
        if (!_isInitialized ||
            !IsMainStageActive ||
            _transitionPlayer.IsTransitioning ||
            GameManager.Instance == null ||
            !GameManager.Instance.IsGameplayActive ||
            SlimeManager.Instance == null ||
            !SlimeManager.Instance.IsDisplayRoomUnlocked ||
            (SlimeManager.Instance.IsSkyUnlocked &&
             !SlimeManager.Instance.SkyIntroCompleted))
        {
            return false;
        }

        _upgradeUI.TryClose();
        SetInteractionEnabled(false);
        _transitionPlayer.PlaySpace(
            EGameplaySpace.DisplayRoom,
            () => SetCurrentSpace(EGameplaySpace.DisplayRoom),
            onCompleted: RefreshInteraction);
        return true;
    }

    // 연출 값은 StageTransitionPlayer가 소유하므로 UI는 이 경계로만 호출한다.
    public void PlayDisplayRoomTransfer(SlimeController target, Action onComplete)
    {
        if (_transitionPlayer == null)
        {
            onComplete?.Invoke();
            return;
        }

        _transitionPlayer.PlayDisplayRoomTransfer(target, onComplete);
    }

    public void FocusDisplayRoomSlime(SlimeController target, Action onComplete)
    {
        if (_transitionPlayer == null)
        {
            onComplete?.Invoke();
            return;
        }

        _transitionPlayer.FocusDisplayRoomSlime(target, onComplete);
    }

    public void RestoreDisplayRoomFocus(Action onComplete = null)
    {
        if (_transitionPlayer == null)
        {
            onComplete?.Invoke();
            return;
        }

        _transitionPlayer.RestoreDisplayRoomFocus(onComplete);
    }

    public void BeginDisplayRoomObservation(Action onComplete = null)
    {
        _transitionPlayer.BeginDisplayRoomObservation(onComplete);
    }

    public void EndDisplayRoomObservation(Action onComplete = null)
    {
        _transitionPlayer.EndDisplayRoomObservation(onComplete);
    }

    public bool TryExitDisplayRoom()
    {
        if (!_isInitialized ||
            IsMainStageActive ||
            _transitionPlayer.IsTransitioning)
        {
            return false;
        }

        _transitionPlayer.PlaySpace(
            EGameplaySpace.MainStage,
            () => SetCurrentSpace(EGameplaySpace.MainStage),
            onCompleted: RefreshInteraction);
        return true;
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

        RefreshSlimePresentation(target);
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
            !IsMainStageActive ||
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
                StageTransitionCompleted?.Invoke();
            });
    }

    private void ApplyAllSlimeVisibility()
    {
        if (SlimeSpawner.Instance == null) return;

        foreach (SlimeController target in SlimeSpawner.Instance.GetActiveTargets())
        {
            if (target == null) continue;

            RefreshSlimePresentation(target);
        }
    }

    public void RefreshSlimePresentation(SlimeController target)
    {
        if (target == null) return;

        bool isVisible = IsMainStageActive
            ? target.Location == ESlimeLocation.MainStage &&
              GameStageRules.GetStage(target.Grade) == _currentStage
            : target.Location == ESlimeLocation.DisplayRoom;
        target.SetStagePresentationActive(isVisible);
    }

    private void SetCurrentSpace(EGameplaySpace space)
    {
        if (_currentSpace == space) return;

        _currentSpace = space;
        ApplyAllSlimeVisibility();
        _stageUI.SetButtonVisible(
            IsMainStageActive &&
            SlimeManager.Instance != null &&
            SlimeManager.Instance.IsSkyUnlocked,
            animated: false);
        SpaceChanged?.Invoke(_currentSpace);
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

    // 공간별 입력 정책을 한곳에서 정한다.
    // 장식장에서는 기획서 §7.2대로 클릭 포인트와 드래그 합성을 막고 선택만 허용한다.
    private void SetInteractionEnabled(bool isEnabled)
    {
        _upgradeUI.SetToggleInputEnabled(isEnabled && IsMainStageActive);

        if (!isEnabled)
        {
            _clicker.SetInputMode(false, false);
            return;
        }

        if (IsMainStageActive)
        {
            _clicker.SetInputMode(true, true);
            return;
        }

        _clicker.SetInputMode(
            clickEnabled: true,
            dragEnabled: false,
            invokeClickAction: false);
    }

    // 팝업이나 연출이 끝난 뒤 현재 공간에 맞는 입력 상태로 되돌린다.
    public void RefreshInteraction()
    {
        SetInteractionEnabled(
            GameManager.Instance != null &&
            GameManager.Instance.IsGameplayActive);
    }
}
