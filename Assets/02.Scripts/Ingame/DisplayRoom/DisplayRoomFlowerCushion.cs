using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

// 꽃 방석. 다가온 슬라임이 방석 위로 폴짝 올라가 잠깐 낮잠을 잔다. 자는 동안 머리 위로 Zzz가
// 피어오르고 몸이 숨 쉬듯 부풀었다 가라앉는다. 깨면 쭉 기지개를 켜고 옆으로 내려온다.
// 한 번에 한 마리만 올라간다.
public sealed class DisplayRoomFlowerCushion : PlaygroundDecoration
{
    [Tooltip("방석 그림입니다. 올라탈 때 폭 꺼졌다 부풉니다.")]
    [SerializeField] private Transform _cushion;
    [Tooltip("슬라임이 앉을 자리입니다.")]
    [SerializeField] private Transform _seat;
    [SerializeField] private PlaygroundSlimeAttachment _sleep;
    [Tooltip("Zzz 글자들입니다. 차례로 떠올랐다 사라지기를 되풀이합니다.")]
    [SerializeField] private TMP_Text[] _sleepLetters = Array.Empty<TMP_Text>();
    [SerializeField] private Vector2 _sleepRange = new(2f, 4f);
    [SerializeField, Min(0f)] private float _jumpPower = 0.45f;
    [SerializeField, Min(0f)] private float _hopOffDistance = 0.9f;

    private Vector3 _cushionScale;
    private Vector3[] _letterHomes;
    private Sequence _lettersLoop;

    protected override void Awake()
    {
        base.Awake();
        if (!enabled) return;

        if (_cushion == null || _seat == null || _sleep == null)
        {
            Debug.LogError("꽃 방석의 방석, 자리, Zzz 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }

        _cushionScale = _cushion.localScale;
        _letterHomes = new Vector3[_sleepLetters.Length];
        for (int i = 0; i < _sleepLetters.Length; i++)
            if (_sleepLetters[i] != null) _letterHomes[i] = _sleepLetters[i].transform.localPosition;
    }

    protected override void Build(SlimeController slime, Sequence sequence)
    {
        Transform body = slime.transform;
        Vector3 scale = Hold.BaseScale;
        float sleep = UnityEngine.Random.Range(_sleepRange.x, _sleepRange.y);

        // 올라타기
        sequence.Append(body.DOJump(_seat.position, _jumpPower, 1, .45f));
        sequence.AppendCallback(() =>
        {
            PlaySound(EAudioSfx.SlimeLand);
            _cushion.DOKill();
            _cushion.localScale = _cushionScale;
            _cushion.DOPunchScale(new Vector3(_cushionScale.x * .15f, -_cushionScale.y * .2f, 0f), .4f, 5, .5f);
            _sleep.Follow(slime, sleep);
            StartLetters();
        });

        // 숨 쉬기
        int breaths = Mathf.Max(1, Mathf.RoundToInt(sleep / 1.2f));
        for (int i = 0; i < breaths; i++)
        {
            sequence.Append(body.DOScale(new Vector3(scale.x * 1.05f, scale.y * .95f, scale.z), .6f).SetEase(Ease.InOutSine));
            sequence.Append(body.DOScale(scale, .6f).SetEase(Ease.InOutSine));
        }

        // 기지개 켜고 내려오기
        sequence.AppendCallback(StopLetters);
        sequence.Append(body.DOScale(new Vector3(scale.x * .9f, scale.y * 1.18f, scale.z), .22f).SetEase(Ease.OutQuad));
        sequence.Append(body.DOScale(scale, .18f).SetEase(Ease.InQuad));
        sequence.AppendCallback(() => PlaySound(EAudioSfx.SlimeBounce));
        Vector3 side = new(UnityEngine.Random.value < .5f ? -1f : 1f, UnityEngine.Random.Range(-.4f, .2f), 0f);
        Vector3 landing = ClampToPlayground(transform.position + side.normalized * _hopOffDistance, transform.parent);
        sequence.Append(body.DOJump(landing, _jumpPower, 1, .45f));
    }

    private void StartLetters()
    {
        StopLetters();
        _lettersLoop = DOTween.Sequence().SetLoops(-1);
        for (int i = 0; i < _sleepLetters.Length; i++)
        {
            TMP_Text letter = _sleepLetters[i];
            if (letter == null) continue;

            float start = i * .4f;
            Vector3 home = _letterHomes[i];
            _lettersLoop.InsertCallback(start, () => letter.transform.localPosition = home);
            _lettersLoop.Insert(start, letter.DOFade(1f, .2f));
            _lettersLoop.Insert(start, letter.transform.DOLocalMove(home + new Vector3(.25f, .5f, 0f), 1.2f).SetEase(Ease.OutSine));
            _lettersLoop.Insert(start + .9f, letter.DOFade(0f, .3f));
        }
    }

    private void StopLetters()
    {
        _lettersLoop?.Kill();
        _lettersLoop = null;
        if (_sleepLetters == null || _letterHomes == null) return;

        for (int i = 0; i < _sleepLetters.Length; i++)
        {
            TMP_Text letter = _sleepLetters[i];
            if (letter == null) continue;
            letter.DOKill();
            letter.transform.DOKill();
            letter.transform.localPosition = _letterHomes[i];
            letter.alpha = 0f;
        }
    }

    protected override void ResetVisuals()
    {
        StopLetters();
        if (_sleep != null) _sleep.Stop();
        if (_cushion == null) return;
        _cushion.DOKill();
        _cushion.localScale = _cushionScale;
    }
}
