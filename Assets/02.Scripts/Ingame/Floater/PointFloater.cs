using DG.Tweening;
using Lean.Pool;
using TMPro;
using UnityEngine;

public class PointFloater : MonoBehaviour
{
    [SerializeField] private TextMeshPro _text;
    [SerializeField] private float _floatDistance = 1f;
    [SerializeField] private float _sideDistance = 0.3f;
    [SerializeField] private float _duration = 0.8f;
    [SerializeField] private Ease _floatEase;
    [SerializeField] private Ease _fadeEase;

    private LeanGameObjectPool _pool;
    private int _gradeIndex;
    public void SetPool(LeanGameObjectPool pool)
    {
        _pool = pool;
    }

    public void Play(ClickInfo clickInfo)
    {
        _gradeIndex = (int)clickInfo.Grade;
        _text.text = $"<sprite={_gradeIndex}>{clickInfo.Point}";
        _text.alpha = 1f;

        // 위로 떠오르면서 페이드아웃
        transform.DOMoveY(clickInfo.Position.y + _floatDistance, _duration).SetEase(_floatEase);
        transform.DOMoveX(clickInfo.Position.x + UnityEngine.Random.Range(-_sideDistance, _sideDistance), _duration).SetEase(_floatEase);
        _text.DOFade(0f, _duration).SetEase(_fadeEase).OnComplete(() =>
        {
            _pool.Despawn(gameObject);
        });
    }
}
