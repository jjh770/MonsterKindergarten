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

    private void Awake()
    {
        if (_button == null || _icon == null ||
            _onSprite == null || _offSprite == null)
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

        SlimeManager.Instance.SetAutoSpawnEnabled(
            !SlimeManager.Instance.IsAutoSpawnEnabled);
        Refresh();
    }

    private void Refresh()
    {
        if (SlimeManager.Instance == null) return;

        _icon.sprite = SlimeManager.Instance.IsAutoSpawnEnabled
            ? _onSprite
            : _offSprite;
    }
}
