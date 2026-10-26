using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 오버레이 UI 위에서 한 점을 중심으로 반짝이가 사방으로 터지고, 있으면 은은한 빛이 함께 번진다.
// 탐험이 돌아왔을 때와 보상을 받을 때처럼 축하하는 순간에 쓴다.
//
// 반짝이 조각은 장면에 비활성으로 둔 원본 하나를 깨어날 때 복제해 두고 돌려 쓴다. 오버레이 캔버스는
// 파티클을 그리지 않으므로 UpgradeTouchBurst처럼 Image를 DOTween으로 움직인다. 한 번에 하나만
// 터지고, 터지는 도중에 다시 부르면 처음부터 다시 터진다.
public sealed class UiSparkleBurst : MonoBehaviour
{
    [Tooltip("조각을 담을 자리입니다. 터지는 위치는 이 안의 좌표로 바꿔 씁니다.")]
    [SerializeField] private RectTransform _root;
    [Tooltip("반짝이 조각의 원본입니다. 비활성으로 둡니다.")]
    [SerializeField] private Image _sparkleTemplate;
    [Tooltip("가운데서 번지는 빛입니다. 비워 두면 반짝이만 터집니다.")]
    [SerializeField] private Image _flash;

    [SerializeField, Range(3, 24)] private int _sparkleCount = 10;
    [SerializeField, Min(0f)] private float _radius = 220f;
    [SerializeField, Min(0.1f)] private float _duration = 0.7f;
    [SerializeField] private Vector2 _sizeRange = new(0.6f, 1.1f);
    [SerializeField, Min(0f)] private float _flashScale = 2.2f;

    private readonly List<Image> _sparkles = new();
    private Sequence _sequence;
    private Color _flashColor;
    private Color _sparkleColor;

    private void Awake()
    {
        if (_root == null || _sparkleTemplate == null)
        {
            Debug.LogError("반짝이 터짐의 필수 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }

        // 조각 색은 원본의 색을 따른다. 밝은 바탕 위에서는 원본을 금색으로 칠해 두면 잘 보인다.
        _sparkleColor = _sparkleTemplate.color;
        _sparkleTemplate.gameObject.SetActive(false);
        for (int i = 0; i < _sparkleCount; i++)
        {
            Image sparkle = Instantiate(_sparkleTemplate, _root);
            sparkle.raycastTarget = false;
            _sparkles.Add(sparkle);
        }

        if (_flash != null)
        {
            _flashColor = _flash.color;
            _flash.raycastTarget = false;
            _flash.gameObject.SetActive(false);
        }
    }

    private void OnDisable() => Stop();

    // worldPosition은 오버레이 UI의 위치(화면 좌표와 같다)다.
    public void Play(Vector3 worldPosition)
    {
        if (!enabled) return;

        Stop();
        Vector3 center = _root.InverseTransformPoint(worldPosition);
        center.z = 0f;
        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _sequence.OnComplete(() => _sequence = null);

        if (_flash != null)
        {
            RectTransform flash = _flash.rectTransform;
            flash.localPosition = center;
            flash.localScale = Vector3.one * .3f;
            _flash.color = _flashColor;
            _flash.gameObject.SetActive(true);
            _sequence.Insert(0f, flash.DOScale(Vector3.one * _flashScale, _duration * .6f).SetEase(Ease.OutCubic));
            _sequence.Insert(_duration * .15f, _flash.DOFade(0f, _duration * .55f).SetEase(Ease.InQuad));
            _sequence.InsertCallback(_duration * .7f, () => _flash.gameObject.SetActive(false));
        }

        for (int i = 0; i < _sparkles.Count; i++)
        {
            Image sparkle = _sparkles[i];
            RectTransform rect = sparkle.rectTransform;
            float angle = (i + Random.Range(-.35f, .35f)) * Mathf.PI * 2f / _sparkles.Count;
            float distance = _radius * Random.Range(.55f, 1f);
            Vector3 target = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
            float size = Random.Range(_sizeRange.x, _sizeRange.y);
            float delay = Random.Range(0f, .08f);
            rect.localPosition = center;
            rect.localScale = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            sparkle.color = _sparkleColor;
            sparkle.gameObject.SetActive(true);
            _sequence.Insert(delay, rect.DOLocalMove(target, _duration).SetEase(Ease.OutCubic));
            _sequence.Insert(delay, rect.DOScale(Vector3.one * size, _duration * .3f).SetEase(Ease.OutBack));
            _sequence.Insert(delay, rect.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-200f, 200f)), _duration, RotateMode.FastBeyond360));
            _sequence.Insert(delay + _duration * .55f, sparkle.DOFade(0f, _duration * .45f));
            _sequence.InsertCallback(delay + _duration, () => sparkle.gameObject.SetActive(false));
        }
    }

    private void Stop()
    {
        _sequence?.Kill();
        _sequence = null;
        foreach (Image sparkle in _sparkles)
        {
            if (sparkle != null) sparkle.gameObject.SetActive(false);
        }

        if (_flash != null) _flash.gameObject.SetActive(false);
    }
}
