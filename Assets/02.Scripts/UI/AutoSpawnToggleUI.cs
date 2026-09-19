using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 자연 스폰을 켜고 끄는 버튼 하나만 담당한다.
//
// 그림은 지금 상태를 보여준다. 갈 곳을 가리키는 SpaceToggleButtonUI와 반대인데,
// 기획서가 `자동 스폰 [ON / OFF]`로 상태 표기를 정해 두었기 때문이다.
//
// 오브젝트 둘을 껐다 켜지 않고 Image 하나의 스프라이트를 바꾼다. 버튼의 Target
// Graphic이 상태마다 달라지지 않고, 두 그림의 자리와 크기가 어긋날 일도 없다.
//
// 언제 보일지는 GachaHudVisibility가 정한다. 여기서는 그림과 클릭만 다룬다.
public sealed class AutoSpawnToggleUI : MonoBehaviour
{
    [SerializeField] private Button _button;

    [Tooltip("상태에 따라 스프라이트를 갈아 끼울 이미지입니다.")]
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _onSprite;
    [SerializeField] private Sprite _offSprite;

    // 그림만으로는 켜짐과 꺼짐을 가리기 어렵다. 글자와 색을 함께 바꿔 색만으로
    // 구분하지 않게 한다.
    [Tooltip("ON/OFF를 적을 글자입니다.")]
    [SerializeField] private TMP_Text _stateLabel;
    [SerializeField] private Color _onColor = new Color(0.45f, 0.85f, 0.35f);
    [SerializeField] private Color _offColor = new Color(0.6f, 0.6f, 0.6f);

    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;
    public event Action<bool> StateChanged;

    private void Awake()
    {
        if (_button == null || _icon == null ||
            _onSprite == null || _offSprite == null || _stateLabel == null)
        {
            Debug.LogError("자동 스폰 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _button.onClick.AddListener(OnButtonClicked);
        SlimeManager.OnDataInitialized += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        SlimeManager.OnDataInitialized -= Refresh;
    }

    private void OnButtonClicked()
    {
        if (SlimeManager.Instance == null) return;

        bool isEnabled = !SlimeManager.Instance.IsAutoSpawnEnabled;
        SlimeManager.Instance.SetAutoSpawnEnabled(isEnabled);
        Refresh();
        StateChanged?.Invoke(isEnabled);
    }

    private void Refresh()
    {
        if (SlimeManager.Instance == null) return;

        bool isEnabled = SlimeManager.Instance.IsAutoSpawnEnabled;
        _icon.sprite = isEnabled ? _onSprite : _offSprite;
        _stateLabel.text = isEnabled ? "ON" : "OFF";
        _stateLabel.color = isEnabled ? _onColor : _offColor;
    }
}
