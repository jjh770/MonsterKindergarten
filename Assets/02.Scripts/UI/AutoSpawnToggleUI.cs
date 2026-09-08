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
// 조정자를 두지 않고 SlimeManager를 직접 본다. 켜짐 여부와 해금 여부가 모두 그
// 한곳에서 나오고 이 버튼 말고는 아무도 관여하지 않아, 중간 계층이 전달만 하게 된다.
public sealed class AutoSpawnToggleUI : MonoBehaviour
{
    [Tooltip("해금 전에 끌 오브젝트입니다. 버튼 오브젝트 자신을 넣어도 됩니다.")]
    [SerializeField] private GameObject _root;
    [SerializeField] private Button _button;

    [Tooltip("상태에 따라 스프라이트를 갈아 끼울 이미지입니다.")]
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _onSprite;
    [SerializeField] private Sprite _offSprite;

    private void Awake()
    {
        if (_root == null || _button == null || _icon == null ||
            _onSprite == null || _offSprite == null)
        {
            Debug.LogError("자동 스폰 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _button.onClick.AddListener(OnButtonClicked);

        SlimeManager.OnDataInitialized += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;

        // 구독과 첫 갱신을 Start가 아니라 여기서 끝낸다. Root는 이 컴포넌트가 붙은
        // 오브젝트 자신일 수 있는데, 그때 아래 Refresh가 자기를 끄면 Unity는 Start를
        // 부르지 않는다. Start에 구독을 두면 영영 켜지지 못한다.
        //
        // 꺼진 오브젝트여도 static 이벤트는 그대로 도달하므로 해금 시점에 다시 켜진다.
        Refresh();
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        SlimeManager.OnDataInitialized -= Refresh;
        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
    }

    private void OnHighestGradeChanged(ESlimeGrade grade) => Refresh();

    private void OnButtonClicked()
    {
        if (SlimeManager.Instance == null) return;

        SlimeManager.Instance.SetAutoSpawnEnabled(
            !SlimeManager.Instance.IsAutoSpawnEnabled);
        Refresh();
    }

    private void Refresh()
    {
        SlimeManager manager = SlimeManager.Instance;
        bool isUnlocked = manager != null && manager.IsGachaUnlocked;

        _root.SetActive(isUnlocked);
        if (!isUnlocked) return;

        _icon.sprite = manager.IsAutoSpawnEnabled ? _onSprite : _offSprite;
    }
}
