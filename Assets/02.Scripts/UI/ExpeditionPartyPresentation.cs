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
    private static readonly Color EmptyColor = new(.48f, .48f, .48f, 1f);
    public bool IsAnimating => _animation != null;
    public IEnumerable<string> MemberIds => _targets.Where(target => target != null).Select(target => target.InstanceId);
    public event Action<int> SlotClicked;

    private void Awake()
    {
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
        _departureLabel.gameObject.SetActive(true);
        _departureLabel.transform.position = center + _flightLayer.TransformVector(new Vector3(0f, 145f));
        _departureLabel.alpha = 0f;
        _animation.Insert(.35f, _departureLabel.DOFade(1f, .18f));
        _animation.Insert(.85f, _departureLabel.DOFade(0f, .2f));
        _animation.OnComplete(() => { _animation = null; Clear(); completed?.Invoke(); });
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
        fly.gameObject.SetActive(true);
    }

}
