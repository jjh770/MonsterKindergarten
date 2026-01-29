using DG.Tweening;
using UnityEngine;

public class BackgroundMove : MonoBehaviour
{
    [SerializeField] private Sprite[] _backgrounds;
    [SerializeField] private SpriteRenderer _background;

    [SerializeField] private float _endPosX = -7f;
    [SerializeField] private float _duration = 40f;

    private void Start()
    {
        if (_backgrounds == null || _backgrounds.Length == 0) return;
        _background.sprite = _backgrounds[UnityEngine.Random.Range(0, _backgrounds.Length)];
        transform.DOMoveX(_endPosX, _duration).SetEase(Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
    }
}
