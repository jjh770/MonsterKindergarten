using UnityEngine;

// 슬라임이 돌아다닐 수 있는 네모를 벽 콜라이더의 안쪽 면에서 읽는다.
//
// 같은 값을 화면 쪽에 한 번 더 적어 두면 벽을 옮겼을 때 조용히 어긋난다.
// 테두리를 그리는 쪽이 이 네모만 보게 해서 벽이 곧 진실이 되게 한다.
//
// 꺼져 있어도 값을 낼 수 있어야 한다. 두 공간 중 한쪽 벽은 언제나 꺼져 있고,
// Collider2D.bounds는 꺼진 콜라이더에서 크기 0을 돌려준다. 그래서 위치와
// 크기를 직접 계산한다.
public sealed class PlayAreaBounds : MonoBehaviour
{
    [SerializeField] private BoxCollider2D _left;
    [SerializeField] private BoxCollider2D _right;
    [SerializeField] private BoxCollider2D _upper;
    [SerializeField] private BoxCollider2D _under;

    [Tooltip("오브젝트를 놓는 구역입니다. 배치 규칙과 어긋나면 에디터에서 알려 줍니다.")]
    [SerializeField] private bool _isPlaygroundArea;

    public bool HasWalls =>
        _left != null && _right != null && _upper != null && _under != null;

    public Rect WorldRect
    {
        get
        {
            if (!HasWalls) return Rect.zero;

            return Rect.MinMaxRect(
                InnerX(_left, isLeftWall: true),
                InnerY(_under, isUnderWall: true),
                InnerX(_right, isLeftWall: false),
                InnerY(_upper, isUnderWall: false));
        }
    }

    private void Awake()
    {
        if (!HasWalls)
        {
            Debug.LogError("놀이 구역의 벽 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        WarnWhenRulesDisagree();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        WarnWhenRulesDisagree();
    }
#endif

    // 놓을 수 있는 범위는 도메인의 상수다. 벽을 옮기면 테두리는 따라오지만
    // 그 상수는 따라오지 않아, 테두리 안인데 놓이지 않는 자리가 생긴다.
    //
    // 상수를 벽에서 읽어 오지는 않는다. 세이브를 불러올 때 쓰는 값이라 씬에
    // 매달면 저장된 위치의 유효성이 씬을 따라 흔들린다. 어긋났다는 사실만 알린다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void WarnWhenRulesDisagree()
    {
        if (!_isPlaygroundArea || !HasWalls) return;

        Rect area = WorldRect;
        if (PlaygroundRules.MatchesWalls(area.xMin, area.yMin, area.xMax, area.yMax)) return;

        Debug.LogWarning(
            $"놀이터 벽 x[{area.xMin:F2}~{area.xMax:F2}] y[{area.yMin:F2}~{area.yMax:F2}]이 " +
            $"배치 규칙 x[{PlaygroundRules.AreaMinX:F2}~{PlaygroundRules.AreaMaxX:F2}] " +
            $"y[{PlaygroundRules.AreaMinY:F2}~{PlaygroundRules.AreaMaxY:F2}]과 어긋납니다. " +
            $"벽 안쪽에서 {PlaygroundRules.WallInset:F2}만큼 물러난 값이어야 합니다.",
            this);
    }

    private static float InnerX(BoxCollider2D wall, bool isLeftWall)
    {
        float half = HalfSize(wall).x;
        float center = Center(wall).x;
        return isLeftWall ? center + half : center - half;
    }

    private static float InnerY(BoxCollider2D wall, bool isUnderWall)
    {
        float half = HalfSize(wall).y;
        float center = Center(wall).y;
        return isUnderWall ? center + half : center - half;
    }

    private static Vector2 Center(BoxCollider2D wall)
    {
        return wall.transform.TransformPoint(wall.offset);
    }

    // 벽은 돌아가 있지 않으므로 축마다 배율만 곱하면 된다.
    private static Vector2 HalfSize(BoxCollider2D wall)
    {
        Vector3 scale = wall.transform.lossyScale;
        return new Vector2(
            Mathf.Abs(wall.size.x * scale.x),
            Mathf.Abs(wall.size.y * scale.y)) * 0.5f;
    }
}
