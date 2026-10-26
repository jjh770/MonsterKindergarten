using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class AutoMergeButtonUI : MonoBehaviour
{
    [SerializeField] private RadialProgressView _progressView;
    [SerializeField] private Button _button;

    [Tooltip("합성할 쌍이 없을 때 띄우는 안내입니다.")]
    [SerializeField] private ToastMessageUI _toast;
    [SerializeField] private AutoMergeManager _autoMergeManager;
    [SerializeField] private SlimeManager _slimeManager;

    [Header("Ready")]
    [Tooltip("쿨다운이 끝나 다시 누를 수 있게 될 때 버튼이 출렁이는 크기입니다.")]
    [SerializeField, Min(0f)] private float _readyPunch = 0.18f;

    private static string NoPairMessage => UiMessages.NoMergePair;

    private bool? _lastInteractable;
    private bool? _wasReady;
    private Tween _readyTween;
    private ButtonPressFeedback _pressFeedback;

    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;
    // 누를 때마다 결과와 함께 알린다. 도감 10종 안내가 눌렀다는 사실만 보고
    // 끝내야 하므로, 합성에 실패한 누름도 빠뜨리지 않는다.
    public event Action<AutoMergeManager.EMergeFailure> Pressed;

    private void Awake()
    {
        if (_progressView == null)
        {
            _progressView = GetComponent<RadialProgressView>();
        }

        if (_button == null ||
            _progressView == null ||
            _autoMergeManager == null ||
            _slimeManager == null)
        {
            Debug.LogError("자동 합성 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _pressFeedback = _button.GetComponent<ButtonPressFeedback>();
    }

    private void Start()
    {
        if (!enabled) return;

        _button.onClick.AddListener(OnButtonClicked);
        _slimeManager.DataInitialized += Refresh;
        _slimeManager.NormalCollectionCountChanged += OnCollectionCountChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        if (_slimeManager != null)
        {
            _slimeManager.DataInitialized -= Refresh;
            _slimeManager.NormalCollectionCountChanged -= OnCollectionCountChanged;
        }

        _readyTween?.Kill(complete: true);
    }

    private void Update()
    {
        _progressView.SetProgress(_autoMergeManager.Progress01);
        CheckReady();
        Refresh();
    }

    private void OnButtonClicked()
    {
        AutoMergeManager.EMergeFailure failure = _autoMergeManager.TryMerge();
        Pressed?.Invoke(failure);

        // 쿨타임과 잠긴 상황은 게이지와 버튼 상태가 이미 보여 주므로 문구까지 띄우지
        // 않는다. 누를 수 있는데 아무 일도 없었을 때만 이유를 알려 준다.
        if (failure == AutoMergeManager.EMergeFailure.NoPair)
        {
            _toast?.Show(NoPairMessage);
        }
    }

    private void OnCollectionCountChanged(int _)
    {
        Refresh();
    }

    // 쿨다운이 끝나 다시 누를 수 있게 된 순간 버튼이 통통 튀고 짧은 알림음이 난다. 튜토리얼이나
    // 공간 전환으로 잠겼다 풀리는 것은 쿨다운과 상관없으므로, 준비 상태가 바뀐 때만 본다.
    // 처음 읽는 값은 기준으로만 쓴다. 게임에 들어오자마자 소리가 나지 않게 한다.
    private void CheckReady()
    {
        bool isReady = _autoMergeManager.IsReady;
        bool becameReady = _wasReady == false && isReady;
        _wasReady = isReady;
        if (!becameReady || !_autoMergeManager.IsAvailable() || !_button.gameObject.activeInHierarchy) return;

        // 버튼 크기는 누름 연출과 함께 ButtonPressFeedback 한 곳에서만 바꾼다. 여기서 따로 흔들면
        // 출렁이는 도중에 누른 크기가 기준이 되어 버튼이 점점 커진다.
        if (_pressFeedback != null)
        {
            _pressFeedback.Punch(_readyPunch, .35f);
        }
        else
        {
            _readyTween?.Kill(complete: true);
            _readyTween = _button.transform.DOPunchScale(Vector3.one * _readyPunch, .35f, 6, .5f)
                .OnKill(() => _readyTween = null);
        }
        AudioManager.Instance?.PlaySFXRandomPitch(EAudioSfx.TicketCollect, 1.15f, 1.25f, .6f);
    }

    // 매 프레임 도니 상태가 바뀔 때만 손댄다.
    private void Refresh()
    {
        bool isInteractable = _autoMergeManager.IsReady &&
                              _autoMergeManager.IsAvailable();
        if (isInteractable == _lastInteractable) return;

        _lastInteractable = isInteractable;
        _button.interactable = isInteractable;
    }
}
