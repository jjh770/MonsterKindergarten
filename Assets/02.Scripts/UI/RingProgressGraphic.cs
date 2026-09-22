using System.Collections.Generic;
using UnityEngine;

// Image 스프라이트에 의존하지 않는 테두리 게이지와 완료 펄스.
//
// 경로는 둥근 네모다. 네모 버튼을 감싸면 버튼 크기 그대로 둘레가 차오르고, 모서리
// 반지름을 칸 절반 이상으로 두면 원이 된다. 12시 방향에서 시작해 시계 방향으로 돈다.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RingProgressGraphic : UnityEngine.UI.MaskableGraphic
{
    private const int CornerSegments = 10;
    private const float PulseDuration = 0.45f;
    // 펄스가 바깥으로 번지는 폭. 칸 가장자리에서 이만큼 안쪽에 띠를 그려 잘리지 않게 한다.
    private const float GlowSpread = 3f;

    [SerializeField] private Color _trackColor = new(0.18f, 0.12f, 0.07f, 0.72f);
    [SerializeField] private Color _progressColor = new(0.48f, 0.92f, 0.27f, 0.95f);
    [SerializeField] private Color _glowColor = new(1f, 0.96f, 0.58f, 1f);
    [SerializeField, Min(1f)] private float _thickness = 7f;

    [Tooltip("띠 바깥쪽의 모서리 반지름입니다. 칸 절반 이상이면 원으로 그립니다.")]
    [SerializeField, Min(0f)] private float _cornerRadius = 1000f;

    private readonly List<Vector2> _outer = new();
    private readonly List<Vector2> _inner = new();
    private readonly List<float> _lengths = new();
    private float _progress;
    private float _pulseRemaining;

    public void Configure(Color trackColor, Color progressColor)
    {
        _trackColor = trackColor;
        _progressColor = progressColor;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void SetProgress(float progress01)
    {
        _progress = Mathf.Clamp01(progress01);
        SetVerticesDirty();
    }

    public void PlayCompletionPulse()
    {
        _pulseRemaining = PulseDuration;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (_pulseRemaining <= 0f) return;

        _pulseRemaining -= Time.unscaledDeltaTime;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        // 피벗이 가운데가 아니어도 칸 한가운데에 그린다.
        Vector2 center = rect.center;
        Vector2 half = new Vector2(rect.width, rect.height) * 0.5f -
                       Vector2.one * GlowSpread;
        if (half.x <= _thickness || half.y <= _thickness) return;

        DrawBand(vertexHelper, center, half, _cornerRadius, _thickness, 1f, _trackColor);

        // 펄스는 한 바퀴를 채운 직후에 나온다. 그때 진행도는 이미 0으로 돌아가 있으므로
        // 진행도 길이로 그리면 보이지 않는다. 방금 채운 한 바퀴 전체를 빛나게 한다.
        float pulse01 = _pulseRemaining > 0f
            ? Mathf.Sin((1f - _pulseRemaining / PulseDuration) * Mathf.PI)
            : 0f;
        if (pulse01 > 0f)
        {
            DrawBand(
                vertexHelper,
                center,
                half + Vector2.one * GlowSpread,
                _cornerRadius + GlowSpread,
                _thickness + GlowSpread * 2f,
                1f,
                WithAlpha(_glowColor, 0.28f * pulse01));
            DrawBand(
                vertexHelper,
                center,
                half,
                _cornerRadius,
                _thickness,
                1f,
                WithAlpha(_glowColor, pulse01));
        }

        if (_progress > 0f)
        {
            DrawBand(
                vertexHelper,
                center,
                half,
                _cornerRadius,
                _thickness,
                _progress,
                _progressColor);
        }
    }

    // 바깥 경로와 안쪽 경로를 같은 수의 점으로 만들고, 둘 사이를 사각형으로 잇는다.
    // 진행도는 바깥 경로 길이 기준이라 모서리에서 빨라지거나 느려지지 않는다.
    private void DrawBand(
        UnityEngine.UI.VertexHelper vertexHelper,
        Vector2 center,
        Vector2 outerHalf,
        float outerRadius,
        float thickness,
        float amount,
        Color color)
    {
        float radius = Mathf.Min(outerRadius, outerHalf.x, outerHalf.y);
        Vector2 innerHalf = outerHalf - Vector2.one * thickness;
        float innerRadius = Mathf.Max(0f, radius - thickness);

        BuildPath(_outer, outerHalf, radius);
        BuildPath(_inner, innerHalf, innerRadius);

        _lengths.Clear();
        float total = 0f;
        for (int i = 0; i < _outer.Count - 1; i++)
        {
            float length = Vector2.Distance(_outer[i], _outer[i + 1]);
            _lengths.Add(length);
            total += length;
        }

        float target = total * amount;
        float walked = 0f;
        for (int i = 0; i < _lengths.Count && walked < target; i++)
        {
            float length = _lengths[i];
            float t = length > 0f ? Mathf.Clamp01((target - walked) / length) : 1f;
            Vector2 outerEnd = Vector2.Lerp(_outer[i], _outer[i + 1], t);
            Vector2 innerEnd = Vector2.Lerp(_inner[i], _inner[i + 1], t);
            AddQuad(
                vertexHelper,
                center + _outer[i],
                center + outerEnd,
                center + innerEnd,
                center + _inner[i],
                color);
            walked += length;
        }
    }

    // 12시에서 시작해 시계 방향으로 한 바퀴 돌아 다시 12시로 오는 점 목록.
    private static void BuildPath(List<Vector2> points, Vector2 half, float radius)
    {
        points.Clear();
        points.Add(new Vector2(0f, half.y));
        AddCorner(points, new Vector2(half.x - radius, half.y - radius), radius, 90f, 0f);
        AddCorner(points, new Vector2(half.x - radius, -half.y + radius), radius, 0f, -90f);
        AddCorner(points, new Vector2(-half.x + radius, -half.y + radius), radius, -90f, -180f);
        AddCorner(points, new Vector2(-half.x + radius, half.y - radius), radius, 180f, 90f);
        points.Add(new Vector2(0f, half.y));
    }

    private static void AddCorner(
        List<Vector2> points,
        Vector2 cornerCenter,
        float radius,
        float fromDegrees,
        float toDegrees)
    {
        for (int i = 0; i <= CornerSegments; i++)
        {
            float degrees = Mathf.Lerp(fromDegrees, toDegrees, i / (float)CornerSegments);
            float angle = degrees * Mathf.Deg2Rad;
            points.Add(cornerCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private static void AddQuad(
        UnityEngine.UI.VertexHelper vertexHelper,
        Vector2 outerStart,
        Vector2 outerEnd,
        Vector2 innerEnd,
        Vector2 innerStart,
        Color color)
    {
        int index = vertexHelper.currentVertCount;
        vertexHelper.AddVert(outerStart, color, Vector2.zero);
        vertexHelper.AddVert(outerEnd, color, Vector2.zero);
        vertexHelper.AddVert(innerEnd, color, Vector2.zero);
        vertexHelper.AddVert(innerStart, color, Vector2.zero);
        vertexHelper.AddTriangle(index, index + 1, index + 2);
        vertexHelper.AddTriangle(index + 2, index + 3, index);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
