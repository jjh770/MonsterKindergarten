using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

public enum EGameplaySpace
{
    MainField,
    DisplayRoom,
}

public sealed class GameplaySpaceManager : MonoBehaviour
{
    public static GameplaySpaceManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private Clicker _clicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [FormerlySerializedAs("_stageUI")]
    [SerializeField] private BackgroundThemeUI _backgroundThemeUI;
    [SerializeField] private GameplayTransitionPlayer _transitionPlayer;
    [FormerlySerializedAs("_skyIntroDirector")]
    [SerializeField] private BackgroundThemeUnlockDirector _backgroundThemeUnlockDirector;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;

    private EBackgroundTheme _currentBackgroundTheme = EBackgroundTheme.Ground;
    private EGameplaySpace _currentSpace = EGameplaySpace.MainField;
    private bool _isInitializeStarted;
    private bool _isInitialized;

    public EBackgroundTheme CurrentBackgroundTheme => _currentBackgroundTheme;
    public EGameplaySpace CurrentSpace => _currentSpace;
    public bool IsMainFieldActive => _currentSpace == EGameplaySpace.MainField;
    public bool IsTransitioning => _transitionPlayer != null &&
                                   _transitionPlayer.IsTransitioning;
    public bool IsMainFieldInteractionActive =>
        IsMainFieldActive && !IsTransitioning;
    public event Action<EBackgroundTheme> BackgroundThemeChanged;
    public event Action BackgroundThemeTransitionCompleted;
    public event Action<EGameplaySpace> SpaceChanged;
    public event Action SpaceTransitionCompleted;

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

        _backgroundThemeUI.ButtonClicked += OnBackgroundButtonClicked;
        _backgroundThemeUI.ThemeSelected += OnBackgroundThemeSelected;
        _backgroundThemeUnlockDirector.BackgroundThemeTransitionRequested +=
            OnBackgroundThemeTransitionRequested;
        _backgroundThemeUnlockDirector.InteractionEnableRequested +=
            SetInteractionEnabled;

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

        if (_backgroundThemeUI != null)
        {
            _backgroundThemeUI.ButtonClicked -= OnBackgroundButtonClicked;
            _backgroundThemeUI.ThemeSelected -= OnBackgroundThemeSelected;
        }

        if (_backgroundThemeUnlockDirector != null)
        {
            _backgroundThemeUnlockDirector.BackgroundThemeTransitionRequested -=
                OnBackgroundThemeTransitionRequested;
            _backgroundThemeUnlockDirector.InteractionEnableRequested -=
                SetInteractionEnabled;
        }
    }

    public bool TryEnterDisplayRoom()
    {
        if (!_isInitialized ||
            !IsMainFieldActive ||
            _transitionPlayer.IsTransitioning ||
            !GameplayGate.IsActive ||
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
            onCompleted: CompleteSpaceTransition);
        return true;
    }

    // 연출 값은 GameplayTransitionPlayer가 소유하므로 UI는 이 경계로만 호출한다.
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
        if (_transitionPlayer == null)
        {
            onComplete?.Invoke();
            return;
        }

        _transitionPlayer.BeginDisplayRoomObservation(onComplete);
    }

    public void EndDisplayRoomObservation(Action onComplete = null)
    {
        if (_transitionPlayer == null)
        {
            onComplete?.Invoke();
            return;
        }

        _transitionPlayer.EndDisplayRoomObservation(onComplete);
    }

    public bool TryExitDisplayRoom()
    {
        if (!_isInitialized ||
            IsMainFieldActive ||
            _transitionPlayer.IsTransitioning)
        {
            return false;
        }

        _transitionPlayer.PlaySpace(
            EGameplaySpace.MainField,
            () => SetCurrentSpace(EGameplaySpace.MainField),
            onCompleted: CompleteSpaceTransition);
        return true;
    }

    private void CompleteSpaceTransition()
    {
        RefreshInteraction();
        SpaceTransitionCompleted?.Invoke();
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _clicker != null &&
                             _upgradeUI != null &&
                             _backgroundThemeUnlockDirector != null &&
                             _backgroundThemeUI != null &&
                             _unlockPopupUI != null &&
                             _transitionPlayer != null;
        if (!hasReferences)
        {
            Debug.LogError("게임플레이 공간 매니저의 필수 참조가 비어 있습니다.", this);
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

        _currentBackgroundTheme = SlimeManager.Instance.IsSkyUnlocked
            ? SlimeManager.Instance.SelectedBackgroundTheme
            : EBackgroundTheme.Ground;
        _isInitialized = true;
        _transitionPlayer.ApplyEnvironment(_currentBackgroundTheme, 0f);
        BackgroundThemeChanged?.Invoke(_currentBackgroundTheme);
        _backgroundThemeUI.SetSelectedTheme(_currentBackgroundTheme);
        ApplyAllSlimeVisibility();
        RefreshBackgroundButton();
        SetInteractionEnabled(false);

        await WaitForGameplayActiveAsync(token);

        SetInteractionEnabled(true);

        // 하늘 안내를 아직 못 본 계정만 여기서 인트로를 띄운다. 버튼 노출은
        // 규칙 하나가 정하므로 분기마다 따로 켜고 끄지 않는다.
        RefreshBackgroundButton();
        if (SlimeManager.Instance.IsSkyUnlocked &&
            !SlimeManager.Instance.SkyIntroCompleted)
        {
            SlimeController skyTarget = FindFirstSkySlime();
            if (skyTarget != null)
            {
                _backgroundThemeUnlockDirector.Prepare(skyTarget);
                _backgroundThemeUnlockDirector.Begin();
            }
            else
            {
                SlimeManager.Instance.UpdateBackgroundProgress(
                    EBackgroundTheme.Ground,
                    backgroundUnlockCompleted: true);
                RefreshBackgroundButton();
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
            !BackgroundThemeRules.IsUnlockMerge(fromGrade, toGrade))
        {
            return;
        }

        if (SlimeManager.Instance != null &&
            !SlimeManager.Instance.SkyIntroCompleted)
        {
            target.PreparePresentationTransfer();
            _backgroundThemeUnlockDirector.Prepare(target);
            SetInteractionEnabled(false);

            // 해금 팝업이 재생 중일 때만 PresentationCompleted가 온다.
            // 이미 해금된 등급이면 팝업이 뜨지 않으므로 바로 인트로를 시작한다.
            if (!_unlockPopupUI.IsPresenting)
            {
                _backgroundThemeUnlockDirector.Begin();
            }

            return;
        }

        RefreshSlimePresentation(target);
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        if (!BackgroundThemeRules.IsUnlockGrade(grade) ||
            !_backgroundThemeUnlockDirector.HasPendingTarget ||
            _transitionPlayer.IsTransitioning)
        {
            return;
        }

        _backgroundThemeUnlockDirector.Begin();
    }

    private void OnBackgroundThemeTransitionRequested(SlimeController target, Action onArrived)
    {
        StartBackgroundThemeTransition(
            EBackgroundTheme.Sky,
            onArrived,
            saveTheme: false);
    }

    private void OnBackgroundButtonClicked()
    {
        if (!_isInitialized ||
            !IsMainFieldActive ||
            _transitionPlayer.IsTransitioning ||
            !GameplayGate.IsActive ||
            SlimeManager.Instance == null ||
            !SlimeManager.Instance.IsSkyUnlocked)
        {
            return;
        }

        if (_backgroundThemeUnlockDirector.IsWaitingForBackgroundButton)
        {
            // 첫 안내에서는 이동하지 않고 버튼 설명 다음 대화로 이어진다.
            _backgroundThemeUnlockDirector.AdvanceBackgroundButtonStep();
            return;
        }

        _upgradeUI.TryClose();
        _backgroundThemeUI.ToggleThemeSelector();
    }

    private void OnBackgroundThemeSelected(EBackgroundTheme theme)
    {
        if (!_isInitialized ||
            !IsMainFieldActive ||
            _transitionPlayer.IsTransitioning ||
            !GameplayGate.IsActive ||
            SlimeManager.Instance == null ||
            !SlimeManager.Instance.IsSkyUnlocked ||
            !BackgroundThemeRules.IsValid(theme) ||
            // 지금 보고 있는 배경을 다시 고른 것이라 바꿀 것이 없다. 그냥 두면
            // 밀 자리가 없어 화면을 덮는 쪽으로 떨어져, 같은 버튼이 다른 연출을 낸다.
            theme == _currentBackgroundTheme)
        {
            return;
        }

        StartBackgroundThemeTransition(
            theme,
            onComplete: null,
            saveTheme: true);
    }

    private void StartBackgroundThemeTransition(
        EBackgroundTheme targetTheme,
        Action onComplete,
        bool saveTheme)
    {
        if (_transitionPlayer.IsTransitioning)
        {
            onComplete?.Invoke();
            return;
        }

        SetInteractionEnabled(false);

        _transitionPlayer.PlayBackgroundTheme(
            targetTheme,
            onThemeSwitched: () =>
            {
                _currentBackgroundTheme = targetTheme;
                BackgroundThemeChanged?.Invoke(_currentBackgroundTheme);
                _backgroundThemeUI.SetSelectedTheme(_currentBackgroundTheme);
            },
            onCompleted: () =>
            {
                if (saveTheme && SlimeManager.Instance != null)
                {
                    SlimeManager.Instance.UpdateBackgroundProgress(
                        _currentBackgroundTheme,
                        SlimeManager.Instance.BackgroundUnlockCompleted);
                }

                SetInteractionEnabled(true);
                onComplete?.Invoke();
                BackgroundThemeTransitionCompleted?.Invoke();
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

    // PlayDisplayRoomTransfer의 완료 처리다. 저장 위치를 옮기고 새 자리에 배치한 뒤
    // 표시를 갱신한다. 실패하면 연출 시작 전 자리로 되돌린다.
    //
    // 연출 전반부를 이 클래스가 소유하므로 후반부도 여기 둔다. 호출부마다 복사하면
    // 저장·좌표·표시 세 계층을 건드리는 절차가 UI로 흩어진다.
    public bool TryRelocateSlime(
        SlimeController target,
        ESlimeLocation destination,
        Vector3 fallbackPosition)
    {
        if (target == null || SlimeManager.Instance == null) return false;

        try
        {
            SlimeManager.Instance.MoveSlime(target.InstanceId, destination);
            Vector2 spawnPoint = SpawnManager.Instance != null
                ? SpawnManager.Instance.GetRandomSpawnPosition()
                : Vector2.zero;
            target.transform.position = new Vector3(
                spawnPoint.x,
                spawnPoint.y,
                target.transform.position.z);
            RefreshSlimePresentation(target);
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException ||
                                  e is ArgumentException)
        {
            Debug.LogWarning($"슬라임 위치를 옮길 수 없습니다: {e.Message}");
            target.transform.position = fallbackPosition;
            RefreshSlimePresentation(target);
            return false;
        }
    }

    // 표시 규칙은 이 클래스 안에서만 쓴다. 밖에서 부르면 배경·공간 상태와
    // 어긋난 시점에 적용될 수 있어 공개하지 않는다.
    private void RefreshSlimePresentation(SlimeController target)
    {
        if (target == null) return;

        bool isVisible = IsMainFieldActive
            ? target.Location == ESlimeLocation.MainField
            : target.Location == ESlimeLocation.DisplayRoom;
        target.SetLocationPresentationActive(isVisible);
    }

    private void SetCurrentSpace(EGameplaySpace space)
    {
        if (_currentSpace == space) return;

        _currentSpace = space;
        ApplyAllSlimeVisibility();
        RefreshBackgroundButton();
        SpaceChanged?.Invoke(_currentSpace);
    }

    private SlimeController FindFirstSkySlime()
    {
        if (SlimeSpawner.Instance == null) return null;

        foreach (SlimeController target in SlimeSpawner.Instance.GetActiveTargets())
        {
            if (target != null &&
                target.Grade >= UnlockGrades.BackgroundTheme)
            {
                return target;
            }
        }

        return null;
    }

    // 공간별 입력 정책을 한곳에서 정한다.
    // 평상시는 공간 기본값이며, 초기화·전환 연출 중에는 차단 우선순위를 쓴다.
    private void SetInteractionEnabled(bool isEnabled)
    {
        _upgradeUI.SetToggleInputEnabled(isEnabled && IsMainFieldActive);
        _clicker.PushMode(
            this,
            GetSpaceInputMode(isEnabled),
            isEnabled ? ClickerInputPriority.Space : ClickerInputPriority.Modal);
    }

    private ClickerInputMode GetSpaceInputMode(bool isEnabled)
    {
        if (!isEnabled) return ClickerInputMode.Blocked;
        if (IsMainFieldActive) return ClickerInputMode.Free;

        // 장식장에서는 기획서 §7.2대로 클릭 포인트와 드래그 합성을 막고 선택만 허용한다.
        return ClickerInputMode.SelectOnly();
    }

    // 팝업이나 연출이 끝난 뒤 현재 공간에 맞는 입력 상태로 되돌린다.
    // 평상시 공간 기본값은 갱신 순서와 무관하게 선택 모드·튜토리얼보다 낮다.
    // 배경 버튼을 열어도 되는지 판정하는 한 곳. 초기화 직후, 배경 해금 안내가
    // 끝날 때, 공간을 오갈 때가 모두 같은 규칙을 쓴다.
    //
    // 나뉘어 있을 때는 공간 전환만 인트로 완료를 보지 않는 등 조건이 서로
    // 달랐고, 하늘 인트로 쪽은 조건 없이 켜기만 했다. 규칙이 늘어날 때
    // 빠뜨리는 자리가 생긴다.
    //
    // 메뉴 안에서 실제로 보일지는 BackgroundThemeUI의 다른 축이 정한다. 여기서는
    // 버튼을 열어도 되는 상태인지만 답한다.
    public void RefreshBackgroundButton()
    {
        _backgroundThemeUI.SetButtonVisible(
            _isInitialized &&
            IsMainFieldActive &&
            SlimeManager.Instance != null &&
            SlimeManager.Instance.IsSkyUnlocked &&
            SlimeManager.Instance.SkyIntroCompleted);
    }

    public void RefreshInteraction()
    {
        SetInteractionEnabled(GameplayGate.IsActive);
    }
}
