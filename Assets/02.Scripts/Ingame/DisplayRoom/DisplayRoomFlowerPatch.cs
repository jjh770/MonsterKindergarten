using System;
using DG.Tweening;
using UnityEngine;

// 꽃밭. 지나가던 슬라임이 잠깐 멈춰 꽃향기를 맡는다. 머리 위에 하트가 뜨고, 꽃이 살랑이며
// 꽃잎 몇 장이 흩날린다. 슬라임은 제자리에서 움찔거리기만 하고 자리를 옮기지 않는다.
public sealed class DisplayRoomFlowerPatch : PlaygroundDecoration
{
    [SerializeField] private Transform[] _flowers = Array.Empty<Transform>();
    [Tooltip("흩날릴 꽃잎입니다. 평소에는 투명하게 꽃 사이에 숨어 있습니다.")]
    [SerializeField] private SpriteRenderer[] _petals = Array.Empty<SpriteRenderer>();
    [SerializeField] private PlaygroundSlimeAttachment _heart;
    [SerializeField, Min(0.3f)] private float _sniffDuration = 1.6f;
    [SerializeField, Range(0f, 30f)] private float _swayAngle = 12f;
    [SerializeField, Min(0f)] private float _petalRise = 0.6f;

    private Vector3[] _petalHomes;
    private Quaternion[] _flowerRotations;

    protected override void Awake()
    {
        base.Awake();
        if (!enabled) return;

        if (_heart == null)
        {
            Debug.LogError("꽃밭의 하트 표시가 비어 있어요.", this);
            enabled = false;
            return;
        }

        _petalHomes = new Vector3[_petals.Length];
        for (int i = 0; i < _petals.Length; i++)
            if (_petals[i] != null) _petalHomes[i] = _petals[i].transform.localPosition;
        _flowerRotations = new Quaternion[_flowers.Length];
        for (int i = 0; i < _flowers.Length; i++)
            if (_flowers[i] != null) _flowerRotations[i] = _flowers[i].localRotation;
        ResetVisuals();
    }

    protected override void Build(SlimeController slime, Sequence sequence)
    {
        Transform body = slime.transform;
        Vector3 scale = Hold.BaseScale;
        PlaySound(EAudioSfx.SlimeReaction);
        _heart.Follow(slime, _sniffDuration + 0.4f);

        // 킁킁: 두 번 움찔한다.
        sequence.Append(body.DOScale(new Vector3(scale.x * 1.06f, scale.y * .92f, scale.z), .16f).SetEase(Ease.OutQuad));
        sequence.Append(body.DOScale(scale, .16f).SetEase(Ease.InQuad));
        sequence.Append(body.DOScale(new Vector3(scale.x * 1.06f, scale.y * .92f, scale.z), .16f).SetEase(Ease.OutQuad));
        sequence.Append(body.DOScale(scale, .16f).SetEase(Ease.InQuad));
        sequence.AppendInterval(Mathf.Max(0f, _sniffDuration - .64f));

        for (int i = 0; i < _flowers.Length; i++)
        {
            if (_flowers[i] == null) continue;
            float angle = (i % 2 == 0 ? 1f : -1f) * _swayAngle;
            sequence.Insert(i * .05f, _flowers[i].DOPunchRotation(new Vector3(0f, 0f, angle), .9f, 4, .6f));
        }

        for (int i = 0; i < _petals.Length; i++)
        {
            SpriteRenderer petal = _petals[i];
            if (petal == null) continue;

            float start = .1f + i * .12f;
            Vector3 home = _petalHomes[i];
            Vector3 to = home + new Vector3(UnityEngine.Random.Range(-.5f, .5f), _petalRise * UnityEngine.Random.Range(.7f, 1.2f), 0f);
            sequence.Insert(start, petal.DOFade(1f, .15f));
            sequence.Insert(start, petal.transform.DOLocalMove(to, 1.1f).SetEase(Ease.OutQuad));
            sequence.Insert(start, petal.transform.DOLocalRotate(new Vector3(0f, 0f, UnityEngine.Random.Range(-200f, 200f)), 1.1f, RotateMode.FastBeyond360));
            sequence.Insert(start + .75f, petal.DOFade(0f, .35f));
        }
    }

    protected override void ResetVisuals()
    {
        if (_flowers != null && _flowerRotations != null)
        {
            for (int i = 0; i < _flowers.Length; i++)
            {
                if (_flowers[i] == null) continue;
                _flowers[i].DOKill();
                _flowers[i].localRotation = _flowerRotations[i];
            }
        }

        if (_petals != null && _petalHomes != null)
        {
            for (int i = 0; i < _petals.Length; i++)
            {
                SpriteRenderer petal = _petals[i];
                if (petal == null) continue;
                petal.DOKill();
                petal.transform.DOKill();
                petal.transform.localPosition = _petalHomes[i];
                petal.transform.localRotation = Quaternion.identity;
                Color color = petal.color;
                color.a = 0f;
                petal.color = color;
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_heart != null) _heart.Stop();
    }
}
