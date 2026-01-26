using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamageFloater : MonoBehaviour
{
    [SerializeField] private TextMeshPro _text;
    [SerializeField] private float _floatDistance = 1f;
    [SerializeField] private float _duration = 0.8f;

    public void Play(int damage, Vector3 position)
    {
        transform.position = position;
        _text.text = damage.ToString();
        _text.alpha = 1f;

        // 위로 떠오르면서 페이드아웃
        transform.DOMoveY(position.y + _floatDistance, _duration).SetEase(Ease.OutCubic);
        _text.DOFade(0f, _duration).SetEase(Ease.InQuad).OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }
}
