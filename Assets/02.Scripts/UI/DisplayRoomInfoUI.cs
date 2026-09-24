using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 장식장 슬라임 선택과 기획서 §8의 관찰 진입 UI를 담당한다.
// 카메라 연출은 GameplayTransitionPlayer에 위임하고 이 컴포넌트는 표시 상태만 소유한다.
//
// DisplayRoomUI와 합치지 않는다. GameExitManager가 소유자별로 뒤로가기 핸들러를
// 하나만 유지하므로, 같은 소유자가 장식장 나가기와 정보 UI 닫기를 함께 등록하면
// 나중 등록이 앞의 것을 덮어써 §26의 닫기 우선순위가 무너진다.
public sealed class DisplayRoomInfoUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Common")]
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private ToastMessageUI _toast;

    [Header("Info Panel")]
    [SerializeField] private GameObject _infoRoot;
    [SerializeField] private CanvasGroup _infoCanvasGroup;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private RectTransform _infoSummaryTarget;
    [SerializeField] private Button _observeButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _takeOutButton;

    [Header("Observation Mode")]
    [SerializeField] private GameObject _observationInputRoot;
    [SerializeField] private HudVisibility _hudVisibility;
    // 상점 서랍은 HudVisibility가 옮기는 두 루트에 들어 있지 않다. 자기 폭과
    // 세이프에어리어로 숨는 자리를 스스로 계산하므로 서랍에 맡겨야 한다.
    [SerializeField] private UpgradeUI _upgradeUI;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float _observationDuration = 0.3f;

    private Tween _fadeTween;
    private GameplayModeSession _observationSession;
    private SlimeController _target;
    private Vector3 _takeOutStartPosition;
    private bool _isTakeOutPlaying;
    private bool _isObserving;

    public bool IsVisible => _target != null;
    public bool IsObserving => _isObserving;

    // 대포가 물고 있는 동안이다. 이때 꺼내면 필드로 보낸 슬라임을 대포가 3초 뒤에
    // 장식장 좌표로 끌어다 놓고 쏜다. 저장과 화면이 갈라지므로 손을 떼고 기다린다.
    private bool IsTargetHeld => _target != null && _target.IsPresentationLocked;
    public RectTransform InfoSummaryTarget => _infoSummaryTarget;
    public RectTransform ObserveButtonTarget =>
        _observeButton != null ? _observeButton.transform as RectTransform : null;
    public RectTransform TakeOutButtonTarget =>
        _takeOutButton != null ? _takeOutButton.transform as RectTransform : null;
    public RectTransform CloseButtonTarget =>
        _closeButton != null ? _closeButton.transform as RectTransform : null;

    // 확대 연출이 시작되는 시점. 튜토리얼이 안내 말풍선을 숨기는 데 쓴다.
    public event Action<SlimeController> InfoOpening;
    // 확대가 끝나고 패널이 나타나는 시점.
    public event Action<SlimeController> InfoOpened;
    // 닫기 버튼과 뒤로가기를 가리지 않고 패널이 닫힌 시점.
    // 강제 정리(ForceClose)는 공간 전환이 별도로 처리하므로 발화하지 않는다.
    public event Action InfoClosed;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _infoCanvasGroup.interactable = false;
        _infoRoot.SetActive(false);
        _observationSession = new GameplayModeSession(
            _upgradeUI,
            _clicker,
            _gameExitManager,
            _hudVisibility,
            EHudParts.All,
            _observationInputRoot,
            _infoCanvasGroup,
            _observationDuration,
            activeAlpha: 0f,
            inactiveAlpha: 1f,
            manageRaycasts: false);
        _observationSession.ResetPresentation();
        _observeButton.onClick.AddListener(EnterObservationMode);
        _closeButton.onClick.AddListener(Close);
        _takeOutButton.onClick.AddListener(OnTakeOutButtonClicked);
        _clicker.TargetClicked += OnTargetClicked;
        GameplaySpaceManager.Instance.SpaceChanged += OnSpaceChanged;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _observationSession?.Dispose();
        _observeButton?.onClick.RemoveListener(EnterObservationMode);
        _closeButton?.onClick.RemoveListener(Close);
        _takeOutButton?.onClick.RemoveListener(OnTakeOutButtonClicked);

        if (_clicker != null)
        {
            _clicker.TargetClicked -= OnTargetClicked;
            _clicker.ReleaseMode(this);
        }

        if (GameplaySpaceManager.Instance != null)
        {
            GameplaySpaceManager.Instance.SpaceChanged -= OnSpaceChanged;
        }

        _gameExitManager?.UnregisterBackHandler(this);
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _gameExitManager != null &&
                             _clicker != null &&
                             _toast != null &&
                             _infoRoot != null &&
                             _infoCanvasGroup != null &&
                             _nameText != null &&
                             _numberText != null &&
                             _descriptionText != null &&
                             _infoSummaryTarget != null &&
                             _observeButton != null &&
                             _closeButton != null &&
                             _takeOutButton != null &&
                             _observationInputRoot != null &&
                             _hudVisibility != null &&
                             _upgradeUI != null &&
                             GameplaySpaceManager.Instance != null;
        if (!hasReferences)
        {
            Debug.LogError("장식장 정보 UI의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnTargetClicked(SlimeController target)
    {
        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        if (target == null ||
            _isTakeOutPlaying ||
            IsVisible ||
            spaceManager == null ||
            spaceManager.IsMainFieldActive ||
            spaceManager.IsTransitioning ||
            target.Location != ESlimeLocation.DisplayRoom)
        {
            return;
        }

        Open(target);
    }

    private void Open(SlimeController target)
    {
        _target = target;
        InfoOpening?.Invoke(target);
        ResetObservationPresentation();
        _infoRoot.SetActive(true);
        _infoCanvasGroup.alpha = 0f;
        _infoCanvasGroup.interactable = false;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        _gameExitManager.RegisterBackHandler(this, TryClose);

        SlimeSpecData specData = target.Slime?.SpecData;
        _nameText.text = specData?.Name ?? string.Empty;
        _numberText.text = $"No.{(int)target.Grade}";
        _descriptionText.text = specData?.Description ?? string.Empty;

        GameplaySpaceManager.Instance.FocusDisplayRoomSlime(
            target,
            () => ShowInfo(target));
    }

    private void ShowInfo(SlimeController target)
    {
        if (_target != target) return;

        _infoCanvasGroup.interactable = true;
        _infoCanvasGroup.blocksRaycasts = true;
        _fadeTween?.Kill();
        _fadeTween = _infoCanvasGroup
            .DOFade(1f, _fadeDuration)
            .OnComplete(() => _fadeTween = null);
        InfoOpened?.Invoke(target);
    }

    private void Close()
    {
        TryClose();
    }

    private bool TryClose()
    {
        if (!IsVisible) return false;

        if (_isObserving)
        {
            ExitObservationMode();
            return true;
        }

        if (_observationSession.IsTransitioning) return true;

        // 물려 있는 동안에는 닫지 않되 입력은 소비한다.
        if (IsTargetHeld) return true;

        // 꺼내기 연출 중에는 닫지 않되 입력은 소비한다.
        // false를 반환하면 GameExitManager가 이 핸들러를 목록에서 제거해
        // 연출이 끝난 뒤 뒤로가기로 정보 UI를 닫을 수 없게 된다.
        if (_isTakeOutPlaying) return true;

        _target = null;
        _gameExitManager.UnregisterBackHandler(this);
        _infoCanvasGroup.interactable = false;

        _fadeTween?.Kill();
        _fadeTween = _infoCanvasGroup
            .DOFade(0f, _fadeDuration)
            .OnComplete(() =>
            {
                _fadeTween = null;
                _infoRoot.SetActive(false);
            });
        // 카메라가 원래 자리로 돌아온 뒤에 입력을 돌려준다.
        // 즉시 해제하면 축소가 풀리는 동안 슬라임이 탭돼 패널이 다시 열린다.
        if (GameplaySpaceManager.Instance != null)
        {
            GameplaySpaceManager.Instance.RestoreDisplayRoomFocus(
                () => _clicker.ReleaseMode(this));
        }
        else
        {
            _clicker.ReleaseMode(this);
        }

        InfoClosed?.Invoke();
        return true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isObserving)
        {
            ExitObservationMode();
        }
    }

    private void EnterObservationMode()
    {
        if (!IsVisible || _isTakeOutPlaying || _isObserving || IsTargetHeld) return;

        _isObserving = true;
        _infoCanvasGroup.interactable = false;
        _infoCanvasGroup.blocksRaycasts = false;
        GameplaySpaceManager.Instance?.BeginDisplayRoomObservation();
        _fadeTween?.Kill();
        _fadeTween = null;
        _observationSession.Enter(
            ClickerInputMode.Blocked,
            ClickerInputPriority.Modal,
            TryClose,
            bringRootToFront: true);
    }

    private void ExitObservationMode()
    {
        if (!_isObserving) return;

        _isObserving = false;
        _infoCanvasGroup.blocksRaycasts = true;
        GameplaySpaceManager.Instance?.EndDisplayRoomObservation();
        _observationSession.Exit(
            deactivateRootImmediately: true,
            onCompleted: () => _infoCanvasGroup.interactable = true);
    }

    private void OnTakeOutButtonClicked()
    {
        if (!IsVisible || _isTakeOutPlaying || IsTargetHeld) return;

        // 기획서 §7.5 - 메인 필드가 가득 차면 꺼낼 수 없다.
        if (SpawnManager.Instance == null ||
            !SpawnManager.Instance.HasMainFieldRoom())
        {
            _toast.Show("메인 필드가 가득 차서 꺼낼 수 없어요.");
            return;
        }

        SlimeController target = _target;
        _isTakeOutPlaying = true;
        _takeOutStartPosition = target.transform.position;
        _infoCanvasGroup.interactable = false;
        GameplaySpaceManager.Instance.PlayDisplayRoomTransfer(
            target,
            () => CompleteTakeOut(target));
    }

    private void CompleteTakeOut(SlimeController target)
    {
        _isTakeOutPlaying = false;

        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        if (target == null || spaceManager == null)
        {
            TryClose();
            return;
        }

        if (spaceManager.TryRelocateSlime(
                target,
                ESlimeLocation.MainField,
                _takeOutStartPosition))
        {
            TryClose();
            return;
        }

        _infoCanvasGroup.interactable = true;
        _toast.Show("이 슬라임은 지금 꺼낼 수 없어요.");
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        if (space != EGameplaySpace.DisplayRoom)
        {
            ForceClose();
        }
    }

    // 공간이 바뀌면 대상 슬라임이 화면에서 사라지므로 연출 없이 즉시 정리한다.
    private void ForceClose()
    {
        // 닫기 연출이 공간 전환으로 취소돼도 이 UI의 입력 잠금은 해제한다.
        _clicker.ReleaseMode(this);
        if (!IsVisible) return;

        _isTakeOutPlaying = false;
        _target = null;
        _gameExitManager.UnregisterBackHandler(this);
        ResetObservationPresentation();
        _infoCanvasGroup.interactable = false;
        _fadeTween?.Kill();
        _fadeTween = null;
        _infoCanvasGroup.alpha = 0f;
        _infoRoot.SetActive(false);
    }


    // 대포가 물어 가면 버튼을 내리고, 놓아 주면 되돌린다.
    //
    // 신호를 받아 한 번만 바꾸지 않고 매 프레임 확인한다. 대포가 놓아 주는 경로가
    // 발사·중단·공간 이탈·배치 모드로 여럿이라, 그중 하나만 신호를 빠뜨려도 버튼이
    // 꺼진 채로 남고 정보창을 닫을 수도 없게 된다.
    private void Update()
    {
        if (!IsVisible) return;

        bool canUse = !IsTargetHeld && !_isTakeOutPlaying;
        if (_observeButton != null) _observeButton.interactable = canUse;
        if (_takeOutButton != null) _takeOutButton.interactable = canUse;
        if (_closeButton != null) _closeButton.interactable = canUse;
    }

    private void ResetObservationPresentation()
    {
        if (_observeButton != null) _observeButton.interactable = true;
        if (_takeOutButton != null) _takeOutButton.interactable = true;
        if (_closeButton != null) _closeButton.interactable = true;

        _isObserving = false;
        _observationSession.ResetPresentation();
        _infoCanvasGroup.blocksRaycasts = true;
    }

}
