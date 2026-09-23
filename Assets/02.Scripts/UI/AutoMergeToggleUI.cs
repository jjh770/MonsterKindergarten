using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class AutoMergeToggleUI : MonoBehaviour
{
    [SerializeField] private BooleanToggleButtonView _view;
    [SerializeField] private RadialProgressView _progressView;
    [SerializeField] private GameObject _contentRoot;
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _onSprite;
    [SerializeField] private Sprite _offSprite;

    [Tooltip("합성할 쌍이 없을 때 띄우는 안내입니다.")]
    [SerializeField] private ToastMessageUI _toast;

    private const string NoPairMessage = "합성할 수 있는 슬라임이 없어요.";

    private bool _wasReady;

    public RectTransform ButtonTarget => _view != null ? _view.ButtonTarget : null;
    // 합성이 실제로 일어났을 때만 알린다. 튜토리얼과 안내가 쓴다.
    public event Action Merged;

    private void Awake()
    {
        if (_view == null)
        {
            _view = _contentRoot != null
                ? _contentRoot.GetComponent<BooleanToggleButtonView>()
                : GetComponentInChildren<BooleanToggleButtonView>(true);
        }

        if (_progressView == null)
        {
            _progressView = GetComponent<RadialProgressView>();
        }

        if (_view == null || _progressView == null)
        {
            Debug.LogError("자동 합성 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        // 켜고 끄는 버튼이 아니므로 ON/OFF를 쓸 자리가 없다. 라벨 없이 묶는다.
        if (_button != null && _icon != null && _onSprite != null &&
            _offSprite != null)
        {
            _view.Configure(_button, _icon, _onSprite, _offSprite);
        }
    }

    private void Start()
    {
        if (!enabled) return;

        _view.Clicked += OnButtonClicked;
        SlimeManager.OnDataInitialized += Refresh;
        SlimeManager.OnNormalCollectionCountChanged += OnCollectionCountChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.Clicked -= OnButtonClicked;
        }

        SlimeManager.OnDataInitialized -= Refresh;
        SlimeManager.OnNormalCollectionCountChanged -= OnCollectionCountChanged;
    }

    private void Update()
    {
        AutoMergeManager manager = AutoMergeManager.Instance;
        if (manager == null) return;

        _progressView.SetProgress(manager.Progress01);
        Refresh();
    }

    private void OnButtonClicked()
    {
        AutoMergeManager manager = AutoMergeManager.Instance;
        if (manager == null) return;

        AutoMergeManager.EMergeFailure failure = manager.TryMerge();
        if (failure == AutoMergeManager.EMergeFailure.None)
        {
            Merged?.Invoke();
            return;
        }

        // 쿨타임과 잠긴 상황은 게이지와 버튼 상태가 이미 보여 주므로 문구까지 띄우지
        // 않는다. 누를 수 있는데 아무 일도 없었을 때만 이유를 알려 준다.
        if (failure == AutoMergeManager.EMergeFailure.NoPair)
        {
            _toast?.Show(NoPairMessage);
        }
    }

    private void OnCollectionCountChanged(int count)
    {
        Refresh();
    }

    // 매 프레임 도니 상태가 바뀔 때만 손댄다.
    private void Refresh()
    {
        AutoMergeManager manager = AutoMergeManager.Instance;
        if (manager == null) return;

        // 켜고 끄는 버튼이 아니므로 상태는 "지금 누를 수 있는가"를 뜻한다.
        bool isReady = manager.IsReady && manager.IsAvailable();
        if (isReady == _wasReady) return;

        _wasReady = isReady;
        _view.SetState(isReady);
    }
}
