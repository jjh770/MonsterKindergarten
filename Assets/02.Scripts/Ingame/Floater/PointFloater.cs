using DG.Tweening;
using Lean.Pool;
using TMPro;
using UnityEngine;

public class PointFloater : MonoBehaviour
{
    [SerializeField] private TextMeshPro _text;
    [SerializeField] private float _floatDistance = 1f;

    // 누른 자리에서 바로 띄우면 숫자가 슬라임 얼굴을 가린다. 조금 위에서 시작한다.
    [SerializeField] private float _startOffsetY = 0.4f;
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
        _text.text = $"<sprite name=\"{_gradeIndex:00}\">+{clickInfo.Point}";
        _text.alpha = 1f;

        float startY = clickInfo.Position.y + _startOffsetY;
        transform.position = new Vector3(clickInfo.Position.x, startY, transform.position.z);

        // 위로 떠오르면서 페이드아웃
        transform.DOMoveY(startY + _floatDistance, _duration).SetEase(_floatEase);
        transform.DOMoveX(clickInfo.Position.x + UnityEngine.Random.Range(-_sideDistance, _sideDistance), _duration).SetEase(_floatEase);
        _text.DOFade(0f, _duration).SetEase(_fadeEase).OnComplete(() =>
        {
            _pool.Despawn(gameObject);
        });
    }
}
