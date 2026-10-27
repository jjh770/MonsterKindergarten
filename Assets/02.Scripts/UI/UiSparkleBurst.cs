using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 오버레이 UI 위에서 한 점을 중심으로 반짝이가 사방으로 터지고, 있으면 은은한 빛이 함께 번진다.
// 탐험이 돌아왔을 때와 보상을 받을 때처럼 축하하는 순간에 쓴다.
//
// 움직임은 날아가는 보상의 터짐과 같은 UiBurst가 맡고, 여기서는 이 자리에서 쓸 그림과 크기만
// 정한다. 조각과 빛은 장면에 비활성으로 둔 원본을 복제한 풀에서 빌려 쓰므로, 터지는 도중에 다시
// 불러도 앞의 터짐을 끊지 않고 함께 터진다. 그리는 순서는 _root가 있는 캔버스를 따른다.
public sealed class UiSparkleBurst : MonoBehaviour
{
    [Tooltip("조각을 담을 자리입니다. 터지는 위치는 이 안의 좌표로 바꿔 씁니다.")]
    [SerializeField] private RectTransform _root;
    [Tooltip("반짝이 조각의 원본입니다. 비활성으로 두고, 그림·색·크기를 조각에 씁니다.")]
    [SerializeField] private Image _sparkleTemplate;
    [Tooltip("가운데서 번지는 빛의 그림·색·크기를 읽는 원본입니다. 비활성으로 둡니다. 비워 두면 반짝이만 터집니다.")]
    [SerializeField] private Image _flash;

    [SerializeField, Range(3, 24)] private int _sparkleCount = 10;
    [SerializeField, Min(0f)] private float _radius = 220f;
    [SerializeField, Min(0.1f)] private float _duration = 0.7f;
    [SerializeField] private Vector2 _sizeRange = new(0.6f, 1.1f);
    [SerializeField, Min(0f)] private float _flashScale = 2.2f;

    private readonly List<Sequence> _playing = new();
    private UiImagePool _pool;
    private UiBurstStyle _style;

    private void Awake()
    {
        if (_root == null || _sparkleTemplate == null)
        {
            Debug.LogError("반짝이 터짐의 필수 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }

        // 조각 색은 원본의 색을 따른다. 밝은 바탕 위에서는 원본을 금색으로 칠해 두면 잘 보인다.
        _sparkleTemplate.gameObject.SetActive(false);
        _style = new UiBurstStyle
        {
            PieceSprite = _sparkleTemplate.sprite,
            PieceColor = _sparkleTemplate.color,
            PieceSize = _sparkleTemplate.rectTransform.sizeDelta,
            Count = _sparkleCount,
            Radius = _radius,
            Duration = _duration,
            ScaleRange = _sizeRange,
            Rotate = true,
        };

        if (_flash != null)
        {
            _flash.gameObject.SetActive(false);
            _style.FlashSprite = _flash.sprite;
            _style.FlashColor = _flash.color;
            _style.FlashSize = _flash.rectTransform.sizeDelta;
            _style.FlashScale = _flashScale;
        }

        _pool = new UiImagePool(_sparkleTemplate, _root, _sparkleCount + 1);
    }

    private void OnDisable() => Stop();

    // worldPosition은 오버레이 UI의 위치(화면 좌표와 같다)다.
    public void Play(Vector3 worldPosition)
    {
        if (!enabled) return;

        Vector3 center = _root.InverseTransformPoint(worldPosition);
        Sequence sequence = UiBurst.Play(_pool, center, _style, gameObject);
        if (sequence == null) return;

        _playing.Add(sequence);
        sequence.SetUpdate(true).OnComplete(() => _playing.Remove(sequence));
    }

    private void Stop()
    {
        // Kill이 조각을 풀로 돌려준다. 목록을 먼저 비워 완료 콜백과 엇갈리지 않게 한다.
        var playing = _playing.ToArray();
        _playing.Clear();
        foreach (Sequence sequence in playing)
        {
            sequence?.Kill();
        }
    }
}
