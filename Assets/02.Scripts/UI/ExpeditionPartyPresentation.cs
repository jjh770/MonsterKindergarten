using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 아래 장식장 목록과 탐험지 필드 사이의 편성 연출을 맡는다. 목록에서 고른 슬라임은 깡충 뛰어
// 탐험지 가운데 줄의 자기 칸에 내려앉아 가끔 통통 뛰며 기다리고, 그 슬라임을 누르면 목록으로
// 돌아간다. 실제 개체와 탐험 저장은 변경하지 않는다.
//
// 칸 세 개는 탐험지 화면이 막 출발한 탐험의 슬라임을 세우는 자리와 같다. 그래서 출발하면 칸을
// 비우는 순간 탐험지 화면이 같은 자리에서 이어받아 돌아다니게 한다.
public sealed class ExpeditionPartyPresentation : MonoBehaviour
{
    // 귀환 연출 한 마리의 그림, 탐험지 화면에서 보이던 자리와 크기, 돌아갈 장식장 목록 자리다.
    // Home이 없으면 그 자리에서 작아지며 사라진다.
    public readonly struct ReturningMember
    {
        public readonly Sprite Sprite;
        public readonly Vector3 Start;
        public readonly Vector2 Size;
        public readonly Image Home;

        public ReturningMember(Sprite sprite, Vector3 start, Vector2 size, Image home)
        {
            Sprite = sprite;
            Start = start;
            Size = size;
            Home = home;
        }
    }

    [Tooltip("탐험지 필드 안에 선 편성 칸입니다. 누르면 그 슬라임을 목록으로 돌려보냅니다.")]
    [SerializeField] private Button[] _buttons;
    [SerializeField] private Image[] _icons;
    [SerializeField] private Image[] _flyingIcons;
    [SerializeField] private TMPro.TextMeshProUGUI _departureLabel;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Material _silhouetteMaterial;
    [SerializeField] private RectTransform _flightLayer;

    [Header("Waiting")]
    [Tooltip("탐험지에서 기다리는 슬라임이 한 번씩 통통 뛰는 간격 범위입니다(초).")]
    [SerializeField] private Vector2 _idleHopInterval = new(1.2f, 2.6f);
    [SerializeField, Min(0f)] private float _idleHopHeight = 22f;

    private readonly SlimeInstance[] _targets = new SlimeInstance[3];
    private readonly RectTransform[] _sources = new RectTransform[3];
    private readonly Sprite[] _sprites = new Sprite[3];
    private Sequence _animation;
    private Tween _idleHop;
    private float _nextIdleHop;
    // 출발 문구는 씬에 적힌 것을 쓰고, 귀환 때만 다른 문구로 바꾼다.
    private string _departureText;
    private static readonly Color EmptyColor = new(.48f, .48f, .48f, 1f);
    public bool IsAnimating => _animation != null;
    // 칸 순서대로다. 출발한 탐험도 이 순서로 저장돼야 탐험지 화면이 서 있던 자리에서 이어받는다.
    public IEnumerable<string> MemberIds => _targets.Where(target => target != null).Select(target => target.InstanceId);
    public int MemberCount => _targets.Count(target => target != null);
    public event Action<int> SlotClicked;

    private void Awake()
    {
        _departureText = _departureLabel.text;
        for (int i = 0; i < _buttons.Length; i++)
        {
            int slot = i;
            _buttons[i].onClick.AddListener(() => SlotClicked?.Invoke(slot));
        }
        Clear();
    }

    private void OnDisable() => Clear();

    private void Update()
    {
        if (IsAnimating || _idleHop != null || Time.unscaledTime < _nextIdleHop) return;

        _nextIdleHop = Time.unscaledTime + UnityEngine.Random.Range(_idleHopInterval.x, _idleHopInterval.y);
        int count = MemberCount;
        if (count == 0) return;

        // 기다리는 슬라임 가운데 하나가 제자리에서 통통 뛴다.
        int pick = UnityEngine.Random.Range(0, count);
        for (int i = 0; i < 3; i++)
        {
            if (_targets[i] == null || pick-- > 0) continue;

            RectTransform icon = _icons[i].rectTransform;
            _idleHop = icon.DOJumpAnchorPos(icon.anchoredPosition, _idleHopHeight, 1, .35f)
                .SetUpdate(true)
                .OnKill(() => _idleHop = null);
            return;
        }
    }

    public bool ContainsGrade(ESlimeGrade grade) => _targets.Any(target => target != null && target.Grade == grade);

    // 목록의 source 자리에서 깡충 뛰어 탐험지의 빈 칸에 내려앉는다.
    public bool TrySelect(SlimeInstance target, RectTransform source, SlimeManager slimes, Action completed)
    {
        if (target == null || IsAnimating || ContainsGrade(target.Grade)) return false;
        int slot = Array.FindIndex(_targets, item => item == null);
        if (slot < 0) return false;
        StopIdleHop();
        _targets[slot] = target;
        _sources[slot] = source;
        _sprites[slot] = slimes.Get(target.Grade).SpecData.Sprite;
        Image fly = _flyingIcons[slot];
        SetFly(slot, _sprites[slot], source.position);
        fly.rectTransform.sizeDelta = source.rect.size;
        _animation = DOTween.Sequence().SetUpdate(true);
        _animation.InsertCallback(0f, () => PlaySound(EAudioSfx.SlimeBounce));
        _animation.Insert(0f, fly.rectTransform.DOJump(_icons[slot].transform.position, HopPower(160f), 1, HopSeconds)
            .SetEase(Ease.Linear));
        _animation.Insert(0f, fly.rectTransform.DOSizeDelta(_icons[slot].rectTransform.rect.size, HopSeconds));
        _animation.AppendCallback(() =>
        {
            fly.gameObject.SetActive(false);
            ShowSlot(slot);
            PlaySound(EAudioSfx.SlimeLand);
        });
        _animation.Append(_icons[slot].rectTransform.DOPunchScale(new Vector3(.18f, -.18f, 0f), .25f, 5, .5f));
        _animation.OnComplete(() =>
        {
            _animation = null;
            completed?.Invoke();
        });
        return true;
    }

    // 탐험지에 내려앉은 그 슬라임을 목록으로 돌려보낸다. 편성에 없으면 false다.
    public bool TryReturnMember(string instanceId, Action completed)
    {
        int slot = Array.FindIndex(_targets, target => target != null && target.InstanceId == instanceId);
        if (slot < 0 || IsAnimating) return false;

        ReturnSlot(slot, completed);
        return true;
    }

    public void ReturnSlot(int slot, Action completed)
    {
        if (IsAnimating || _targets[slot] == null) return;
        StopIdleHop();
        _animation = DOTween.Sequence().SetUpdate(true);
        AppendReturn(_animation, slot);
        _animation.OnComplete(() =>
        {
            _animation = null;
            completed?.Invoke();
        });
    }

    public void ReturnAll(Action completed)
    {
        if (IsAnimating) return;
        if (MemberCount == 0)
        {
            completed?.Invoke();
            return;
        }

        StopIdleHop();
        _animation = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < 3; i++)
        {
            if (_targets[i] != null) AppendReturn(_animation, i);
        }

        _animation.OnComplete(() =>
        {
            _animation = null;
            completed?.Invoke();
        });
    }

    private void AppendReturn(Sequence sequence, int slot)
    {
        SetFly(slot, _sprites[slot], _icons[slot].transform.position);
        ResetSlot(slot);
        Image fly = _flyingIcons[slot];
        sequence.InsertCallback(0f, () => PlaySound(EAudioSfx.SlimeBounce));
        sequence.Insert(0f, fly.rectTransform.DOSizeDelta(_sources[slot].rect.size, HopSeconds));
        sequence.Insert(0f, fly.rectTransform.DOJump(_sources[slot].position, HopPower(160f), 1, HopSeconds)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                _targets[slot] = null;
                fly.gameObject.SetActive(false);
            }));
    }

    // 탐험지에 선 슬라임들이 출발음과 함께 제자리에서 두 번 뛰고 "탐험 시작!"이 뜬다. 끝나면 칸을
    // 비우고, 탐험지 화면이 같은 자리에서 이어받아 돌아다니게 한다.
    public void PlayDeparture(RectTransform destinationArea, Action completed)
    {
        _animation?.Kill();
        StopIdleHop();
        _animation = DOTween.Sequence().SetUpdate(true);
        Vector3 center = destinationArea.TransformPoint(Vector3.zero);
        _animation.InsertCallback(0f, () => PlaySound(EAudioSfx.AreaTransition));
        for (int i = 0; i < 3; i++)
        {
            if (_targets[i] == null) continue;

            RectTransform icon = _icons[i].rectTransform;
            float delay = i * .08f;
            _animation.Insert(delay, icon.DOJump(icon.position, HopPower(70f), 2, .7f));
            _animation.Insert(delay + .7f, icon.DOPunchScale(new Vector3(.18f, -.18f, 0f), .25f, 5, .5f));
        }

        ShowDepartureLabel(_departureText, center + _flightLayer.TransformVector(new Vector3(0f, 145f)), .2f, .95f);
        _animation.OnComplete(() =>
        {
            _animation = null;
            Clear();
            completed?.Invoke();
        });
    }

    // 탐험지에서 돌아온 슬라임들이 그 자리에서 공중제비와 좌우 흔들기로 기뻐하는 동안 "다녀왔어요!"가
    // 떴다 사라지고, 그다음 한 마리씩 통통 튀어 아래 장식장 목록의 제자리(Home)로 돌아간다. 닿으면
    // 그 자리의 회색 그림을 원래 슬라임으로 되돌린다.
    public void PlayReturn(RectTransform destinationArea, IReadOnlyList<ReturningMember> members, Action completed)
    {
        _animation?.Kill();
        _animation = DOTween.Sequence().SetUpdate(true);
        float flipPower = _flightLayer.TransformVector(new Vector3(0f, 90f)).y;
        float homePower = _flightLayer.TransformVector(new Vector3(0f, 150f)).y;
        int count = Mathf.Min(3, members.Count);
        for (int i = 0; i < 3; i++)
        {
            _targets[i] = null;
            ResetSlot(i);
        }

        for (int i = 0; i < count; i++)
        {
            int slot = i;
            Image fly = _flyingIcons[slot];
            ReturningMember member = members[slot];
            RectTransform rect = fly.rectTransform;
            SetFly(slot, member.Sprite, member.Start);
            rect.sizeDelta = member.Size;
            float offset = slot * .08f;
            float turn = slot % 2 == 0 ? 360f : -360f;

            // 축하: 공중제비, 좌우 흔들기, 반대 방향 공중제비.
            AppendFlip(rect, member.Start, flipPower, turn, offset, slot == 0);
            _animation.Insert(offset + .6f, rect.DOPunchRotation(new Vector3(0f, 0f, 22f), .6f, 6, .5f));
            AppendFlip(rect, member.Start, flipPower, -turn, offset + 1.25f, slot == 0);

            // 귀환: 장식장 목록의 제자리로 튀어 간다.
            float leave = CelebrationSeconds + slot * .12f;
            Image home = member.Home;
            if (home != null)
            {
                _animation.Insert(leave, rect.DOJump(home.transform.position, homePower, 1, .6f).SetEase(Ease.Linear));
                _animation.Insert(leave, rect.DOSizeDelta(home.rectTransform.rect.size, .6f));
                _animation.InsertCallback(leave + .6f, () =>
                {
                    AudioManager.Instance?.PlaySFXRandomPitch(EAudioSfx.SlimeLand);
                    fly.gameObject.SetActive(false);
                    home.sprite = member.Sprite;
                    home.material = null;
                    home.color = Color.white;
                });
                _animation.Insert(leave + .6f, home.transform.DOPunchScale(Vector3.one * .25f, .3f, 6, .5f));
            }
            else
            {
                _animation.Insert(leave, rect.DOScale(0f, .3f).SetEase(Ease.InBack));
            }
        }

        Vector3 center = destinationArea.TransformPoint(Vector3.zero);
        _animation.InsertCallback(.3f, () => AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock));
        ShowDepartureLabel(UiMessages.ExpeditionHomecoming, center + _flightLayer.TransformVector(new Vector3(0f, 145f)),
            .3f, CelebrationSeconds - .25f);
        _animation.AppendInterval(.15f);
        _animation.OnComplete(() => { _animation = null; Clear(); completed?.Invoke(); });
    }

    // 축하가 끝나고 "다녀왔어요!"가 사라진 뒤 장식장으로 출발하는 때다.
    private const float CelebrationSeconds = 2f;

    // 제자리에서 한 번 뛰어오르며 한 바퀴 돈다.
    private void AppendFlip(RectTransform rect, Vector3 position, float power, float turn, float at, bool sound)
    {
        _animation.Insert(at, rect.DOJump(position, power, 1, .55f));
        _animation.Insert(at, rect.DOLocalRotate(new Vector3(0f, 0f, turn), .55f, RotateMode.FastBeyond360)
            .SetEase(Ease.OutQuad).SetRelative());
        if (sound) _animation.InsertCallback(at, () => AudioManager.Instance?.PlaySFXRandomPitch(EAudioSfx.SlimeBounce));
    }

    // 편성 칸과 장식장 목록이 함께 쓰는 슬라임 이름이다. 특별한 슬라임은 그렇다고 붙여 준다.
    public static string MemberName(SlimeManager slimes, ESlimeGrade grade, bool isSpecial)
    {
        string name = slimes.GetName(grade);
        return isSpecial ? string.Format(UiMessages.ExpeditionSpecialName, name) : name;
    }

    // 아래 장식장 목록에서 편성됐거나 떠나 있는 슬라임 자리를 빈자리와 같은 회색 그림으로 둔다.
    public void ApplyEmptyLook(Image image)
    {
        image.sprite = _emptySprite;
        image.material = _silhouetteMaterial;
        image.color = EmptyColor;
    }

    private void ShowDepartureLabel(string text, Vector3 position, float fadeIn, float fadeOut)
    {
        _departureLabel.text = text;
        _departureLabel.gameObject.SetActive(true);
        _departureLabel.transform.position = position;
        _departureLabel.alpha = 0f;
        _animation.Insert(fadeIn, _departureLabel.DOFade(1f, .18f));
        _animation.Insert(fadeOut, _departureLabel.DOFade(0f, .2f));
    }

    // 떠나 있는 탐험이 있으면 탐험지 화면이 그 슬라임들을 보여 주므로 칸은 비워 둔다.
    public void Refresh(ExpeditionRun run, bool canEdit)
    {
        if (IsAnimating) return;
        for (int i = 0; i < 3; i++)
        {
            _buttons[i].interactable = run == null && canEdit && _targets[i] != null;
            if (run != null || _targets[i] == null) ResetSlot(i);
        }
    }

    public void Clear()
    {
        _animation?.Kill();
        _animation = null;
        StopIdleHop();
        for (int i = 0; i < 3; i++)
        {
            _targets[i] = null;
            if (_icons != null && _icons.Length > i && _icons[i] != null) ResetSlot(i);
            if (_flyingIcons != null && _flyingIcons.Length > i && _flyingIcons[i] != null)
                _flyingIcons[i].gameObject.SetActive(false);
        }
        if (_departureLabel != null) _departureLabel.gameObject.SetActive(false);
    }

    private void ShowSlot(int slot)
    {
        Image icon = _icons[slot];
        icon.sprite = _sprites[slot];
        icon.material = null;
        icon.color = Color.white;
        icon.transform.localScale = Vector3.one;
        icon.enabled = true;
    }

    private void ResetSlot(int slot)
    {
        _icons[slot].enabled = false;
        _icons[slot].transform.localScale = Vector3.one;
    }

    // 뛰고 있던 슬라임을 제자리에 내려놓고 멈춘다. 다른 연출이 같은 그림을 움직이기 전에 부른다.
    private void StopIdleHop()
    {
        _idleHop?.Kill(complete: true);
        _idleHop = null;
    }

    private float HopPower(float uiUnits) => _flightLayer.TransformVector(new Vector3(0f, uiUnits)).y;

    private static void PlaySound(EAudioSfx cue) => AudioManager.Instance?.PlaySFXRandomPitch(cue);

    // 목록에서 탐험지까지, 또는 그 반대로 한 번 뛰는 시간이다.
    private const float HopSeconds = .45f;

    private void SetFly(int slot, Sprite sprite, Vector3 position)
    {
        Image fly = _flyingIcons[slot];
        fly.sprite = sprite;
        fly.color = Color.white;
        fly.rectTransform.sizeDelta = _icons[slot].rectTransform.rect.size;
        fly.transform.position = position;
        fly.transform.localRotation = Quaternion.identity;
        fly.transform.localScale = Vector3.one;
        fly.gameObject.SetActive(true);
    }
}
