using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 자연 스폰 상태와 저장만 담당한다. 공통 ON/OFF 표현은 BooleanToggleButtonView가 맡고,
// 언제 보일지는 GachaHudVisibility가 정한다.
public sealed class AutoSpawnToggleUI : MonoBehaviour
{
    [SerializeField] private BooleanToggleButtonView _view;
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _onSprite;
    [SerializeField] private Sprite _offSprite;
    [SerializeField] private TMP_Text _stateLabel;
    [SerializeField] private Color _onColor = new(0.45f, 0.85f, 0.35f);
    [SerializeField] private Color _offColor = new(0.6f, 0.6f, 0.6f);
    [SerializeField] private SlimeManager _slimeManager;

    public RectTransform ButtonTarget => _view != null ? _view.ButtonTarget : null;

    private void Awake()
    {
        if (_view == null)
        {
            _view = GetComponent<BooleanToggleButtonView>();
        }

        if (_view == null)
        {
            Debug.LogError("자동 스폰 버튼의 공통 표현이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        if (_button != null && _icon != null && _onSprite != null &&
            _offSprite != null && _stateLabel != null)
        {
            _view.Configure(_button, _icon, _onSprite, _offSprite, _stateLabel,
                _onColor, _offColor);
        }
        _view.Clicked += OnButtonClicked;
        if (_slimeManager == null)
        {
            Debug.LogError("자동 스폰 버튼의 SlimeManager 참조가 없습니다.", this);
            enabled = false;
            return;
        }

        _slimeManager.DataInitialized += Refresh;
        _slimeManager.AutoSpawnChanged += OnAutoSpawnChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.Clicked -= OnButtonClicked;
        }

        if (_slimeManager != null)
        {
            _slimeManager.DataInitialized -= Refresh;
            _slimeManager.AutoSpawnChanged -= OnAutoSpawnChanged;
        }
    }

    private void OnButtonClicked()
    {
        if (_slimeManager == null) return;

        bool isEnabled = !_slimeManager.IsAutoSpawnEnabled;
        _slimeManager.SetAutoSpawnEnabled(isEnabled);
    }

    private void Refresh()
    {
        if (_slimeManager == null) return;

        _view.SetState(_slimeManager.IsAutoSpawnEnabled);
    }

    private void OnAutoSpawnChanged(bool isEnabled)
    {
        _view.SetState(isEnabled);
    }
}
