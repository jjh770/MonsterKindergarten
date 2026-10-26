using UnityEngine;
using UnityEngine.UI;

// 뽑기 화면의 빛과 반짝이를 맡는다. 캡슐 층(CapsuleLayer)에 붙고, 좌표는 모두 그 층의 로컬 좌표다.
//
// 빛은 캡슐이 열릴 때 등급을 보여 준다. 큰 빛 하나는 하나만 열 때, 캡슐마다의 작은 빛은 여러 개를
// 한꺼번에 열 때 쓴다. 반짝이는 한 벌뿐이라 티켓이 날아갈 때의 꼬리, 캡슐이 터질 때의 조각,
// 슬라임이 솟는 동안 퍼지는 빛이 번갈아 쓴다. 겹쳐 쓰지 않는 것은 부르는 쪽(GachaMachineView)의 순서가 지킨다.
//
// 언제 얼마나 크게 빛날지는 GachaMachineView가 정하고, 여기서는 받은 값으로 그리기만 한다.
public sealed class GachaMachineLights : MonoBehaviour
{
    [SerializeField] private Image _glow;
    [SerializeField] private Image[] _twinkles;
    [Tooltip("여러 개를 한꺼번에 열 때 캡슐마다 하나씩 쓰는 빛입니다. 캡슐과 같은 순서로, 캡슐 수만큼 둡니다.")]
    [SerializeField] private Image[] _multiGlows;
    [Tooltip("한꺼번에 열 때 캡슐마다의 빛 크기 배율입니다. 캡슐끼리 가까워 하나일 때보다 작게 씁니다.")]
    [SerializeField, Min(0.1f)] private float _multiGlowScale = 0.6f;
    [SerializeField, Min(0f)] private float _glowBaseScale = 1f;
    [Tooltip("티켓이 날아갈 때 꼬리 반짝이를 하나씩 남기는 간격입니다.")]
    [SerializeField, Min(0.01f)] private float _trailInterval = 0.04f;
    [Tooltip("꼬리 반짝이 하나가 남아 있는 시간입니다.")]
    [SerializeField, Min(0.05f)] private float _trailLife = 0.45f;

    private float[] _trailBirth;
    private Vector2[] _trailPosition;
    private int _trailNext;
    private float _trailNextEmit;
    private bool _isReady;

    public int MultiGlowCount => _multiGlows != null ? _multiGlows.Length : 0;

    private void Awake()
    {
        if (_glow == null || _twinkles == null || _twinkles.Length == 0 || !IsValidMultiGlows())
        {
            Debug.LogError("뽑기 화면 빛의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _trailBirth = new float[_twinkles.Length];
        _trailPosition = new Vector2[_twinkles.Length];
        _isReady = true;
    }

    public void HideAll()
    {
        if (!_isReady) return;

        _glow.gameObject.SetActive(false);
        HideMultiGlows();
        HideTwinkles();
    }

    public void HideTwinkles()
    {
        if (!_isReady) return;

        foreach (Image twinkle in _twinkles)
        {
            if (twinkle != null) twinkle.gameObject.SetActive(false);
        }
    }

    // 하나만 뽑을 때 열리기를 기다리는 캡슐 뒤가 은은히 빛나 시선을 끈다.
    public void ShowIdleGlow(Vector2 at, float alpha)
    {
        if (!_isReady) return;

        _glow.gameObject.SetActive(true);
        _glow.rectTransform.anchoredPosition = at;
        _glow.rectTransform.localScale = Vector3.one * (_glowBaseScale * 0.9f);
        SetImageColor(_glow, Color.white, alpha);
    }

    // 캡슐이 흔들리며 뜸을 들이는 동안 빛이 점점 커지고 짙어진다. ratio는 뜸의 진행이다.
    public void ChargeGlow(Vector2 at, float glowScale, float ratio, Color tint)
    {
        if (!_isReady) return;

        _glow.gameObject.SetActive(true);
        _glow.rectTransform.anchoredPosition = at;
        _glow.rectTransform.localScale = Vector3.one *
                                         (_glowBaseScale * Mathf.Lerp(0.7f, glowScale * 0.8f, ratio));
        SetImageColor(_glow, tint, Mathf.Lerp(0.25f, 0.9f, ratio));
    }

    // 캡슐이 갈라지는 동안 빛이 한 번 더 부풀며 옅어지고, 반짝이 twinkles개가 둘레로 터진다.
    public void BurstGlow(Vector2 at, float glowScale, int twinkles, float ratio, Color tint)
    {
        if (!_isReady) return;

        float outward = 1f - (1f - ratio) * (1f - ratio);
        _glow.rectTransform.localScale = Vector3.one *
                                         (_glowBaseScale * Mathf.Lerp(glowScale * 0.8f, glowScale * 1.25f, outward));
        SetImageColor(_glow, tint, 1f - ratio);
        AnimateTwinkleBurst(at, twinkles, ratio, tint);
    }

    public void HideGlow()
    {
        if (_isReady) _glow.gameObject.SetActive(false);
    }

    // 여러 개를 한꺼번에 열 때 index번 캡슐의 빛이 뜸을 들인다.
    public void ChargeMultiGlow(int index, Vector2 at, float glowScale, float ratio, Color tint)
    {
        if (!_isReady) return;

        Image glow = _multiGlows[index];
        glow.gameObject.SetActive(true);
        glow.rectTransform.anchoredPosition = at;
        glow.rectTransform.localScale = Vector3.one *
                                        (_glowBaseScale * _multiGlowScale * Mathf.Lerp(0.7f, glowScale * 0.8f, ratio));
        SetImageColor(glow, tint, Mathf.Lerp(0.25f, 0.9f, ratio));
    }

    // 여러 개를 한꺼번에 열 때 index번 캡슐의 빛이 터진다. 반짝이는 한 벌을 groups몫으로 나눠 group번째 몫을
    // 쓰고, 몫 안에서는 좋은 결과일수록 많이(twinkles개까지) 쓴다.
    public void BurstMultiGlow(
        int index,
        int group,
        int groups,
        Vector2 at,
        float glowScale,
        int twinkles,
        float ratio,
        Color tint)
    {
        if (!_isReady) return;

        float outward = 1f - (1f - ratio) * (1f - ratio);
        Image glow = _multiGlows[index];
        glow.rectTransform.localScale = Vector3.one *
                                        (_glowBaseScale * _multiGlowScale *
                                         Mathf.Lerp(glowScale * 0.8f, glowScale * 1.25f, outward));
        SetImageColor(glow, tint, 1f - ratio);

        int share = Mathf.Max(1, _twinkles.Length / Mathf.Max(1, groups));
        AnimateTwinkleGroup(group * share, Mathf.Min(share, twinkles), at, ratio, tint);
    }

    public void HideMultiGlow(int index)
    {
        if (_isReady) _multiGlows[index].gameObject.SetActive(false);
    }

    // 티켓이 날아가는 동안 지나간 자리에 반짝이를 남긴다. 시작할 때 한 번 BeginTrail을 부른다.
    public void BeginTrail()
    {
        if (!_isReady) return;

        for (int i = 0; i < _trailBirth.Length; i++)
        {
            _trailBirth[i] = -999f;
        }

        _trailNext = 0;
        _trailNextEmit = 0f;
    }

    // time은 BeginTrail부터 흐른 시간이다. 간격마다 position에 하나씩 남기고, 남은 것들을 가라앉힌다.
    public void Trail(Vector2 position, float time)
    {
        if (!_isReady) return;

        while (time >= _trailNextEmit)
        {
            EmitTrail(position, _trailNextEmit);
            _trailNextEmit += _trailInterval;
        }

        FadeTrail(time);
    }

    // 새로 남기지 않고 이미 남긴 반짝이만 가라앉힌다. 반짝이는 태어난 자리에 남아 천천히 아래로
    // 가라앉으며 작아지고 옅어진다.
    public void FadeTrail(float time)
    {
        if (!_isReady) return;

        for (int i = 0; i < _twinkles.Length; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            float age = time - _trailBirth[i];
            if (age < 0f || age > _trailLife)
            {
                twinkle.gameObject.SetActive(false);
                continue;
            }

            float life = age / _trailLife;
            twinkle.gameObject.SetActive(true);
            twinkle.rectTransform.anchoredPosition = _trailPosition[i] + Vector2.down * (life * 40f);
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.15f, life);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, life * 160f + i * 30f);
            SetImageColor(twinkle, new Color(1f, 0.95f, 0.65f), (1f - life) * 0.9f);
        }
    }

    // 슬라임이 솟아오르는 동안 center에서 빛이 계속 새로 퍼지게 한다. 한 번 터지고 끝나면 허전하다.
    // 하나만 뽑을 때만 쓴다. 여러 개를 뽑을 때는 반짝이를 열리는 캡슐이 쓰고 있다.
    public void Sustain(Vector2 center, float elapsed, Color color)
    {
        if (!_isReady) return;

        int count = _twinkles.Length;
        for (int i = 0; i < count; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            float phase = Mathf.Repeat(elapsed * 1.5f + (float)i / count, 1f);
            float angle = Mathf.PI * 2f * i / count + elapsed * 0.45f;
            float distance = Mathf.Lerp(60f, 320f + i % 3 * 30f, phase);
            float alpha = Mathf.Sin(phase * Mathf.PI);
            twinkle.gameObject.SetActive(true);
            twinkle.rectTransform.anchoredPosition = center +
                                                     new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.1f, alpha);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * 90f + i * 30f);
            SetImageColor(twinkle, Color.Lerp(Color.white, color, 0.6f), alpha * 0.85f);
        }
    }

    private void EmitTrail(Vector2 position, float time)
    {
        int index = _trailNext;
        _trailNext = (_trailNext + 1) % _twinkles.Length;
        _trailBirth[index] = time;
        _trailPosition[index] = position + new Vector2(
            Random.Range(-28f, 28f),
            Random.Range(-28f, 28f));
    }

    private void AnimateTwinkleBurst(Vector2 center, int count, float ratio, Color color)
    {
        int length = _twinkles.Length;
        for (int i = 0; i < length; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            if (i >= count)
            {
                twinkle.gameObject.SetActive(false);
                continue;
            }

            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) + (i % 2) * 0.25f;
            float distance = Mathf.Lerp(90f, 300f + i % 3 * 60f, 1f - (1f - ratio) * (1f - ratio));
            twinkle.gameObject.SetActive(true);
            twinkle.rectTransform.anchoredPosition = center +
                                                     new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.2f, ratio);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ratio * 120f + i * 25f);
            SetImageColor(twinkle, Color.Lerp(Color.white, color, 0.6f), 1f - ratio);
        }
    }

    // 반짝이 중 [start, start + count) 칸만 써서 center를 둘레로 터뜨린다. 한꺼번에 열 때 캡슐마다 제 몫을 쓴다.
    // 캡슐끼리 가까우므로 하나만 열 때보다 짧게 퍼진다.
    private void AnimateTwinkleGroup(int start, int count, Vector2 center, float ratio, Color color)
    {
        int end = Mathf.Min(start + count, _twinkles.Length);
        for (int i = start; i < end; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            int k = i - start;
            float angle = Mathf.PI * 2f * k / Mathf.Max(1, count) + start * 0.6f;
            float distance = Mathf.Lerp(50f, 170f + k % 2 * 30f, 1f - (1f - ratio) * (1f - ratio));
            twinkle.gameObject.SetActive(true);
            twinkle.rectTransform.anchoredPosition = center +
                                                     new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.2f, ratio);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ratio * 120f + i * 25f);
            SetImageColor(twinkle, Color.Lerp(Color.white, color, 0.6f), 1f - ratio);
        }
    }

    private bool IsValidMultiGlows()
    {
        if (_multiGlows == null) return false;

        foreach (Image glow in _multiGlows)
        {
            if (glow == null) return false;
        }

        return true;
    }

    private void HideMultiGlows()
    {
        foreach (Image glow in _multiGlows)
        {
            glow.gameObject.SetActive(false);
        }
    }

    private static void SetImageColor(Image image, Color color, float alpha)
    {
        if (image != null)
        {
            image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }
    }
}
