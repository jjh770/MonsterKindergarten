using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장식장 슬라임 선택과 기획서 §8의 관찰 진입 UI를 담당한다.
// 카메라 연출은 StageTransitionPlayer에 위임하고 이 컴포넌트는 표시 상태만 소유한다.
//
// DisplayRoomUI와 합치지 않는다. GameExitManager가 소유자별로 뒤로가기 핸들러를
// 하나만 유지하므로, 같은 소유자가 장식장 나가기와 정보 UI 닫기를 함께 등록하면
// 나중 등록이 앞의 것을 덮어써 §26의 닫기 우선순위가 무너진다.
public sealed class DisplayRoomInfoUI : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private DisplayRoomUI _displayRoomUI;

    [Header("Info Panel")]
    [SerializeField] private GameObject _infoRoot;
    [SerializeField] private CanvasGroup _infoCanvasGroup;
    [SerializeField] private RectTransform _infoPanel;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _takeOutButton;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float _panelMargin = 90f;

    private Tween _fadeTween;
    private SlimeController _target;
    private Vector3 _takeOutStartPosition;
    private bool _isTakeOutPlaying;

    public bool IsVisible => _target != null;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _infoCanvasGroup.interactable = false;
        _infoRoot.SetActive(false);
        _closeButton.onClick.AddListener(Close);
        _takeOutButton.onClick.AddListener(OnTakeOutButtonClicked);
        _clicker.TargetClicked += OnTargetClicked;
        StageManager.Instance.SpaceChanged += OnSpaceChanged;

        RefreshLayout();
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
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
        bool hasReferences = _canvas != null &&
                             _gameExitManager != null &&
                             _clicker != null &&
                             _displayRoomUI != null &&
                             _infoRoot != null &&
                             _infoCanvasGroup != null &&
                             _infoPanel != null &&
                             _nameText != null &&
                             _numberText != null &&
                             _closeButton != null &&
                             _takeOutButton != null &&
                             StageManager.Instance != null;
        if (!hasReferences)
        {
            Debug.LogError("장식장 정보 UI의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_canvas != null && _infoPanel != null)
        {
            RefreshLayout();
        }
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
        _infoRoot.SetActive(true);
        _infoCanvasGroup.alpha = 0f;
        _infoCanvasGroup.interactable = false;
        _clicker.SetInputMode(false, false);
        _gameExitManager.RegisterBackHandler(this, TryClose);

        _nameText.text = target.Slime != null
            ? target.Slime.SpecData.Name
            : string.Empty;
        _numberText.text = $"No.{(int)target.Grade}";
        RefreshLayout();

        StageManager.Instance.FocusDisplayRoomSlime(
            target,
            () => ShowInfo(target));
    }

    private void ShowInfo(SlimeController target)
    {
        if (_target != target) return;

        _infoCanvasGroup.interactable = true;
        _fadeTween?.Kill();
        _fadeTween = _infoCanvasGroup
            .DOFade(1f, _fadeDuration)
            .OnComplete(() => _fadeTween = null);
    }

    private void Close()
    {
        TryClose();
    }

    private bool TryClose()
    {
        if (!IsVisible) return false;

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
        return true;
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
        _infoCanvasGroup.interactable = false;
        _fadeTween?.Kill();
        _fadeTween = null;
        _infoCanvasGroup.alpha = 0f;
        _infoRoot.SetActive(false);
    }

    private void RefreshLayout()
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null) return;

        SafeAreaInsets insets = SafeAreaUtility.GetInsets(canvasRect);
        Vector2 panelPosition = _infoPanel.anchoredPosition;
        panelPosition.y = insets.Bottom + _panelMargin;
        _infoPanel.anchoredPosition = panelPosition;
    }
}
