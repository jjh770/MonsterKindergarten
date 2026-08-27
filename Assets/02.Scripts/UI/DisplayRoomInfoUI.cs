using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 장식장 슬라임 선택과 기획서 §8의 관찰 진입 UI를 담당한다.
// 카메라 연출은 StageTransitionPlayer에 위임하고 이 컴포넌트는 표시 상태만 소유한다.
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

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float _observationDuration = 0.3f;

    private Tween _fadeTween;
    private Sequence _observationSequence;
    private SlimeController _target;
    private Vector3 _takeOutStartPosition;
    private bool _isTakeOutPlaying;
    private bool _isObserving;

    public bool IsVisible => _target != null;
    public bool IsObserving => _isObserving;
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
        _observationInputRoot.SetActive(false);
        _observeButton.onClick.AddListener(EnterObservationMode);
        _closeButton.onClick.AddListener(Close);
        _takeOutButton.onClick.AddListener(OnTakeOutButtonClicked);
        _clicker.TargetClicked += OnTargetClicked;
        StageManager.Instance.SpaceChanged += OnSpaceChanged;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _observationSequence?.Kill();
        _observeButton?.onClick.RemoveListener(EnterObservationMode);
        _closeButton?.onClick.RemoveListener(Close);
        _takeOutButton?.onClick.RemoveListener(OnTakeOutButtonClicked);

        if (_clicker != null)
        {
            _clicker.TargetClicked -= OnTargetClicked;
            _clicker.ReleaseMode(this);
        }

        if (StageManager.Instance != null)
        {
            StageManager.Instance.SpaceChanged -= OnSpaceChanged;
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
                             StageManager.Instance != null;
        if (!hasReferences)
        {
            Debug.LogError("장식장 정보 UI의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnTargetClicked(SlimeController target)
    {
        StageManager stageManager = StageManager.Instance;
        if (target == null ||
            _isTakeOutPlaying ||
            IsVisible ||
            stageManager == null ||
            stageManager.IsMainStageActive ||
            stageManager.IsTransitioning ||
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

        StageManager.Instance.FocusDisplayRoomSlime(
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

        if (_observationSequence != null) return true;

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
        if (StageManager.Instance != null)
        {
            StageManager.Instance.RestoreDisplayRoomFocus(
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
        if (!IsVisible || _isTakeOutPlaying || _isObserving) return;

        _isObserving = true;
        _infoCanvasGroup.interactable = false;
        _infoCanvasGroup.blocksRaycasts = false;
        _observationInputRoot.SetActive(true);
        _observationInputRoot.transform.SetAsLastSibling();
        StageManager.Instance?.BeginDisplayRoomObservation();

        _fadeTween?.Kill();
        _fadeTween = null;
        _observationSequence?.Kill();
        _observationSequence = DOTween.Sequence();
        _observationSequence.Join(
            _infoCanvasGroup.DOFade(0f, _observationDuration));
        _observationSequence.OnComplete(() => _observationSequence = null);
        _hudVisibility.PushHide(this, EHudParts.All);
    }

    private void ExitObservationMode()
    {
        if (!_isObserving) return;

        _isObserving = false;
        _observationInputRoot.SetActive(false);
        _infoCanvasGroup.blocksRaycasts = true;
        StageManager.Instance?.EndDisplayRoomObservation();

        _observationSequence?.Kill();
        _observationSequence = DOTween.Sequence();
        _observationSequence.Join(
            _infoCanvasGroup.DOFade(1f, _observationDuration));
        _hudVisibility.Release(this);
        _observationSequence.OnComplete(() =>
        {
            _observationSequence = null;
            _infoCanvasGroup.interactable = true;
        });
    }

    private void OnTakeOutButtonClicked()
    {
        if (!IsVisible || _isTakeOutPlaying) return;

        // 기획서 §7.5 - 메인 필드가 가득 차면 꺼낼 수 없다.
        if (SpawnManager.Instance == null ||
            !SpawnManager.Instance.HasMainStageRoom())
        {
            _toast.Show("메인 필드가 가득 차서 꺼낼 수 없어요.");
            return;
        }

        SlimeController target = _target;
        _isTakeOutPlaying = true;
        _takeOutStartPosition = target.transform.position;
        _infoCanvasGroup.interactable = false;
        StageManager.Instance.PlayDisplayRoomTransfer(
            target,
            () => CompleteTakeOut(target));
    }

    private void CompleteTakeOut(SlimeController target)
    {
        _isTakeOutPlaying = false;

        StageManager stageManager = StageManager.Instance;
        if (target == null || stageManager == null)
        {
            TryClose();
            return;
        }

        if (stageManager.TryRelocateSlime(
                target,
                ESlimeLocation.MainStage,
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

    private void ResetObservationPresentation()
    {
        _isObserving = false;
        _observationSequence?.Kill();
        _observationSequence = null;
        _observationInputRoot.SetActive(false);
        _hudVisibility.Release(this, animated: false);
        _infoCanvasGroup.blocksRaycasts = true;
    }

}
