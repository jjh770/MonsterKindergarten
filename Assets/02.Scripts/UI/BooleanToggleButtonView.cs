using System;
using TMPro;
using UnityEngine;

// 도메인과 무관한 ON/OFF 버튼 표현만 담당한다.
// 실제 상태 변경과 저장은 이 뷰를 사용하는 기능 컴포넌트가 결정한다.
public sealed class BooleanToggleButtonView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button _button;
    [SerializeField] private UnityEngine.UI.Image _icon;
    [SerializeField] private Sprite _onSprite;
    [SerializeField] private Sprite _offSprite;
    [SerializeField] private TMP_Text _stateLabel;
    [SerializeField] private Color _onColor = Color.white;
    [SerializeField] private Color _offColor = new(1f, 1f, 1f, 0.55f);
    private bool _isBound;

    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;

    public event Action Clicked;

    public void Configure(
        UnityEngine.UI.Button button,
        UnityEngine.UI.Image icon,
        Sprite onSprite,
        Sprite offSprite,
        TMP_Text stateLabel,
        Color onColor,
        Color offColor)
    {
        _button = button;
        _icon = icon;
        _onSprite = onSprite;
        _offSprite = offSprite;
        _stateLabel = stateLabel;
        _onColor = onColor;
        _offColor = offColor;

        Bind();
    }

    private void Awake()
    {
        Bind();
    }

    private void OnDestroy()
    {
        if (_button != null && _isBound)
        {
            _button.onClick.RemoveListener(NotifyClicked);
        }
    }

    private void Bind()
    {
        if (!isActiveAndEnabled || _button == null) return;

        _button.onClick.RemoveListener(NotifyClicked);
        _button.onClick.AddListener(NotifyClicked);
        _isBound = true;
    }

    public void SetState(bool isOn)
    {
        if (_icon != null)
        {
            _icon.sprite = isOn ? _onSprite : _offSprite;
        }

        if (_stateLabel != null)
        {
            _stateLabel.text = isOn ? "ON" : "OFF";
            _stateLabel.color = isOn ? _onColor : _offColor;
        }
    }

    private void NotifyClicked()
    {
        Clicked?.Invoke();
    }
}
