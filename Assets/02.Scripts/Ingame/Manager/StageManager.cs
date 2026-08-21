using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _stageButtonTemplate;
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
    [SerializeField] private Color _transitionColor = new(0.75f, 0.9f, 1f, 1f);

    private EGameStage _currentStage = EGameStage.Ground;
    private Vector3 _cameraBasePosition;
    private Button _stageButton;
    private TextMeshProUGUI _stageButtonArrow;
    private Image _transitionOverlay;
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
        CreateTransitionOverlay();
        CreateStageButton();

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

        _stageButton?.onClick.RemoveListener(OnStageButtonClicked);
        _transitionSequence?.Kill();
        _chargeSequence?.Kill();
        _tutorialPresentation?.Dispose();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_stageButton != null)
        {
            RefreshStageButtonSafeArea();
        }
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
                             _stageButtonTemplate != null &&
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
        ApplyEnvironment(_currentStage, 0f);
        ApplyAllSlimeVisibility();
        SetStageButtonVisible(false, false);
        SetInteractionEnabled(false);

        await WaitForGameplayActiveAsync(token);

        SetInteractionEnabled(true);

        if (!SlimeManager.Instance.IsSkyUnlocked)
        {
            SetStageButtonVisible(false, false);
        }
        else if (SlimeManager.Instance.SkyIntroCompleted)
        {
            SetStageButtonVisible(true, false);
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
                SetStageButtonVisible(true, false);
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
        SetStageButtonVisible(true, true);

        if (_tutorialPresentation == null || _stageButton == null)
        {
            CompleteStageButtonTutorial();
            return;
        }

        _isWaitingForStageButtonTutorial = true;
        _tutorialPresentation.Spotlight.ShowUiTarget(
            _tutorialContent.StageButtonMessage,
            _stageButton.transform as RectTransform,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void CompleteFirstSkyIntroWithoutTarget()
    {
        SlimeManager.Instance?.UpdateStageProgress(
            EGameStage.Ground,
            skyIntroCompleted: true);
        SetStageButtonVisible(true, true);
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
        _stageButton.interactable = false;
        _transitionOverlay.transform.SetAsLastSibling();
        _transitionOverlay.raycastTarget = true;
        SetOverlayAlpha(0f);

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
            _transitionOverlay.DOFade(1f, halfDuration));

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
            _transitionOverlay.DOFade(0f, halfDuration));

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
            _transitionOverlay.raycastTarget = false;
            _isTransitioning = false;
            ApplyAllSlimeVisibility();

            if (saveStage && SlimeManager.Instance != null)
            {
                SlimeManager.Instance.UpdateStageProgress(
                    _currentStage,
                    SlimeManager.Instance.SkyIntroCompleted);
            }

            UpdateStageButtonVisual();
            _stageButton.interactable = true;
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

    private void CreateTransitionOverlay()
    {
        GameObject overlayObject = new GameObject(
            "StageTransitionOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        overlayObject.layer = _canvas.gameObject.layer;
        RectTransform overlayRect = (RectTransform)overlayObject.transform;
        overlayRect.SetParent(_canvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        _transitionOverlay = overlayObject.GetComponent<Image>();
        _transitionOverlay.color = _transitionColor;
        _transitionOverlay.raycastTarget = false;
        SetOverlayAlpha(0f);
    }

    private void SetOverlayAlpha(float alpha)
    {
        Color color = _transitionOverlay.color;
        color.a = alpha;
        _transitionOverlay.color = color;
    }

    private void CreateStageButton()
    {
        _stageButton = Instantiate(_stageButtonTemplate, _canvas.transform);
        _stageButton.name = "StageMoveButton";
        _stageButton.onClick.AddListener(OnStageButtonClicked);

        RectTransform buttonRect = (RectTransform)_stageButton.transform;
        buttonRect.localRotation = Quaternion.identity;
        buttonRect.localScale = Vector3.zero;
        buttonRect.anchorMin = Vector2.one;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.pivot = Vector2.one;
        buttonRect.sizeDelta = new Vector2(112f, 112f);

        Image buttonImage = _stageButton.targetGraphic as Image;
        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.18f, 0.42f, 0.65f, 0.95f);
        }

        GameObject arrowObject = new GameObject(
            "Arrow",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        arrowObject.layer = _canvas.gameObject.layer;
        RectTransform arrowRect = (RectTransform)arrowObject.transform;
        arrowRect.SetParent(buttonRect, false);
        arrowRect.anchorMin = Vector2.zero;
        arrowRect.anchorMax = Vector2.one;
        arrowRect.offsetMin = Vector2.zero;
        arrowRect.offsetMax = Vector2.zero;

        _stageButtonArrow = arrowObject.GetComponent<TextMeshProUGUI>();
        _stageButtonArrow.alignment = TextAlignmentOptions.Center;
        _stageButtonArrow.fontSize = 56f;
        _stageButtonArrow.color = Color.white;
        _stageButtonArrow.raycastTarget = false;

        RefreshStageButtonSafeArea();
        UpdateStageButtonVisual();
        _stageButton.gameObject.SetActive(false);
    }

    private void SetStageButtonVisible(bool isVisible, bool animated)
    {
        if (_stageButton == null) return;

        _stageButton.gameObject.SetActive(isVisible);
        _stageButton.transform.DOKill();

        if (!isVisible)
        {
            _stageButton.transform.localScale = Vector3.zero;
            return;
        }

        RefreshStageButtonSafeArea();
        UpdateStageButtonVisual();

        if (!animated)
        {
            _stageButton.transform.localScale = Vector3.one;
            return;
        }

        _stageButton.transform.localScale = Vector3.zero;
        _stageButton.transform.DOScale(Vector3.one, 0.35f)
            .SetEase(Ease.OutBack);
    }

    private void UpdateStageButtonVisual()
    {
        if (_stageButtonArrow == null) return;

        _stageButtonArrow.text = _currentStage == EGameStage.Ground
            ? "↑"
            : "↓";
    }

    private void RefreshStageButtonSafeArea()
    {
        if (_stageButton == null || _canvas == null) return;

        RectTransform canvasRect = _canvas.transform as RectTransform;
        RectTransform buttonRect = _stageButton.transform as RectTransform;
        if (canvasRect == null || buttonRect == null) return;

        float rightInset = GetCanvasInset(
            Screen.width - Screen.safeArea.xMax,
            Screen.width,
            canvasRect.rect.width);
        float topInset = GetCanvasInset(
            Screen.height - Screen.safeArea.yMax,
            Screen.height,
            canvasRect.rect.height);
        buttonRect.anchoredPosition = new Vector2(
            -rightInset - 28f,
            -topInset - 28f);
    }

    private static float GetCanvasInset(
        float pixelInset,
        int screenSize,
        float canvasSize)
    {
        if (screenSize <= 0 || canvasSize <= 0f) return 0f;

        return Mathf.Max(0f, pixelInset / screenSize * canvasSize);
    }
}
