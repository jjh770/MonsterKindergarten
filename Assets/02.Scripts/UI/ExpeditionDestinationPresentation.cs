using System;
using UnityEngine;
using UnityEngine.UI;

// 탐험지 화면에서 떠나 있는 슬라임들이 돌아다니는 모습을 UI에서 재생한다. 표시만 담당한다.
//
// 슬라임마다 탐험지 안의 아무 지점을 골라 걸어가고, 닿으면 잠깐 쉬었다가 다음 지점을 고른다.
// 가끔은 다른 슬라임 곁으로 놀러 가고, 쉬는 동안 두리번거리거나 통통 뛴다. 세 마리가 한자리에
// 포개지지 않도록 가까워지면 서로 비켜 선다. 걷는 속도는 슬라임마다 조금씩 다르다.
//
// 프레임은 메인 필드와 같은 그림을 쓴다. 메인 필드 Animator가 걷기와 쉬기를 0.5배속으로 재생하므로
// 여기서도 같은 배속으로 돌린다. 화면을 열 때마다 새 무작위 값으로 시작해 매번 다른 길로 다닌다.
public sealed class ExpeditionDestinationPresentation : MonoBehaviour
{
    [Serializable]
    private sealed class SpriteTrack
    {
        public Sprite[] Frames;
        public float[] Times;
        public float Duration;
        public Sprite At(float elapsed, Sprite fallback)
        {
            if (Frames == null || Frames.Length == 0 || Times == null || Times.Length != Frames.Length) return fallback;
            float time = Mathf.Repeat(elapsed, Mathf.Max(.01f, Duration));
            int index = 0;
            while (index + 1 < Times.Length && Times[index + 1] <= time) index++;
            return Frames[index] != null ? Frames[index] : fallback;
        }
    }

    [Serializable]
    private sealed class GradeMotion
    {
        public ESlimeGrade Grade;
        public SpriteTrack Idle;
        public SpriteTrack Moving;
    }

    private sealed class VisitorState
    {
        public Vector2 Target;
        public float Speed;
        public float Remaining;
        public float AnimationTime;
        public bool Moving;
        public float HopTime = -1f;
        public float Facing = 1f;
        public Sprite Fallback;
        public GradeMotion Motion;
    }

    [SerializeField] private RectTransform _area;
    [SerializeField] private Image[] _visitors;
    [SerializeField] private GradeMotion[] _motions;

    [Header("Walk")]
    [Tooltip("걷는 속도 범위입니다(초당 UI 단위). 슬라임마다 이 안에서 하나를 고릅니다.")]
    [SerializeField] private Vector2 _speedRange = new(70f, 110f);
    [Tooltip("다음 목적지를 고를 때 지금 자리에서 적어도 이만큼은 떨어진 곳을 고릅니다.")]
    [SerializeField, Min(0f)] private float _minTravel = 160f;
    [Tooltip("목적지에 닿은 뒤 쉬는 시간 범위입니다(초).")]
    [SerializeField] private Vector2 _idleRange = new(0.8f, 2.6f);
    [Tooltip("다음 목적지를 다른 슬라임 곁으로 고를 확률입니다.")]
    [SerializeField, Range(0f, 1f)] private float _visitFriendChance = 0.25f;

    [Header("Idle")]
    [Tooltip("쉬는 동안 한 번 통통 뛸 확률입니다.")]
    [SerializeField, Range(0f, 1f)] private float _hopChance = 0.35f;
    [SerializeField, Min(0f)] private float _hopHeight = 34f;
    [SerializeField, Min(0.05f)] private float _hopDuration = 0.42f;

    [Header("Spacing")]
    [Tooltip("두 슬라임이 이보다 가까우면 서로 비켜 섭니다.")]
    [SerializeField, Min(0f)] private float _personalSpace = 120f;

    [Header("Animation")]
    [Tooltip("메인 필드 Animator의 걷기·쉬기 상태 배속과 같게 둡니다.")]
    [SerializeField, Min(0.01f)] private float _animationSpeed = 0.5f;

    // 막 출발한 탐험은 출발 연출이 끝난 가운데 줄에서 시작한다. 그 뒤에 다시 열면 흩어진 자리에서 시작한다.
    private static readonly TimeSpan JustDepartedWindow = TimeSpan.FromSeconds(5);
    private const float WalkMarginX = 90f;
    private const float WalkMarginY = 65f;

    private readonly VisitorState[] _states = new VisitorState[3];
    private readonly Vector2[] _positions = new Vector2[3];
    private ExpeditionRun _run;

    public void Show(ExpeditionRun run, SlimeManager slimes)
    {
        if (_run?.RunId == run?.RunId) return;
        _run = run;
        bool justDeparted = run != null && ServerClock.TrustedUtcNow - run.StartedAtUtc < JustDepartedWindow;
        for (int i = 0; i < _visitors.Length; i++)
        {
            bool active = run != null && i < run.Members.Count;
            _visitors[i].gameObject.SetActive(active);
            _states[i] = null;
            if (!active) continue;
            ESlimeGrade grade = run.Members[i].Grade;
            Vector2 start = justDeparted ? new Vector2((i - 1) * 130f, 0f) : RandomPoint();
            _positions[i] = start;
            _states[i] = new VisitorState
            {
                Speed = UnityEngine.Random.Range(_speedRange.x, _speedRange.y),
                // 처음에는 엇갈려 출발하도록 쉬는 시간을 조금씩 다르게 준다.
                Remaining = UnityEngine.Random.Range(0.1f, 1.2f),
                Fallback = slimes.Get(grade).SpecData.Sprite,
                Motion = _motions == null ? null : Array.Find(_motions, motion => motion.Grade == grade),
                AnimationTime = UnityEngine.Random.Range(0f, 1f),
                Facing = UnityEngine.Random.value < .5f ? -1f : 1f,
            };
            _visitors[i].sprite = _states[i].Fallback;
            _visitors[i].rectTransform.anchoredPosition = start;
            _visitors[i].transform.localScale = new Vector3(_states[i].Facing, 1f, 1f);
        }
    }

    // 귀환 연출이 출발할 자리다. 지금 보이는 방문자의 화면 위치와 크기를 돌려준다.
    public bool TryGetVisitor(int index, out Vector3 position, out Vector2 size)
    {
        position = default;
        size = default;
        if (_run == null || index < 0 || index >= _visitors.Length || !_visitors[index].gameObject.activeSelf) return false;

        position = _visitors[index].transform.position;
        size = _visitors[index].rectTransform.rect.size;
        return true;
    }

    private void Update()
    {
        if (_run == null) return;
        bool finished = _run.IsReadyToReturn(ServerClock.TrustedUtcNow);
        float delta = Mathf.Min(Time.unscaledDeltaTime, .1f);
        int count = Mathf.Min(_run.Members.Count, _visitors.Length);
        for (int i = 0; i < count; i++)
        {
            VisitorState state = _states[i];
            if (state == null) continue;

            state.AnimationTime += delta * _animationSpeed;
            if (finished) state.Moving = false;
            else if (state.Moving) Walk(i, state, delta);
            else Rest(i, state, delta);
        }

        if (!finished) KeepApart(count, delta);

        for (int i = 0; i < count; i++)
        {
            VisitorState state = _states[i];
            if (state == null) continue;
            Apply(i, state, delta);
        }
    }

    private void Walk(int index, VisitorState state, float delta)
    {
        Vector2 toTarget = state.Target - _positions[index];
        float distance = toTarget.magnitude;
        // 목적지 근처에서는 천천히 멈춘다.
        float speed = state.Speed * Mathf.Clamp01(distance / 40f + .25f);
        float step = speed * delta;
        if (distance <= step || distance < 2f)
        {
            _positions[index] = state.Target;
            state.Moving = false;
            state.Remaining = UnityEngine.Random.Range(_idleRange.x, _idleRange.y);
            if (UnityEngine.Random.value < _hopChance) state.HopTime = 0f;
            return;
        }

        Vector2 direction = toTarget / distance;
        _positions[index] += direction * step;
        if (Mathf.Abs(direction.x) > .15f) state.Facing = direction.x > 0f ? 1f : -1f;
    }

    private void Rest(int index, VisitorState state, float delta)
    {
        state.Remaining -= delta;
        // 쉬는 중에 가끔 고개를 돌린다.
        if (state.HopTime < 0f && UnityEngine.Random.value < delta * .4f) state.Facing = -state.Facing;
        if (state.Remaining > 0f) return;

        state.Target = PickTarget(index);
        state.Moving = true;
        state.HopTime = -1f;
    }

    // 세 마리가 한자리에 포개지지 않게 가까운 쌍을 서로 밀어낸다.
    private void KeepApart(int count, float delta)
    {
        for (int a = 0; a < count; a++)
        {
            for (int b = a + 1; b < count; b++)
            {
                if (_states[a] == null || _states[b] == null) continue;

                Vector2 offset = _positions[b] - _positions[a];
                float distance = offset.magnitude;
                if (distance >= _personalSpace) continue;

                Vector2 push = distance > .01f ? offset / distance : Vector2.right;
                float amount = (_personalSpace - distance) * Mathf.Min(1f, delta * 4f) * .5f;
                _positions[a] = Clamp(_positions[a] - push * amount);
                _positions[b] = Clamp(_positions[b] + push * amount);
            }
        }
    }

    private void Apply(int index, VisitorState state, float delta)
    {
        RectTransform rect = _visitors[index].rectTransform;
        float hop = 0f;
        if (state.HopTime >= 0f)
        {
            state.HopTime += delta;
            float t = state.HopTime / _hopDuration;
            if (t >= 1f) state.HopTime = -1f;
            else hop = Mathf.Sin(t * Mathf.PI) * _hopHeight;
        }

        rect.anchoredPosition = _positions[index] + new Vector2(0f, hop);
        rect.localScale = new Vector3(state.Facing, 1f, 1f);
        SpriteTrack track = state.Moving ? state.Motion?.Moving : state.Motion?.Idle;
        _visitors[index].sprite = track?.At(state.AnimationTime, state.Fallback) ?? state.Fallback;
    }

    private Vector2 PickTarget(int index)
    {
        Vector2 from = _positions[index];
        if (UnityEngine.Random.value < _visitFriendChance)
        {
            int friend = UnityEngine.Random.Range(0, _visitors.Length);
            if (friend != index && _states[friend] != null)
            {
                // 친구 바로 옆, 비켜 설 거리만큼 떨어진 자리로 간다.
                Vector2 side = new(UnityEngine.Random.value < .5f ? -1f : 1f, UnityEngine.Random.Range(-.4f, .4f));
                return Clamp(_positions[friend] + side.normalized * _personalSpace);
            }
        }

        Vector2 best = RandomPoint();
        for (int attempt = 0; attempt < 6 && Vector2.Distance(best, from) < _minTravel; attempt++)
            best = RandomPoint();
        return best;
    }

    private Vector2 RandomPoint()
    {
        Vector2 half = HalfWalkSize();
        return new Vector2(UnityEngine.Random.Range(-half.x, half.x), UnityEngine.Random.Range(-half.y, half.y));
    }

    private Vector2 Clamp(Vector2 point)
    {
        Vector2 half = HalfWalkSize();
        return new Vector2(Mathf.Clamp(point.x, -half.x, half.x), Mathf.Clamp(point.y, -half.y, half.y));
    }

    private Vector2 HalfWalkSize() => new(
        Mathf.Max(0f, _area.rect.width * .5f - WalkMarginX),
        Mathf.Max(0f, _area.rect.height * .5f - WalkMarginY));
}
