using DG.Tweening;
using UnityEngine;

public class ScaleTweeningFeedback : MonoBehaviour, IFeedback
{
    [Header("Click")]
    [SerializeField, Min(0f)] private float _clickPunchScale = 0.5f;
    [SerializeField, Min(0f)] private float _clickDuration = 0.5f;

    [Header("Promote")]
    [SerializeField, Min(0f)] private float _promotePunchScale = 1f;
    [SerializeField, Min(0f)] private float _promoteDuration = 1f;

    [Header("Common")]
    [SerializeField, Min(1)] private int _vibrato = 10;
    [SerializeField, Range(0f, 1f)] private float _elasticity = 1f;

    private SlimeController _owner;
    private Tween _scaleTween;
    private Vector3 _defaultScale;

    private void Awake()
    {
        _owner = GetComponent<SlimeController>();
        _defaultScale = transform.localScale;
    }

    private void OnEnable()
    {
        _owner.OnPromoted += PlayPromoteFeedback;
    }

    // 역할 : 스케일 트위닝 피드백에 대한 로직을 담당
    public void Play(ClickInfo clickInfo)
    {
        PlayPunch(_clickPunchScale, _clickDuration);
    }

    private void OnDisable()
    {
        if (_owner != null)
        {
            _owner.OnPromoted -= PlayPromoteFeedback;
        }

        // 비활성화 시 Tween 정리 (오브젝트 풀링 대응)
        CleanupTween();
    }

    private void OnDestroy()
    {
        // 파괴 시에도 안전하게 정리
        CleanupTween();
    }

    private void PlayPromoteFeedback()
    {
        PlayPunch(_promotePunchScale, _promoteDuration);
    }

    private void PlayPunch(float punchScale, float duration)
    {
        CleanupTween();
        if (_owner == null) return;

        _scaleTween = _owner.transform
            .DOPunchScale(
                Vector3.one * punchScale,
                duration,
                _vibrato,
                _elasticity)
            .OnComplete(CompleteTween);
    }

    private void CompleteTween()
    {
        ResetScale();
        _scaleTween = null;
    }

    private void CleanupTween()
    {
        _scaleTween?.Kill();
        _scaleTween = null;
        ResetScale();
    }

    private void ResetScale()
    {
        if (_owner != null)
        {
            _owner.transform.localScale = _defaultScale;
        }
    }
}
