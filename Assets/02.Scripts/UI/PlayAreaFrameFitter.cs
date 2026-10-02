using UnityEngine;

// 모드 테두리를 놀이 구역에 맞춘다. 보내기 모드와 배치 모드가 함께 쓴다.
//
// 테두리를 캔버스 폭에서 일정한 여백을 빼는 식으로 잡으면 기기마다 벽과
// 어긋난다. 카메라는 세로를 기준으로 보여 주므로 가로로 넓은 화면일수록 벽이
// 화면에서 차지하는 폭이 줄어드는데, 캔버스 여백 방식은 그 반대로 움직인다.
// 19.5:9에서 22만큼 남던 차이가 4:3 화면에서는 271까지 벌어진다.
//
// 그래서 벽의 월드 좌표를 카메라로 화면에 옮겨 그대로 쓴다. 화면 비율이
// 무엇이든 테두리와 돌아다니는 범위가 같아진다.
[RequireComponent(typeof(RectTransform))]
public sealed class PlayAreaFrameFitter : MonoBehaviour
{
    [SerializeField] private RectTransform _frame;
    [SerializeField] private PlayAreaBounds _area;

    // 월드 단위로 주면 두 공간의 화면상 여백이 달라진다. 장식장은 필드보다
    // 넓지만 카메라도 그만큼 물러나 있어서, 같은 0.1이 화면에서는 다른 두께가 된다.
    // 세로를 기준으로 삼는 것은 카메라가 세로를 고정하기 때문이다. 가로에도 같은
    // 거리를 쓰므로 네 변의 여백이 같아진다.
    [Tooltip("벽 바깥으로 구역 높이의 몇 배만큼 더 둘러쌀지입니다.")]
    [SerializeField, Min(0f)] private float _marginRatio = 0.02f;

    private Camera _camera;
    private Vector2Int _lastScreenSize;
    private Vector3 _lastCameraPosition;
    private float _lastOrthographicSize;
    private Rect _lastWorldRect;
    private bool _isDirty = true;

    private void Awake()
    {
        if (_frame == null) _frame = transform as RectTransform;

        if (_area == null)
        {
            Debug.LogError("테두리가 맞출 놀이 구역이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _frame.anchorMin = new Vector2(0.5f, 0.5f);
        _frame.anchorMax = new Vector2(0.5f, 0.5f);
        _frame.pivot = new Vector2(0.5f, 0.5f);
    }

    private void OnEnable()
    {
        // 꺼져 있는 동안 화면이 바뀌었을 수 있다. 켤 때는 한 번 다시 잰다.
        _isDirty = true;
        Apply();
    }

    // 카메라가 움직이는 동안에도 따라와야 한다.
    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (!enabled || _frame == null) return;

        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null) return;
        }

        if (!_area.HasWalls) return;
        if (_frame.parent is not RectTransform parent) return;

        Rect world = _area.WorldRect;
        if (!ShouldRecompute(world)) return;

        float margin = world.height * _marginRatio;
        Vector2 min = ToLocal(
            parent, new Vector2(world.xMin - margin, world.yMin - margin));
        Vector2 max = ToLocal(
            parent, new Vector2(world.xMax + margin, world.yMax + margin));

        _frame.sizeDelta = max - min;
        // 앵커는 부모 가운데다. 부모의 pivot이 가운데가 아닐 수 있으므로 빼 준다.
        _frame.anchoredPosition = (min + max) * 0.5f - parent.rect.center;

        _isDirty = false;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _lastCameraPosition = _camera.transform.position;
        _lastOrthographicSize = _camera.orthographicSize;
        _lastWorldRect = world;
    }

    // RectTransform에 값을 쓰면 그 캔버스의 레이아웃이 더럽혀진다. 이 테두리 아래에
    // 버튼 줄이 매달려 있어서, 매 프레임 쓰면 그 줄도 매 프레임 다시 계산된다.
    // 바뀐 것이 없으면 쓰지 않는다.
    private bool ShouldRecompute(Rect world)
    {
        if (_isDirty) return true;
        if (_lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height) return true;
        if (_lastCameraPosition != _camera.transform.position) return true;
        if (!Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize)) return true;

        return _lastWorldRect != world;
    }

    private Vector2 ToLocal(RectTransform parent, Vector2 world)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(_camera, world);

        // 오버레이 캔버스라 되돌릴 때는 카메라를 넘기지 않는다.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, screen, null, out Vector2 local);
        return local;
    }
}
