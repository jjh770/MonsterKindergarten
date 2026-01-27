using DG.Tweening;
using UnityEngine;

public class BackgroundMove : MonoBehaviour
{
    [SerializeField] private Sprite[] _backgrounds;
    [SerializeField] private SpriteRenderer _background;
    private void Start()
    {
        _background.sprite = _backgrounds[UnityEngine.Random.Range(0, _backgrounds.Length)];
        transform.DOMoveX(-7f, 40f).SetEase(Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
    }
}
