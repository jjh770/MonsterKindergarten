using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 탐험 보상의 포인트와 뽑기권 아이콘이 상단 바로 날아가는 연출이다. 보상은 이미 지급돼 있고,
// 숫자와 저장은 건드리지 않는다.
//
// 상단 바는 탐험 화면이 열린 동안 숨겨지므로 화면을 닫은 뒤에 부른다. 그래서 탐험 화면의 캔버스가
// 아니라 상단 바보다 위인 공용 팝업 캔버스(10)에 둔다. 두 캔버스 모두 Screen Space Overlay라
// 팝업에서 읽은 화면 위치를 그대로 출발점으로 쓴다.
public sealed class ExpeditionRewardFlight : MonoBehaviour
{
    [Tooltip("복제해 쓰는 비활성 Image 원본입니다.")]
    [SerializeField] private Image _template;
    [SerializeField] private RectTransform _layer;
    [SerializeField] private RectTransform _pointTarget;
    [SerializeField] private RectTransform _ticketTarget;
    [SerializeField] private Sprite _pointSprite;
    [SerializeField] private Sprite _ticketSprite;
    [SerializeField, Min(1)] private int _pointCount = 12;
    [SerializeField, Min(1)] private int _maxTicketCount = 8;
    [SerializeField, Min(1f)] private float _iconSize = 90f;
    [SerializeField] private Vector2 _scatterDistance = new(160f, 110f);
    [SerializeField, Min(0.01f)] private float _scatterDuration = 0.2f;
    [SerializeField, Min(0.05f)] private float _flyDuration = 0.6f;
    [SerializeField, Min(0f)] private float _spawnInterval = 0.04f;
    [SerializeField, Range(0f, 0.6f)] private float _flyArc = 0.3f;

    private readonly List<Image> _flying = new();
    private UiImagePool _pool;
    private Sequence _sequence;

    private void Awake()
    {
        if (_template == null || _layer == null || _pointTarget == null || _ticketTarget == null ||
            _pointSprite == null || _ticketSprite == null)
        {
            Debug.LogError("탐험 보상 날아가기의 필수 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }

        _pool = new UiImagePool(_template, _layer, _pointCount + _maxTicketCount);
    }

    private void OnDestroy() => _sequence?.Kill();

    public void Play(Vector3 pointFrom, Vector3 ticketFrom, int tickets)
    {
        if (!enabled || _pool == null) return;

        Clear();
        _sequence = DOTween.Sequence().SetUpdate(true);
        Add(_pointCount, _pointSprite, pointFrom, _pointTarget, EAudioSfx.OfflineRewardArrival);
        if (tickets > 0)
            Add(Mathf.Min(tickets, _maxTicketCount), _ticketSprite, ticketFrom, _ticketTarget, EAudioSfx.TicketCollect);
        _sequence.OnComplete(() => { _sequence = null; Clear(); });
    }

    private void Add(int count, Sprite sprite, Vector3 from, RectTransform target, EAudioSfx arrivalSound)
    {
        for (int i = 0; i < count; i++)
        {
            Image image = _pool.Rent();
            _flying.Add(image);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(_iconSize, _iconSize);
            rect.localScale = Vector3.one;
            rect.position = from;

            Vector3 scatter = from + _layer.TransformVector(new Vector3(
                Random.Range(-_scatterDistance.x, _scatterDistance.x),
                Random.Range(-_scatterDistance.y, _scatterDistance.y)));
            Vector3 control = GetControlPoint(scatter, target.position);
            float duration = _flyDuration * Random.Range(.85f, 1.15f);
            float start = i * _spawnInterval;
            bool first = i == 0;

            Sequence flight = DOTween.Sequence();
            flight.Append(rect.DOMove(scatter, _scatterDuration).SetEase(Ease.OutQuad));
            flight.Append(DOVirtual.Float(0f, 1f, duration, t =>
            {
                float u = 1f - t;
                // 날아가는 동안 상단 바가 움직여도 지금 자리로 간다.
                rect.position = u * u * scatter + 2f * u * t * control + t * t * target.position;
            }).SetEase(Ease.InQuad));
            flight.Join(rect.DOScale(.55f, duration).SetEase(Ease.InQuad));
            flight.AppendCallback(() =>
            {
                image.gameObject.SetActive(false);
                target.DOKill(complete: true);
                target.DOPunchScale(Vector3.one * .12f, .3f, 6, .5f).SetUpdate(true);
                if (first) AudioManager.Instance?.PlaySFX(arrivalSound);
                else AudioManager.Instance?.PlaySFXRandomPitchWithCooldown(EAudioSfx.TicketCollect, .06f);
            });
            _sequence.Insert(start, flight);
        }
    }

    // 시작점과 목표 사이 가운데에서 옆으로 비켜선 점이다. 아이콘마다 좌우가 달라 한 줄로 쏠리지 않는다.
    private Vector3 GetControlPoint(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        Vector3 perpendicular = new(-delta.y, delta.x, 0f);
        float side = Random.value < .5f ? -1f : 1f;
        return (from + to) * .5f + perpendicular * (_flyArc * Random.Range(.6f, 1.2f) * side);
    }

    private void Clear()
    {
        _sequence?.Kill();
        _sequence = null;
        foreach (Image image in _flying) _pool.Return(image);
        _flying.Clear();
    }
}
