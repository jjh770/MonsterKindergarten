using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 선택 목록과 편성 슬롯 사이의 UI 이동을 맡는다. 실제 개체와 탐험 저장은 변경하지 않는다.
public sealed class ExpeditionPartyPresentation : MonoBehaviour
{
    [SerializeField] private Button[] _buttons;
    [SerializeField] private Image[] _icons;
    [SerializeField] private TextMeshProUGUI[] _labels;
    [SerializeField] private Image[] _flyingIcons;
    [SerializeField] private TextMeshProUGUI _departureLabel;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Material _silhouetteMaterial;
    [SerializeField] private RectTransform _flightLayer;
    private readonly SlimeInstance[] _targets = new SlimeInstance[3];
    private readonly RectTransform[] _sources = new RectTransform[3];
    private readonly Sprite[] _sprites = new Sprite[3];
    private Sequence _animation;
    // 출발 문구는 씬에 적힌 것을 쓰고, 귀환 때만 다른 문구로 바꾼다.
    private string _departureText;
    private static readonly Color EmptyColor = new(.48f, .48f, .48f, 1f);
    public bool IsAnimating => _animation != null;
    public IEnumerable<string> MemberIds => _targets.Where(target => target != null).Select(target => target.InstanceId);
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
    public bool ContainsGrade(ESlimeGrade grade) => _targets.Any(target => target != null && target.Grade == grade);

    public bool TrySelect(SlimeInstance target, RectTransform source, SlimeManager slimes, Action completed)
    {
        if (target == null || IsAnimating || ContainsGrade(target.Grade)) return false;
        int slot = Array.FindIndex(_targets, item => item == null);
        if (slot < 0) return false;
        _targets[slot] = target;
        _sources[slot] = source;
        _sprites[slot] = slimes.Get(target.Grade).SpecData.Sprite;
        Image fly = _flyingIcons[slot];
        SetFly(slot, _sprites[slot], source.position);
        fly.rectTransform.sizeDelta = source.rect.size;
        _icons[slot].sprite = _sprites[slot];
        _icons[slot].color = EmptyColor;
        _labels[slot].text = (target.IsSpecial ? "특별 " : "") + slimes.GetName(target.Grade);
        _animation = DOTween.Sequence().SetUpdate(true);
        _animation.Join(fly.rectTransform.DOMove(_icons[slot].transform.position, .35f).SetEase(Ease.OutCubic));
        _animation.Join(fly.rectTransform.DOSizeDelta(_icons[slot].rectTransform.rect.size, .35f));
        _animation.AppendCallback(() => fly.gameObject.SetActive(false));
        _animation.AppendCallback(() => _icons[slot].material = null);
        _animation.Append(_icons[slot].DOColor(Color.white, .12f));
        _animation.OnComplete(() => { _animation = null; completed?.Invoke(); });
        return true;
    }

    public void ReturnSlot(int slot, Action completed)
    {
        if (IsAnimating || _targets[slot] == null) return;
        _animation = DOTween.Sequence().SetUpdate(true);
        AppendReturn(_animation, slot);
        _animation.OnComplete(() => { _animation = null; completed?.Invoke(); });
    }

    public void ReturnAll(Action completed)
    {
        if (IsAnimating) return;
        if (_targets.All(target => target == null)) { completed?.Invoke(); return; }
        _animation = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < 3; i++) if (_targets[i] != null) AppendReturn(_animation, i);
        _animation.OnComplete(() => { _animation = null; completed?.Invoke(); });
    }

    private void AppendReturn(Sequence sequence, int slot)
    {
        SetFly(slot, _sprites[slot], _icons[slot].transform.position);
        ResetSlot(slot);
        Image fly = _flyingIcons[slot];
        sequence.Insert(0f, fly.rectTransform.DOSizeDelta(_sources[slot].rect.size, .35f));
        sequence.Insert(0f, fly.rectTransform.DOMove(_sources[slot].position, .35f).SetEase(Ease.InOutCubic)
            .OnComplete(() =>
            {
                _targets[slot] = null;
                fly.gameObject.SetActive(false);
            }));
    }

    public void PlayDeparture(RectTransform destinationArea, Action completed)
    {
        _animation?.Kill();
        _animation = DOTween.Sequence().SetUpdate(true);
        Vector3 center = destinationArea.TransformPoint(Vector3.zero);
        for (int i = 0; i < 3; i++)
        {
            if (_targets[i] == null) continue;
            SetFly(i, _sprites[i], _icons[i].transform.position);
            ResetSlot(i);
            Image fly = _flyingIcons[i];
            Vector3 together = center + _flightLayer.TransformVector(new Vector3((i - 1) * 130f, 0f));
            _animation.Insert(0f, fly.rectTransform.DOMove(together, .42f).SetEase(Ease.OutCubic));

        }
        ShowDepartureLabel(_departureText, center + _flightLayer.TransformVector(new Vector3(0f, 145f)), .35f, .85f);
        _animation.OnComplete(() => { _animation = null; Clear(); completed?.Invoke(); });
    }

    // 탐험지에서 돌아온 슬라임들이 그 자리에서 공중제비와 좌우 흔들기로 기뻐하는 동안 "다녀왔어요!"가
    // 떴다 사라지고, 그다음 한 마리씩 통통 튀어 아래 장식장 목록의 제자리(homes)로 돌아간다. 닿으면
    // 그 자리의 회색 그림을 원래 슬라임으로 되돌린다. starts와 sizes는 탐험지 화면에서 보이던 자리와
    // 크기다. homes가 비어 있는 슬라임은 그 자리에서 작아지며 사라진다.
    public void PlayReturn(RectTransform destinationArea, IReadOnlyList<Sprite> sprites,
        IReadOnlyList<Vector3> starts, IReadOnlyList<Vector2> sizes, IReadOnlyList<Image> homes, Action completed)
    {
        _animation?.Kill();
        _animation = DOTween.Sequence().SetUpdate(true);
        float flipPower = _flightLayer.TransformVector(new Vector3(0f, 90f)).y;
        float homePower = _flightLayer.TransformVector(new Vector3(0f, 150f)).y;
        int count = Mathf.Min(3, sprites.Count);
        for (int i = 0; i < 3; i++)
        {
            _targets[i] = null;
            ResetSlot(i);
        }

        for (int i = 0; i < count; i++)
        {
            int slot = i;
            Image fly = _flyingIcons[slot];
            RectTransform rect = fly.rectTransform;
            SetFly(slot, sprites[slot], starts[slot]);
            rect.sizeDelta = sizes[slot];
            float offset = slot * .08f;
            float turn = slot % 2 == 0 ? 360f : -360f;

            // 축하: 공중제비, 좌우 흔들기, 반대 방향 공중제비.
            AppendFlip(rect, starts[slot], flipPower, turn, offset, slot == 0);
            _animation.Insert(offset + .6f, rect.DOPunchRotation(new Vector3(0f, 0f, 22f), .6f, 6, .5f));
            AppendFlip(rect, starts[slot], flipPower, -turn, offset + 1.25f, slot == 0);

            // 귀환: 장식장 목록의 제자리로 튀어 간다.
            float leave = CelebrationSeconds + slot * .12f;
            Image home = slot < homes.Count ? homes[slot] : null;
            if (home != null)
            {
                _animation.Insert(leave, rect.DOJump(home.transform.position, homePower, 1, .6f).SetEase(Ease.Linear));
                _animation.Insert(leave, rect.DOSizeDelta(home.rectTransform.rect.size, .6f));
                _animation.InsertCallback(leave + .6f, () =>
                {
                    AudioManager.Instance?.PlaySFXRandomPitch(EAudioSfx.SlimeLand);
                    fly.gameObject.SetActive(false);
                    home.sprite = sprites[slot];
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

    public void Refresh(ExpeditionRun run, SlimeManager slimes, bool canEdit)
    {
        if (IsAnimating) return;
        for (int i = 0; i < 3; i++)
        {
            _buttons[i].interactable = canEdit && _targets[i] != null;
            if (run != null && i < run.Members.Count)
            {
                ExpeditionMember member = run.Members[i];
                _icons[i].sprite = slimes.Get(member.Grade).SpecData.Sprite;
                _icons[i].material = null;
                _icons[i].color = Color.white;
                _labels[i].text = (member.IsSpecial ? "특별 " : "") + slimes.GetName(member.Grade);
            }
            else if (_targets[i] == null) ResetSlot(i);
        }
    }

    public void Clear()
    {
        _animation?.Kill();
        _animation = null;
        for (int i = 0; i < 3; i++)
        {
            _targets[i] = null;
            if (_icons != null && _icons.Length > i && _icons[i] != null) _icons[i].transform.localScale = Vector3.one;
            if (_icons != null && _icons.Length > i && _icons[i] != null &&
                _labels != null && _labels.Length > i && _labels[i] != null) ResetSlot(i);
            if (_flyingIcons != null && _flyingIcons.Length > i && _flyingIcons[i] != null)
                _flyingIcons[i].gameObject.SetActive(false);
        }
        if (_departureLabel != null) _departureLabel.gameObject.SetActive(false);
    }

    private void ResetSlot(int slot)
    {
        _icons[slot].material = _silhouetteMaterial;
        _icons[slot].sprite = _emptySprite;
        _icons[slot].color = EmptyColor;
        _labels[slot].text = "빈자리";
    }

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
