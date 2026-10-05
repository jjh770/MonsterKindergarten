using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 로그인 화면의 연출을 맡는다. 로고가 내려앉는 순간의 반응, 로고의 숨쉬기, 떠오르는 별, 그리고
// 화면을 눌렀을 때의 크게 터지는 반응이다.
//
// 언제 시작할지는 LoginScene이 정한다. 로고가 다 내려오면 PlayLanding, 버튼을 누르면 PlayPress를 부른다.
// 별 조각은 씬에 미리 만들어 둔 이미지를 돌려 쓴다. 화면 가득 새로 만들지 않고, 같은 조각이 떠올랐다가
// 사라지면 다시 다른 자리에서 떠오른다. 오버레이 캔버스에는 ParticleSystem이 그려지지 않아서
// 터치 효과와 같이 UI 이미지와 DOTween으로 움직인다.
public sealed class LoginSceneEffects : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("별이 움직이는 화면 크기의 영역입니다. 가운데 기준으로 늘어난 RectTransform이어야 합니다.")]
    [SerializeField] private RectTransform _root;
    [Tooltip("내려앉는 로고입니다. 크기를 눌렀다 펴고 숨쉬게 합니다.")]
    [SerializeField] private RectTransform _title;
    [Tooltip("퍼지는 원 하나입니다. 씬에서는 꺼 두고 로고 뒤에 둡니다.")]
    [SerializeField] private Image _ring;
    [Tooltip("떠오르는 별 이미지들입니다. 씬에서는 꺼 두고 로고 뒤에 둡니다.")]
    [SerializeField] private Image[] _sparkles;
    [SerializeField] private LoginButtonTextPulse _labelPulse;

    [Header("Landing")]
    [Tooltip("로고가 땅에 닿는 순간 눌리는 비율입니다. x는 넓어지고 y는 낮아집니다.")]
    [SerializeField] private Vector2 _squashScale = new Vector2(1.14f, 0.84f);
    [SerializeField, Min(0.05f)] private float _squashSeconds = 0.1f;
    [SerializeField, Min(0.1f)] private float _reboundSeconds = 0.55f;
    [SerializeField, Min(0.1f)] private float _landingRingScale = 2.2f;
    [SerializeField, Min(0.1f)] private float _landingRingSeconds = 0.6f;

    [Header("Breathing")]
    [Tooltip("로고가 숨쉬듯 부풀었다 줄어드는 최대 크기입니다. 1이면 움직이지 않습니다.")]
    [SerializeField, Range(1f, 1.1f)] private float _breathScale = 1.025f;
    [SerializeField, Min(0.2f)] private float _breathSeconds = 1.8f;

    [Header("Sparkles")]
    [SerializeField] private Color _sparkleColor = new Color(1f, 0.88f, 0.4f, 0.95f);
    [SerializeField] private Vector2 _sparkleSize = new Vector2(22f, 48f);
    [Tooltip("한 번 떠올랐다 사라지는 데 걸리는 시간 범위입니다.")]
    [SerializeField] private Vector2 _sparkleLife = new Vector2(2.4f, 4.2f);
    [Tooltip("떠오르는 높이 범위입니다(캔버스 단위).")]
    [SerializeField] private Vector2 _sparkleRise = new Vector2(220f, 520f);

    [Header("Exit")]
    [Tooltip("게임으로 넘어가기 전에 로고가 위로 사라지는 데 걸리는 시간입니다. 장면 전환은 이 시간의 일부가 지나면 시작합니다.")]
    [SerializeField, Min(0.1f)] private float _exitSeconds = 0.6f;
    [SerializeField] private float _exitRise = 220f;

    [Header("Press")]
    [SerializeField, Min(1f)] private float _pressTitleScale = 1.1f;
    [SerializeField, Min(0.1f)] private float _pressRingScale = 4.5f;
    [SerializeField, Min(0.1f)] private float _pressRingSeconds = 0.7f;
    [SerializeField] private Vector2 _burstDistance = new Vector2(320f, 760f);
    [SerializeField] private Vector2 _burstSeconds = new Vector2(0.55f, 0.95f);

    private Tween[] _sparkleTweens;
    private Tween _titleTween;
    private Tween _ringTween;
    private Vector3 _titleBaseScale;
    private Vector3 _ringBaseScale;
    private Color _ringBaseColor;
    private bool _isReady;

    private void Awake()
    {
        if (_root == null || _title == null || _ring == null || _sparkles == null)
        {
            Debug.LogError("로그인 연출의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _titleBaseScale = _title.localScale;
        _ringBaseScale = _ring.rectTransform.localScale;
        _ringBaseColor = _ring.color;
        _sparkleTweens = new Tween[_sparkles.Length];
        _isReady = true;
    }

    private void OnDestroy()
    {
        _titleTween?.Kill();
        _ringTween?.Kill();

        if (_sparkleTweens == null) return;

        foreach (Tween tween in _sparkleTweens)
        {
            tween?.Kill();
        }
    }

    // 로고가 다 내려온 순간이다. 눌렸다가 튀어 오르고, 원이 퍼지고, 별이 떠오르기 시작한다.
    public void PlayLanding()
    {
        if (!_isReady) return;

        PlayRing(GetTitleCenter(), _landingRingScale, _landingRingSeconds);

        _titleTween?.Kill();
        Sequence landing = DOTween.Sequence();
        landing.Append(_title
            .DOScale(Scale(_squashScale.x, _squashScale.y), _squashSeconds)
            .SetEase(Ease.OutQuad));
        landing.Append(_title
            .DOScale(_titleBaseScale, _reboundSeconds)
            .SetEase(Ease.OutElastic));
        landing.OnComplete(StartBreathing);
        _titleTween = landing;

        for (int i = 0; i < _sparkles.Length; i++)
        {
            StartSparkle(i, Random.Range(0f, _sparkleLife.y));
        }
    }

    // 버튼을 눌렀을 때 호출한다. 문구가 튀고, 로고가 부풀고, 로고 가운데에서 원이, 누른 자리에서 별이 터진다.
    public void PlayPress(Vector2 screenPoint)
    {
        if (!_isReady) return;

        _labelPulse?.PlayPress();
        PlayRing(GetTitleCenter(), _pressRingScale, _pressRingSeconds);

        _titleTween?.Kill();
        Sequence punch = DOTween.Sequence();
        punch.Append(_title
            .DOScale(Scale(_pressTitleScale, _pressTitleScale), 0.12f)
            .SetEase(Ease.OutQuad));
        punch.Append(_title
            .DOScale(_titleBaseScale, 0.35f)
            .SetEase(Ease.OutBack));
        punch.OnComplete(StartBreathing);
        _titleTween = punch;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _root, screenPoint, null, out Vector2 local);
        for (int i = 0; i < _sparkles.Length; i++)
        {
            Burst(i, local, i / (float)_sparkles.Length);
        }
    }

    // 원은 늘 로고의 한가운데에서 터진다. 피벗이 아니라 그림이 차지한 사각형의 가운데를 쓴다.
    private Vector3 GetTitleCenter()
    {
        return _title.TransformPoint(_title.rect.center);
    }

    // 게임 씬으로 넘어가기 전의 퇴장이다. 문구가 지워지고, 별이 사라지고, 로고가 한 번 부풀었다가
    // 위로 떠오르며 사라진다. 이어서 화면을 덮으면 누른 반응에서 곧바로 장면 전환으로 이어진다.
    // 연출이 끝나는 시간을 돌려준다.
    public float PlayExit()
    {
        if (!_isReady) return 0f;

        _labelPulse?.PlayExit(_exitSeconds * 0.5f);

        foreach (Image piece in _sparkles)
        {
            piece.DOFade(0f, _exitSeconds * 0.5f);
        }

        _titleTween?.Kill();
        Image titleImage = _title.GetComponent<Image>();
        Sequence exit = DOTween.Sequence();
        exit.Append(_title
            .DOScale(Scale(1.07f, 1.07f), 0.15f)
            .SetEase(Ease.OutQuad));
        exit.Insert(0.1f, _title
            .DOAnchorPosY(_title.anchoredPosition.y + _exitRise, _exitSeconds - 0.1f)
            .SetEase(Ease.InQuad));
        if (titleImage != null)
        {
            exit.Insert(_exitSeconds * 0.35f, titleImage.DOFade(0f, _exitSeconds * 0.65f));
        }

        _titleTween = exit;
        return _exitSeconds;
    }

    private Vector3 Scale(float x, float y)
    {
        return new Vector3(_titleBaseScale.x * x, _titleBaseScale.y * y, _titleBaseScale.z);
    }

    private void StartBreathing()
    {
        _titleTween = null;
        if (_breathScale <= 1f) return;

        _titleTween = _title
            .DOScale(_titleBaseScale * _breathScale, _breathSeconds)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void PlayRing(Vector3 worldPosition, float endScale, float seconds)
    {
        _ringTween?.Kill();

        RectTransform ring = _ring.rectTransform;
        ring.position = worldPosition;
        ring.localScale = _ringBaseScale * 0.25f;
        _ring.color = _ringBaseColor;
        _ring.gameObject.SetActive(true);

        _ringTween = DOVirtual.Float(0f, 1f, seconds, t =>
        {
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            ring.localScale = _ringBaseScale * Mathf.Lerp(0.25f, endScale, eased);
            Color color = _ringBaseColor;
            color.a *= Mathf.Pow(1f - t, 1.5f);
            _ring.color = color;
        }).SetEase(Ease.Linear).OnComplete(() =>
        {
            _ringTween = null;
            _ring.gameObject.SetActive(false);
        });
    }

    // 한 조각이 화면의 임의 자리에서 떠올라 흔들리며 사라진다. 끝나면 다른 자리에서 다시 시작한다.
    private void StartSparkle(int index, float delay)
    {
        Image piece = _sparkles[index];
        Rect area = _root.rect;
        RectTransform rect = piece.rectTransform;

        Vector2 start = new Vector2(
            Random.Range(area.xMin, area.xMax),
            Random.Range(area.yMin + area.height * 0.1f, area.yMin + area.height * 0.75f));
        float rise = Random.Range(_sparkleRise.x, _sparkleRise.y);
        float sway = Random.Range(18f, 60f);
        float phase = Random.value * Mathf.PI * 2f;
        float spin = Random.Range(-120f, 120f);
        float life = Random.Range(_sparkleLife.x, _sparkleLife.y);

        rect.sizeDelta = Vector2.one * Random.Range(_sparkleSize.x, _sparkleSize.y);
        rect.anchoredPosition = start;
        SetSparkleAlpha(piece, 0f);
        piece.gameObject.SetActive(true);

        _sparkleTweens[index]?.Kill();
        _sparkleTweens[index] = DOVirtual.Float(0f, 1f, life, t =>
        {
            float fade = Mathf.Sin(t * Mathf.PI);
            float twinkle = 0.8f + 0.2f * Mathf.Sin(t * 22f + phase);
            rect.anchoredPosition = start + new Vector2(Mathf.Sin(phase + t * 4f) * sway, rise * t);
            rect.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, fade);
            rect.localRotation = Quaternion.Euler(0f, 0f, spin * t);
            SetSparkleAlpha(piece, fade * twinkle);
        }).SetDelay(delay).SetEase(Ease.Linear).OnComplete(() =>
        {
            StartSparkle(index, Random.Range(0f, 0.6f));
        });
    }

    // 누른 자리에서 모든 조각이 바깥으로 흩어진다. 끝나면 평소처럼 다시 떠오른다.
    private void Burst(int index, Vector2 origin, float ratio)
    {
        Image piece = _sparkles[index];
        RectTransform rect = piece.rectTransform;

        float angle = (ratio + Random.Range(-0.03f, 0.03f)) * Mathf.PI * 2f;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float distance = Random.Range(_burstDistance.x, _burstDistance.y);
        float seconds = Random.Range(_burstSeconds.x, _burstSeconds.y);
        float spin = Random.Range(-360f, 360f);

        rect.sizeDelta = Vector2.one * Random.Range(_sparkleSize.y, _sparkleSize.y * 1.5f);
        rect.anchoredPosition = origin;
        SetSparkleAlpha(piece, 1f);
        piece.gameObject.SetActive(true);

        _sparkleTweens[index]?.Kill();
        _sparkleTweens[index] = DOVirtual.Float(0f, 1f, seconds, t =>
        {
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            rect.anchoredPosition = origin + direction * (distance * eased);
            rect.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.3f, t);
            rect.localRotation = Quaternion.Euler(0f, 0f, spin * t);
            SetSparkleAlpha(piece, 1f - t * t);
        }).SetEase(Ease.Linear).OnComplete(() =>
        {
            StartSparkle(index, Random.Range(0.4f, 1.4f));
        });
    }

    private void SetSparkleAlpha(Image piece, float alpha)
    {
        Color color = _sparkleColor;
        color.a *= Mathf.Clamp01(alpha);
        piece.color = color;
    }
}
