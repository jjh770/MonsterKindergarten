using UnityEngine;

// 장식장 놀이터 오브젝트를 한 덩어리로 켜고 끈다.
//
// 장식장과 메인 필드는 연출만 다를 뿐 같은 물리 공간이다. 카메라가 옆으로 빠졌다가
// 같은 자리로 돌아오고, 슬라임은 소속에 따라 보이거나 시뮬레이션이 꺼질 뿐이다.
// 그래서 놀이터 오브젝트를 켜 둔 채 두면 메인 필드 한복판에 보이지 않는 장애물이
// 서서, 필드 슬라임이 아무것도 없는 자리에서 튕긴다.
//
// 이 컴포넌트는 항상 켜져 있는 오브젝트에 두고, 꺼야 할 것들은 _content 아래에 둔다.
// 자기 자신을 끄면 다음 공간 전환 이벤트를 듣지 못해 영영 다시 켜지지 않는다.
// FadeCurtainUI가 같은 이유로 같은 모양을 하고 있다.
public class DisplayRoomPlaygroundRoot : MonoBehaviour
{
    [Tooltip("장식장에서만 켜질 오브젝트들의 부모입니다. 이 컴포넌트가 붙은 오브젝트와 달라야 합니다.")]
    [SerializeField] private GameObject _content;

    private void Awake()
    {
        if (_content == null || _content == gameObject)
        {
            Debug.LogError(
                "장식장 놀이터의 대상이 비어 있거나 자기 자신입니다. " +
                "자기 자신을 끄면 공간 전환을 다시 들을 수 없습니다.",
                this);
            enabled = false;
            return;
        }

        // 장식장에 있는지는 저장하지 않는 런타임 상태라 앱은 항상 메인 필드에서
        // 시작한다(기획서 §7.2). 구독이 붙기 전에도 꺼진 상태가 맞다.
        _content.SetActive(false);
    }

    private void Start()
    {
        if (!enabled) return;

        if (GameplaySpaceManager.Instance == null) return;

        GameplaySpaceManager.Instance.SpaceChanged += OnSpaceChanged;

        // 이미 장식장에 들어간 뒤에 붙었을 수 있다. 이벤트를 다시 기다릴 수 없으므로
        // 지금 상태를 한 번 반영한다.
        OnSpaceChanged(GameplaySpaceManager.Instance.CurrentSpace);
    }

    private void OnDestroy()
    {
        // 종료 순서는 보장되지 않아 매니저가 먼저 사라질 수 있다.
        if (GameplaySpaceManager.Instance == null) return;

        GameplaySpaceManager.Instance.SpaceChanged -= OnSpaceChanged;
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        if (_content == null) return;

        _content.SetActive(space == EGameplaySpace.DisplayRoom);
    }
}
