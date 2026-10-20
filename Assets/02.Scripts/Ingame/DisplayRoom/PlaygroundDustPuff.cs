using DG.Tweening;
using UnityEngine;

// 소품이 바닥에 떨어질 때 발치에서 퍼지는 먼지 한 번이다. 프리팹에 미리 둔 먼지 조각들이
// 양옆으로 낮게 퍼지며 커지다가 흐려지고, 다 끝나면 스스로 사라진다.
public sealed class PlaygroundDustPuff : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _puffs = System.Array.Empty<SpriteRenderer>();
    [Tooltip("먼지가 옆으로 퍼지는 거리입니다(월드 단위).")]
    [SerializeField, Min(0f)] private float _spread = 0.55f;
    [Tooltip("퍼지면서 살짝 떠오르는 높이입니다(월드 단위).")]
    [SerializeField, Min(0f)] private float _rise = 0.12f;
    [SerializeField, Min(0.05f)] private float _duration = 0.45f;
    [Tooltip("다 퍼졌을 때 처음 크기의 몇 배가 될지입니다.")]
    [SerializeField, Min(1f)] private float _growth = 1.8f;

    private void Start()
    {
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        for (int i = 0; i < _puffs.Length; i++)
        {
            SpriteRenderer puff = _puffs[i];
            if (puff == null) continue;

            // 바닥을 따라 퍼지도록 좌우로 고르게 나누고, 위아래는 조금만 흔든다.
            float side = _puffs.Length == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(_puffs.Length - 1));
            Vector3 start = puff.transform.localPosition;
            Vector3 end = start + new Vector3(side * _spread * Random.Range(.8f, 1.1f),
                _rise * Random.Range(.5f, 1f), 0f);
            Vector3 scale = puff.transform.localScale;
            float delay = Random.Range(0f, .05f);

            sequence.Insert(delay, puff.transform.DOLocalMove(end, _duration).SetEase(Ease.OutCubic));
            sequence.Insert(delay, puff.transform.DOScale(scale * _growth, _duration).SetEase(Ease.OutQuad));
            sequence.Insert(delay + _duration * .35f, puff.DOFade(0f, _duration * .65f).SetEase(Ease.InQuad));
        }

        sequence.OnComplete(() => Destroy(gameObject));
    }
}
