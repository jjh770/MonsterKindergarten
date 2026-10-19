using System;
using UnityEngine;
using UnityEngine.UI;

// 실제 필드와 같은 이동·대기 스프라이트 프레임을 UI에서 재생한다. 표시만 담당한다.
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
        public System.Random Random;
        public Vector2 Velocity;
        public Vector2 TargetVelocity;
        public float Remaining;
        public float AnimationTime;
        public bool Moving;
        public Sprite Fallback;
        public GradeMotion Motion;
    }

    [SerializeField] private RectTransform _area;
    [SerializeField] private Image[] _visitors;
    [SerializeField] private GradeMotion[] _motions;
    [SerializeField, Min(1f)] private float _moveSpeed = 42f;
    private readonly VisitorState[] _states = new VisitorState[3];
    private ExpeditionRun _run;

    public void Show(ExpeditionRun run, SlimeManager slimes)
    {
        if (_run?.RunId == run?.RunId) return;
        _run = run;
        for (int i = 0; i < _visitors.Length; i++)
        {
            bool active = run != null && i < run.Members.Count;
            _visitors[i].gameObject.SetActive(active);
            _states[i] = null;
            if (!active) continue;
            ESlimeGrade grade = run.Members[i].Grade;
            var random = new System.Random(Seed(run.RunId, i));
            _states[i] = new VisitorState
            {
                Random = random,
                Remaining = Range(random, .3f, 2f),
                Fallback = slimes.Get(grade).SpecData.Sprite,
                Motion = _motions == null ? null : Array.Find(_motions, motion => motion.Grade == grade),
                AnimationTime = Range(random, 0f, 1f),
            };
            _visitors[i].sprite = _states[i].Fallback;
            _visitors[i].rectTransform.anchoredPosition = new Vector2((i - 1) * 130f, 0f);
            _visitors[i].transform.localScale = Vector3.one;
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
        for (int i = 0; i < _run.Members.Count && i < _visitors.Length; i++)
        {
            VisitorState state = _states[i];
            state.AnimationTime += delta;
            state.Remaining -= delta;
            if (finished)
            {
                state.Velocity = state.TargetVelocity = Vector2.zero;
            }
            else if (state.Remaining <= 0f)
            {
                state.Moving = !state.Moving;
                state.Remaining = state.Moving ? Range(state.Random, .5f, 3f) : Range(state.Random, .3f, 2f);
                float angle = Range(state.Random, 0f, Mathf.PI * 2f);
                state.TargetVelocity = state.Moving ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _moveSpeed : Vector2.zero;
            }
            state.Velocity = Vector2.Lerp(state.Velocity, state.TargetVelocity, 1f - Mathf.Exp(-10f * delta));
            RectTransform rect = _visitors[i].rectTransform;
            Vector2 next = rect.anchoredPosition + state.Velocity * delta;
            float halfWidth = Mathf.Max(0f, _area.rect.width * .5f - 90f);
            float halfHeight = Mathf.Max(0f, _area.rect.height * .5f - 65f);
            if (Mathf.Abs(next.x) > halfWidth)
            {
                next.x = Mathf.Clamp(next.x, -halfWidth, halfWidth);
                state.Velocity.x *= -1f;
                state.TargetVelocity.x *= -1f;
            }
            if (Mathf.Abs(next.y) > halfHeight)
            {
                next.y = Mathf.Clamp(next.y, -halfHeight, halfHeight);
                state.Velocity.y *= -1f;
                state.TargetVelocity.y *= -1f;
            }
            rect.anchoredPosition = next;
            if (Mathf.Abs(state.Velocity.x) > _moveSpeed * .1f)
                rect.localScale = new Vector3(state.Velocity.x > 0f ? 1f : -1f, 1f, 1f);
            SpriteTrack track = state.Velocity.magnitude > _moveSpeed * .1f ? state.Motion?.Moving : state.Motion?.Idle;
            _visitors[i].sprite = track?.At(state.AnimationTime, state.Fallback) ?? state.Fallback;
        }
    }

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
    private static int Seed(string id, int index)
    {
        unchecked
        {
            int value = index + 17;
            foreach (char letter in id) value = value * 31 + letter;
            return value;
        }
    }
}
