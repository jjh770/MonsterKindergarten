using DG.Tweening;
using TMPro;
using UnityEngine;

public class PointFloater : MonoBehaviour
{
    [SerializeField] private TextMeshPro _text;
    [SerializeField] private float _floatDistance = 1f;
    [SerializeField] private float _duration = 0.8f;

    private PointFloaterPool _pool;

    public void SetPool(PointFloaterPool pool)
    {
        _pool = pool;
    }

    public void Play(int point, Vector3 position)
    {
        transform.position = position;
        _text.text = point.ToString();
        _text.alpha = 1f;

        // 위로 떠오르면서 페이드아웃
        transform.DOMoveY(position.y + _floatDistance, _duration).SetEase(Ease.OutCubic);
        _text.DOFade(0f, _duration).SetEase(Ease.InQuad).OnComplete(() =>
        {
            if (_pool != null)
            {
                _pool.Despawn(this);
            }
            else
            {
                Debug.Log("Pool이 없습니다.");
                //Destroy(gameObject);
            }
        });
    }
}
