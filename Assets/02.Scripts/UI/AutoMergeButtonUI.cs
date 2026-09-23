using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class AutoMergeButtonUI : MonoBehaviour
{
    [SerializeField] private RadialProgressView _progressView;
    [SerializeField] private Button _button;

    [Tooltip("합성할 쌍이 없을 때 띄우는 안내입니다.")]
    [SerializeField] private ToastMessageUI _toast;

    private const string NoPairMessage = "합성할 수 있는 슬라임이 없어요.";

    private bool? _lastInteractable;

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

        if (_button == null || _progressView == null)
        {
            Debug.LogError("자동 합성 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled) return;

        _button.onClick.AddListener(OnButtonClicked);
        SlimeManager.OnDataInitialized += Refresh;
        SlimeManager.OnNormalCollectionCountChanged += OnCollectionCountChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
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

    // 매 프레임 도니 상태가 바뀔 때만 손댄다.
    private void Refresh()
    {
        AutoMergeManager manager = AutoMergeManager.Instance;
        if (manager == null) return;

        bool isInteractable = manager.IsReady && manager.IsAvailable();
        if (isInteractable == _lastInteractable) return;

        _lastInteractable = isInteractable;
        _button.interactable = isInteractable;
    }
}
