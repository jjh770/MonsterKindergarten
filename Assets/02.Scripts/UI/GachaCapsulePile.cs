using UnityEngine;

// 뽑기 통 안 캡슐 더미의 물리다. 통 안쪽 좌표에서 캡슐을 원으로 보고 중력, 서로 밀어내기, 벽과 바닥을 계산한다.
//
// Unity Physics2D를 쓰지 않는다. 기계는 Overlay 캔버스의 UI라서 월드 좌표가 화면 픽셀이고, 같은 물리 공간의
// 슬라임·장식장 콜라이더와 섞인다. 또 가짜 카메라가 부모의 크기와 위치를 매 프레임 바꾸면 리지드바디는 속도 없이
// 순간이동한다. 여기서는 통 안쪽 좌표만 다루므로 카메라 연출과 화면 크기에 영향을 받지 않는다.
//
// 위치 기반(Verlet) 적분이다. 속도는 지난 위치와의 차이로 갖고, 겹침은 위치를 직접 밀어서 푼다.
// 고정 간격으로 나눠 계산해서 프레임이 들쭉날쭉해도 같은 모습으로 쌓인다.
public sealed class GachaCapsulePile
{
    public const float StepSeconds = 1f / 120f;
    private const int MaxStepsPerFrame = 8;
    private const int SolverIterations = 6;
    private const float Damping = 0.996f;
    private const float FloorFriction = 0.12f;
    // 벽과 천장에 부딪히면 이만큼 튕겨 나오고, 바닥은 덜 튄다. 이보다 느리게 닿으면 튀지 않아서
    // 쌓여 있는 캡슐이 바닥에서 들썩이지 않는다. 속도는 한 간격에 움직이는 거리다.
    private const float WallBounce = 0.45f;
    private const float FloorBounce = 0.25f;
    private const float BounceMinStep = 1.5f;
    // 이보다 작게 움직이면 굴러간 것으로 치지 않는다. 쌓인 캡슐의 미세한 떨림이 회전으로 번지지 않게 한다.
    private const float RollThreshold = 0.05f;

    private readonly Vector2[] _position;
    private readonly Vector2[] _previous;
    private readonly Vector2[] _incoming;
    private readonly float[] _radius;
    private readonly float[] _angle;
    private readonly bool[] _inPile;
    private readonly bool[] _visible;
    private Vector2 _halfSize;
    private float _gravity;
    private float _accumulator;

    public GachaCapsulePile(int count)
    {
        _position = new Vector2[count];
        _previous = new Vector2[count];
        _incoming = new Vector2[count];
        _radius = new float[count];
        _angle = new float[count];
        _inPile = new bool[count];
        _visible = new bool[count];
    }

    public int Count => _position.Length;
    public Vector2 HalfSize => _halfSize;

    // 통이 움직이며 생기는 가속도다. 중력 위에 더한다. 통을 오른쪽으로 홱 당기면 캡슐은 왼쪽으로 쏠리고,
    // 통이 기울면 중력이 비스듬해진다. 통 쪽에서 매 프레임 정한다.
    public Vector2 FrameAcceleration { get; set; }

    public Vector2 GetPosition(int index) => _position[index];
    public float GetAngle(int index) => _angle[index];
    public float GetRadius(int index) => _radius[index];
    public bool IsVisible(int index) => _visible[index];
    public bool IsInPile(int index) => _inPile[index];

    // 통 안쪽의 절반 크기(가운데가 원점)와 중력을 정하고 모든 캡슐을 뺀다. 이어서 Place로 하나씩 넣는다.
    public void Reset(Vector2 halfSize, float gravity)
    {
        _halfSize = halfSize;
        _gravity = gravity;
        _accumulator = 0f;
        FrameAcceleration = Vector2.zero;
        for (int i = 0; i < Count; i++)
        {
            _inPile[i] = false;
            _visible[i] = false;
        }
    }

    public void Place(int index, Vector2 position, float radius, float angleDegrees)
    {
        _position[index] = position;
        _previous[index] = position;
        _radius[index] = Mathf.Max(1f, radius);
        _angle[index] = angleDegrees;
        _inPile[index] = true;
        _visible[index] = true;
    }

    // 프레임 시간만큼 고정 간격으로 나눠 진행한다. 한 프레임이 너무 길면 남는 시간은 버린다.
    public void Advance(float deltaTime)
    {
        _accumulator += Mathf.Max(0f, deltaTime);
        int steps = 0;
        while (_accumulator >= StepSeconds && steps < MaxStepsPerFrame)
        {
            Step();
            _accumulator -= StepSeconds;
            steps++;
        }

        if (steps == MaxStepsPerFrame) _accumulator = 0f;
    }

    // 화면에 보이기 전에 미리 쌓아 둘 때 쓴다.
    public void Simulate(float seconds)
    {
        int steps = Mathf.CeilToInt(seconds / StepSeconds);
        for (int i = 0; i < steps; i++)
        {
            Step();
        }

        _accumulator = 0f;
    }

    // 속도를 더한다. 속도는 지난 위치와의 차이이므로 지난 위치를 반대로 옮긴다.
    public void Kick(int index, Vector2 velocity)
    {
        if (!_inPile[index]) return;

        _previous[index] -= velocity * StepSeconds;
    }

    // 바닥 쪽에서 towardX에 가까운 캡슐 하나를 통 아래로 빠뜨린다. 배출구로 떨어진 것처럼 보이고,
    // 그 위의 캡슐들이 빈자리로 굴러 내려온다. 빠뜨린 캡슐은 바닥 아래로 사라지면 보이지 않게 된다.
    public int DropLowest(float towardX)
    {
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < Count; i++)
        {
            if (!_inPile[i]) continue;

            float score = _position[i].y + Mathf.Abs(_position[i].x - towardX) * 0.5f;
            if (score >= bestScore) continue;

            bestScore = score;
            best = i;
        }

        if (best >= 0) _inPile[best] = false;
        return best;
    }

    private void Step()
    {
        Vector2 pull = (new Vector2(0f, -_gravity) + FrameAcceleration) * (StepSeconds * StepSeconds);
        Vector2 fall = new(0f, -_gravity * StepSeconds * StepSeconds);
        for (int i = 0; i < Count; i++)
        {
            if (!_visible[i]) continue;

            Vector2 velocity = (_position[i] - _previous[i]) * Damping;
            _previous[i] = _position[i];
            // 통 밖으로 빠져 떨어지는 캡슐은 통의 움직임과 상관없이 그냥 떨어진다.
            _incoming[i] = velocity + (_inPile[i] ? pull : fall);
            _position[i] += _incoming[i];
        }

        for (int iteration = 0; iteration < SolverIterations; iteration++)
        {
            SolvePairs();
            SolveWalls();
        }

        for (int i = 0; i < Count; i++)
        {
            if (!_visible[i]) continue;

            // 바닥에 닿은 캡슐은 옆으로 미끄러지는 속도를 잃는다.
            if (_inPile[i] && _position[i].y <= -_halfSize.y + _radius[i] + 0.5f)
            {
                _previous[i].x = Mathf.Lerp(_previous[i].x, _position[i].x, FloorFriction);
            }

            // 굴러간 거리만큼 돈다. 오른쪽으로 구르면 시계 방향이다.
            float moved = _position[i].x - _previous[i].x;
            if (Mathf.Abs(moved) > RollThreshold)
            {
                _angle[i] -= moved / _radius[i] * Mathf.Rad2Deg;
            }

            if (_inPile[i]) Bounce(i);

            // 통 아래로 빠진 캡슐은 몸체 뒤로 충분히 내려가면 감춘다.
            if (!_inPile[i] && _position[i].y < -_halfSize.y - _radius[i] * 3f)
            {
                _visible[i] = false;
            }
        }
    }

    // 벽에 막힌 캡슐은 들어오던 속도를 잃는다. 그 속도의 일부를 반대로 돌려줘서 튕겨 나오게 한다.
    // 다음 간격의 속도는 지금 위치와 지난 위치의 차이이므로 지난 위치를 벽 바깥쪽으로 옮긴다.
    private void Bounce(int index)
    {
        Vector2 position = _position[index];
        Vector2 incoming = _incoming[index];
        float radius = _radius[index];
        const float touch = 0.5f;

        if (incoming.x < -BounceMinStep && position.x <= -_halfSize.x + radius + touch ||
            incoming.x > BounceMinStep && position.x >= _halfSize.x - radius - touch)
        {
            _previous[index].x = position.x + incoming.x * WallBounce;
        }

        if (incoming.y > BounceMinStep && position.y >= _halfSize.y - radius - touch)
        {
            _previous[index].y = position.y + incoming.y * WallBounce;
        }
        else if (incoming.y < -BounceMinStep && position.y <= -_halfSize.y + radius + touch)
        {
            _previous[index].y = position.y + incoming.y * FloorBounce;
        }
    }

    private void SolvePairs()
    {
        for (int i = 0; i < Count; i++)
        {
            if (!_inPile[i]) continue;

            for (int j = i + 1; j < Count; j++)
            {
                if (!_inPile[j]) continue;

                Vector2 delta = _position[j] - _position[i];
                float reach = _radius[i] + _radius[j];
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared >= reach * reach) continue;

                float distance = Mathf.Sqrt(distanceSquared);
                // 정확히 겹치면 방향이 없으므로 옆으로 민다.
                Vector2 normal = distance > 0.0001f ? delta / distance : Vector2.right;
                Vector2 push = normal * ((reach - distance) * 0.5f);
                _position[i] -= push;
                _position[j] += push;
            }
        }
    }

    private void SolveWalls()
    {
        for (int i = 0; i < Count; i++)
        {
            if (!_inPile[i]) continue;

            float radius = _radius[i];
            Vector2 position = _position[i];
            position.x = Mathf.Clamp(position.x, -_halfSize.x + radius, _halfSize.x - radius);
            position.y = Mathf.Clamp(position.y, -_halfSize.y + radius, _halfSize.y - radius);
            _position[i] = position;
        }
    }
}
