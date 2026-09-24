using UnityEngine;

// 공간마다 다른 오브젝트를 켜고 끈다.
//
// 장식장과 메인 필드는 연출만 다를 뿐 같은 물리 공간이다. 카메라가 옆으로 빠졌다가
// 같은 자리로 돌아오고, 슬라임은 소속에 따라 보이거나 시뮬레이션이 꺼질 뿐이다.
// 그래서 한쪽 공간에만 있어야 할 것을 켜 둔 채로 두면, 다른 공간에서 보이지 않는
// 장애물이 되어 슬라임이 아무것도 없는 자리에서 튕긴다.
//
// 지금 이 스위치가 맡는 것은 두 가지다.
//  - 장식장 쪽: 놀이터 오브젝트와 넓은 벽
//  - 메인 필드 쪽: 원래의 좁은 벽
//
// 오브젝트를 옮겨 붙이지 않고 목록으로 들고 있는다. 벽은 원래 씬 최상위에 있고
// 다른 곳에서 참조될 수 있어, 부모를 바꾸는 편이 더 위험하다.
//
// 이 컴포넌트가 붙은 오브젝트는 항상 켜져 있어야 한다. 자기 자신을 껐다가는
// 다음 공간 전환 이벤트를 듣지 못해 영영 돌아오지 못한다. FadeCurtainUI가 같은
// 이유로 같은 모양을 하고 있다.
public class GameplaySpaceContent : MonoBehaviour
{
    [Tooltip("장식장에서만 켜집니다.")]
    [SerializeField] private GameObject[] _displayRoomObjects;

    [Tooltip("메인 필드에서만 켜집니다.")]
    [SerializeField] private GameObject[] _mainFieldObjects;

    private void Awake()
    {
        if (!HasValidTargets()) return;

        // 장식장에 있는지는 저장하지 않는 런타임 상태라 앱은 항상 메인 필드에서
        // 시작한다(기획서 §7.2). 구독이 붙기 전에도 이 상태가 맞다.
        Apply(EGameplaySpace.MainField);
    }

    private void Start()
    {
        if (!enabled) return;
        if (GameplaySpaceManager.Instance == null) return;

        GameplaySpaceManager.Instance.SpaceChanged += Apply;

        // 이미 장식장에 들어간 뒤에 붙었을 수 있다. 이벤트를 다시 기다릴 수 없으므로
        // 지금 상태를 한 번 반영한다.
        Apply(GameplaySpaceManager.Instance.CurrentSpace);
    }

    private void OnDestroy()
    {
        // 종료 순서는 보장되지 않아 매니저가 먼저 사라질 수 있다.
        if (GameplaySpaceManager.Instance == null) return;

        GameplaySpaceManager.Instance.SpaceChanged -= Apply;
    }

    private void Apply(EGameplaySpace space)
    {
        SetActive(_displayRoomObjects, space == EGameplaySpace.DisplayRoom);
        SetActive(_mainFieldObjects, space == EGameplaySpace.MainField);
    }

    private void SetActive(GameObject[] targets, bool isActive)
    {
        if (targets == null) return;

        foreach (GameObject target in targets)
        {
            if (target == null) continue;

            target.SetActive(isActive);
        }
    }

    // 목록을 채우는 것을 잊으면 조용히 아무 일도 일어나지 않는다. 그 상태가
    // 정상으로 보이므로, 자기 자신이 섞여 있는 경우까지 여기서 걸러 알린다.
    private bool HasValidTargets()
    {
        bool hasAny = (_displayRoomObjects != null && _displayRoomObjects.Length > 0) ||
                      (_mainFieldObjects != null && _mainFieldObjects.Length > 0);
        if (!hasAny)
        {
            Debug.LogError("공간별 오브젝트 목록이 비어 있습니다.", this);
            enabled = false;
            return false;
        }

        if (Contains(_displayRoomObjects, gameObject) ||
            Contains(_mainFieldObjects, gameObject))
        {
            Debug.LogError(
                "공간별 목록에 자기 자신이 들어 있습니다. 꺼지는 순간 공간 전환을 " +
                "다시 들을 수 없습니다.",
                this);
            enabled = false;
            return false;
        }

        return true;
    }

    private bool Contains(GameObject[] targets, GameObject target)
    {
        if (targets == null) return false;

        foreach (GameObject candidate in targets)
        {
            if (candidate == target) return true;
        }

        return false;
    }
}
