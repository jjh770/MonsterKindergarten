using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 로그인 화면 배경의 슬라임들이 로고가 내려오는 동안 좌우에서 통통 튀어 들어와 제자리에 앉고,
// 그 뒤로는 숨 쉬듯 살짝 늘었다 줄었다 한다. 멀리 있는 작은 슬라임은 화면 끝에서 오면 어색해서
// 제자리에서 톡 솟아난다.
//
// 배경 짝마다 하나씩 있고, 고른 짝만 켜지므로(LoginBackgroundVariants) 켜질 때 시작한다. 로고 낙하와
// 같이 시작해 한 마리씩 천천히 들어오므로, 로고가 내려앉은 뒤에도 몇 초 더 이어진다. 연출일 뿐이라
// 로그인 버튼은 기다리지 않는다.
// 몸(Body)만 움직이고 자리를 잡는 부모는 그대로 둔다. 그림자는 몸을 따라 옆으로만 옮겨 오며 진해진다.
public sealed class LoginSlimeParade : MonoBehaviour
{
    [Serializable]
    private sealed class Member
    {
        [Tooltip("움직일 슬라임 그림입니다. 피벗을 발밑에 둡니다.")]
        public RectTransform Body;
        public Graphic Shadow;
        [Tooltip("켜면 가까운 쪽 화면 끝에서 튀어 오고, 끄면 제자리에서 솟아납니다.")]
        public bool FromSide = true;
    }

    [SerializeField] private Member[] _members;

    [Header("Entrance")]
    [SerializeField, Min(0f)] private float _startDelay = 0.2f;
    [Tooltip("앞 슬라임이 자리에 앉은 뒤 다음 슬라임이 출발하기까지의 틈입니다.")]
    [SerializeField, Min(0f)] private float _gap = 0.15f;
    [SerializeField, Range(1, 6)] private int _hops = 3;
    [SerializeField, Min(0.1f)] private float _hopSeconds = 0.55f;
    [Tooltip("한 번 튈 때의 높이입니다. 캔버스 단위입니다.")]
    [SerializeField, Min(0f)] private float _hopHeight = 90f;
    [Tooltip("화면 밖에서 출발하는 거리입니다. 캔버스 단위입니다.")]
    [SerializeField, Min(0f)] private float _enterDistance = 1100f;
    [SerializeField] private Vector2 _landingSquash = new Vector2(1.14f, 0.86f);

    [Header("Breath")]
    [Tooltip("숨 쉴 때 가로로 늘고 세로로 주는 정도입니다.")]
    [SerializeField, Range(0f, 0.1f)] private float _breathAmount = 0.035f;
    [SerializeField, Min(0.2f)] private float _breathSeconds = 1.4f;

    private Sequence _entrance;
    private Tween[] _breaths;
    private float[] _shadowAlphas;
    private Vector2[] _shadowHomes;

    private void Awake()
    {
        if (_members == null || _members.Length == 0)
        {
            Debug.LogError("로그인 슬라임 연출의 대상이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _breaths = new Tween[_members.Length];
        _shadowAlphas = new float[_members.Length];
        _shadowHomes = new Vector2[_members.Length];
        for (int i = 0; i < _members.Length; i++)
        {
            Graphic shadow = _members[i].Shadow;
            if (shadow == null) continue;

            _shadowAlphas[i] = shadow.color.a;
            _shadowHomes[i] = shadow.rectTransform.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        if (_members == null || _breaths == null) return;

        PlayEntrance();
    }

    private void OnDisable()
    {
        _entrance?.Kill();
        _entrance = null;
        for (int i = 0; i < _members.Length; i++)
        {
            _breaths[i]?.Kill();
            _breaths[i] = null;
            ResetMember(i);
        }
    }

    private void PlayEntrance()
    {
        _entrance?.Kill();
        _entrance = DOTween.Sequence().SetLink(gameObject);

        // 한 마리씩 차례로 들어온다. 앞 슬라임이 자리에 앉은 뒤 다음 슬라임이 출발한다.
        float next = _startDelay;
        for (int i = 0; i < _members.Length; i++)
        {
            Member member = _members[i];
            if (member.Body == null) continue;

            int index = i;
            float duration = member.FromSide ? _hops * _hopSeconds : _hopSeconds * 1.3f;
            float start = next;
            next = start + duration + _gap;
            if (member.FromSide) InsertSideHops(member, start);
            else InsertPop(member, start);

            InsertShadow(index, start, duration);
            _entrance.InsertCallback(start + duration, () => StartBreath(index));
        }
    }

    // 가까운 쪽 화면 끝에서 출발해 몇 번 튀며 제자리로 온다. 땅에 닿을 때마다 살짝 눌린다.
    private void InsertSideHops(Member member, float start)
    {
        RectTransform body = member.Body;
        float side = EntrySide(body);
        Vector2 home = Vector2.zero;
        Vector2 from = new Vector2(side * _enterDistance, 0f);

        body.anchoredPosition = from;
        body.localScale = Vector3.one;
        for (int hop = 0; hop < _hops; hop++)
        {
            float t = start + hop * _hopSeconds;
            Vector2 to = Vector2.Lerp(from, home, (hop + 1f) / _hops);
            // 마지막으로 갈수록 낮게 튀어 착지가 차분해진다.
            float height = _hopHeight * (1f - 0.35f * hop / Mathf.Max(1, _hops - 1));
            _entrance.Insert(t, body.DOJumpAnchorPos(to, height, 1, _hopSeconds * 0.8f).SetEase(Ease.Linear));
            _entrance.Insert(t + _hopSeconds * 0.8f, Squash(body, _hopSeconds * 0.2f));
        }
    }

    // 제자리에서 작게 솟아나 한 번 통 튄다.
    private void InsertPop(Member member, float start)
    {
        RectTransform body = member.Body;
        body.anchoredPosition = Vector2.zero;
        body.localScale = Vector3.zero;
        _entrance.Insert(start, body.DOScale(Vector3.one, _hopSeconds * 0.6f).SetEase(Ease.OutBack));
        _entrance.Insert(start + _hopSeconds * 0.4f,
            body.DOJumpAnchorPos(Vector2.zero, _hopHeight * 0.4f, 1, _hopSeconds * 0.6f).SetEase(Ease.Linear));
        _entrance.Insert(start + _hopSeconds, Squash(body, _hopSeconds * 0.3f));
    }

    // 그림자는 몸을 따라 옆으로만 오고, 다가오는 동안 진해진다.
    private void InsertShadow(int index, float start, float duration)
    {
        Graphic shadow = _members[index].Shadow;
        if (shadow == null) return;

        RectTransform rect = shadow.rectTransform;
        Vector2 home = _shadowHomes[index];
        Color color = shadow.color;
        color.a = 0f;
        shadow.color = color;
        if (_members[index].FromSide)
        {
            float side = EntrySide(_members[index].Body);
            rect.anchoredPosition = home + new Vector2(side * _enterDistance, 0f);
            _entrance.Insert(start, rect.DOAnchorPos(home, duration * 0.8f).SetEase(Ease.Linear));
        }

        _entrance.Insert(start, shadow.DOFade(_shadowAlphas[index], duration).SetEase(Ease.InQuad));
    }

    private Tween Squash(RectTransform body, float seconds)
    {
        return DOTween.Sequence()
            .Append(body.DOScale(new Vector3(_landingSquash.x, _landingSquash.y, 1f), seconds * 0.4f).SetEase(Ease.OutQuad))
            .Append(body.DOScale(Vector3.one, seconds * 0.6f).SetEase(Ease.OutBack));
    }

    // 숨쉬기는 슬라임마다 시작 위상을 달리해 한꺼번에 들썩이지 않게 한다.
    private void StartBreath(int index)
    {
        RectTransform body = _members[index].Body;
        if (body == null) return;

        _breaths[index]?.Kill();
        body.localScale = Vector3.one;
        _breaths[index] = body
            .DOScale(new Vector3(1f + _breathAmount, 1f - _breathAmount, 1f), _breathSeconds * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetDelay(UnityEngine.Random.Range(0f, _breathSeconds * 0.5f))
            .SetLink(body.gameObject);
    }

    private void ResetMember(int index)
    {
        Member member = _members[index];
        if (member.Body != null)
        {
            member.Body.anchoredPosition = Vector2.zero;
            member.Body.localScale = Vector3.one;
        }

        if (member.Shadow != null)
        {
            Color color = member.Shadow.color;
            color.a = _shadowAlphas[index];
            member.Shadow.color = color;
            member.Shadow.rectTransform.anchoredPosition = _shadowHomes[index];
        }
    }

    // 출발할 쪽을 몸의 부모(자리 틀) 좌표의 x 부호로 돌려준다. 틀의 앵커 가운데가 배경 왼쪽 절반이면
    // 화면 왼쪽에서 온다. 틀을 Y로 180도 돌려 슬라임이 반대쪽을 보게 하면 틀 안의 x가 화면과 거꾸로라,
    // 화면 기준 방향을 틀 안의 방향으로 바꿔야 가까운 쪽 화면 끝에서 들어온다.
    private static float EntrySide(RectTransform body)
    {
        var slot = body.parent as RectTransform;
        if (slot == null) return -1f;

        float screenSide = (slot.anchorMin.x + slot.anchorMax.x) * 0.5f < 0.5f ? -1f : 1f;
        float facing = slot.TransformDirection(Vector3.right).x < 0f ? -1f : 1f;
        return screenSide * facing;
    }
}
