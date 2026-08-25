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
    [SerializeField] private DisplayRoomUI _displayRoomUI;

    [Header("Info Panel")]
    [SerializeField] private GameObject _infoRoot;
    [SerializeField] private CanvasGroup _infoCanvasGroup;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Button _observeButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _takeOutButton;

    [Header("Observation Mode")]
    [SerializeField] private GameObject _observationInputRoot;
    [SerializeField] private RectTransform _topUiRoot;
    [SerializeField] private RectTransform _bottomUiRoot;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float _observationDuration = 0.3f;

    private Tween _fadeTween;
    private Sequence _observationSequence;
    private SlimeController _target;
    private Vector2 _topUiStartPosition;
    private Vector2 _bottomUiStartPosition;
    private Vector3 _takeOutStartPosition;
    private bool _isTakeOutPlaying;
    private bool _isObserving;

    public bool IsVisible => _target != null;
    public bool IsObserving => _isObserving;
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
        _topUiStartPosition = _topUiRoot.anchoredPosition;
        _bottomUiStartPosition = _bottomUiRoot.anchoredPosition;
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
                             _displayRoomUI != null &&
                             _infoRoot != null &&
                             _infoCanvasGroup != null &&
                             _nameText != null &&
                             _numberText != null &&
                             _descriptionText != null &&
                             _observeButton != null &&
                             _closeButton != null &&
                             _takeOutButton != null &&
                             _observationInputRoot != null &&
                             _topUiRoot != null &&
                             _bottomUiRoot != null &&
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
        _clicker.SetInputMode(false, false);
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
        StageManager.Instance?.RestoreDisplayRoomFocus(
            () => StageManager.Instance?.RefreshInteraction());
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
        _observationSequence.Join(
            _topUiRoot.DOAnchorPos(
                _topUiStartPosition + Vector2.up * _topUiRoot.rect.height,
                _observationDuration));
        _observationSequence.Join(
            _bottomUiRoot.DOAnchorPos(
                _bottomUiStartPosition + Vector2.down * _bottomUiRoot.rect.height,
                _observationDuration));
        _observationSequence.OnComplete(() => _observationSequence = null);
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
        _observationSequence.Join(
            _topUiRoot.DOAnchorPos(_topUiStartPosition, _observationDuration));
        _observationSequence.Join(
            _bottomUiRoot.DOAnchorPos(_bottomUiStartPosition, _observationDuration));
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
            _displayRoomUI.ShowWarning("메인 필드가 가득 차서 꺼낼 수 없어요.");
            return;
        }

        SlimeController target = _target;
        _isTakeOutPlaying = true;
        _takeOutStartPosition = target.transform.position;
        _infoCanvasGroup.interactable = false;
        _clicker.SetInputMode(false, false);
        StageManager.Instance.PlayDisplayRoomTransfer(
            target,
            () => CompleteTakeOut(target));
    }

    private void CompleteTakeOut(SlimeController target)
    {
        _isTakeOutPlaying = false;

        if (target == null || SlimeManager.Instance == null)
        {
            TryClose();
            return;
        }

        try
        {
            SlimeManager.Instance.MoveSlime(
                target.InstanceId,
                ESlimeLocation.MainStage);
            Vector2 destination = SpawnManager.Instance != null
                ? SpawnManager.Instance.GetRandomSpawnPosition()
                : Vector2.zero;
            target.transform.position = new Vector3(
                destination.x,
                destination.y,
                target.transform.position.z);
            StageManager.Instance?.RefreshSlimePresentation(target);
            TryClose();
        }
        catch (Exception e) when (e is InvalidOperationException ||
                                  e is ArgumentException)
        {
            Debug.LogWarning($"슬라임을 장식장에서 꺼낼 수 없습니다: {e.Message}");
            target.transform.position = _takeOutStartPosition;
            StageManager.Instance?.RefreshSlimePresentation(target);
            _clicker.SetInputMode(false, false);
            _infoCanvasGroup.interactable = true;
            _displayRoomUI.ShowWarning("이 슬라임은 지금 꺼낼 수 없어요.");
        }
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
        _topUiRoot.anchoredPosition = _topUiStartPosition;
        _bottomUiRoot.anchoredPosition = _bottomUiStartPosition;
        _infoCanvasGroup.blocksRaycasts = true;
    }

}
