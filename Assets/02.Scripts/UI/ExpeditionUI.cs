using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 탐험 화면의 편성·시간 표시와 서비스 호출을 맡는다. 진행 상태는 저장 도메인에서 읽는다.
public sealed class ExpeditionUI : MonoBehaviour
{
    [Serializable]
    private sealed class DestinationView
    {
        public string Name;
        public EExpeditionDestinationType Type;
        public int Tickets;
        [Range(0f, 1f)] public float DecorationChance;
        public Button Button;
        public TextMeshProUGUI Label;
        public Sprite PreviewSprite;
        public TextMeshProUGUI TimeLabel;
        public GameObject LockRoot;
        public TextMeshProUGUI LockLabel;
        public ExpeditionDestination CreateRules() => new(Type, Array.Empty<ESlimeGrade>(), freeParty: true);
    }

    [SerializeField] private SlimeManager _slimes;
    [SerializeField] private CurrencyManager _currency;
    [SerializeField] private GameplaySpaceManager _spaces;
    [SerializeField] private GameManager _game;
    [SerializeField] private DisplayRoomInfoUI _info;
    [SerializeField] private ShopUI _shop;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private GameExitManager _exit;
    [SerializeField] private HudVisibility _hud;
    [SerializeField] private Button _enterButton;
    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _actionButton;
    [SerializeField] private TextMeshProUGUI _actionLabel;
    [SerializeField] private TextMeshProUGUI _detail;
    [SerializeField] private TextMeshProUGUI _notice;
    [SerializeField] private DestinationView[] _destinations;
    [SerializeField] private Sprite _destinationTabSprite;
    [SerializeField] private Sprite _selectedDestinationTabSprite;
    [SerializeField] private ExpeditionPartyPresentation _party;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Image _destinationImage;
    [SerializeField] private RectTransform _destinationArea;
    [SerializeField] private ExpeditionDestinationPresentation _destinationPresentation;
    [SerializeField] private TextMeshProUGUI _memberHeading;
    [SerializeField] private Button[] _memberButtons;
    [SerializeField] private Image[] _memberImages;
    [SerializeField] private TextMeshProUGUI[] _memberLabels;
    private readonly SlimeInstance[] _candidates = new SlimeInstance[20];
    private bool _departing;
    private ExpeditionService _service;
    private ExpeditionRewardCalculator _rewards;
    private GameplayModeSession _session;
    private int _destinationIndex;
    private bool _isOpen;
    private bool _recoveryAttempted;
    private bool _recoverySucceeded;
    private float _nextRefresh;

    private void Start()
    {
        if (_slimes == null || _currency == null || _spaces == null || _game == null ||
            _info == null || _shop == null || _clicker == null || _exit == null || _hud == null ||
            _enterButton == null || _root == null || _group == null || _closeButton == null ||
            _actionButton == null || _actionLabel == null || _detail == null || _notice == null ||
            _destinations == null || _destinations.Length != 3 || _party == null ||
            _destinationImage == null || _destinationArea == null || _destinationPresentation == null ||
            _memberHeading == null || _memberButtons == null || _memberButtons.Length != 20 || _memberImages == null || _memberImages.Length != 20 ||
            _memberLabels == null || _memberLabels.Length != 20 || _nameText == null)
        {
            Debug.LogError("탐험 화면의 필수 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }
        _service = new ExpeditionService(_slimes, _currency);
        _rewards = new ExpeditionRewardCalculator(_slimes);
        _session = new GameplayModeSession(_shop, _clicker, _exit, _hud, EHudParts.All,
            _root, _group, 0.18f);
        _session.ResetPresentation();
        _enterButton.onClick.AddListener(Open);
        _closeButton.onClick.AddListener(Close);
        _actionButton.onClick.AddListener(OnAction);
        for (int i = 0; i < _destinations.Length; i++)
        {
            int index = i;
            _destinations[i].Button.onClick.AddListener(() => SelectDestination(index));
        }
        for (int i = 0; i < _memberButtons.Length; i++)
        {
            int index = i;
            _memberButtons[i].onClick.AddListener(() => SelectMember(index));
        }
        _party.SlotClicked += ReturnSlot;
        _slimes.ExpeditionChanged += Refresh;
        _spaces.SpaceChanged += OnSpaceChanged;
        _game.OnGameplayActivated += OnActivated;
        TutorialManager.Started += OnTutorialStarted;
        Refresh();
    }

    private void OnDestroy()
    {
        ForceClose();
        if (_party != null) _party.SlotClicked -= ReturnSlot;
        _session?.Dispose();
        _enterButton?.onClick.RemoveListener(Open);
        _closeButton?.onClick.RemoveListener(Close);
        _actionButton?.onClick.RemoveListener(OnAction);
        // 목적지·편성 버튼은 이 화면만 소유하고 함께 파괴된다.
        if (_slimes != null) _slimes.ExpeditionChanged -= Refresh;
        if (_spaces != null) _spaces.SpaceChanged -= OnSpaceChanged;
        if (_game != null) _game.OnGameplayActivated -= OnActivated;
        TutorialManager.Started -= OnTutorialStarted;
    }

    private void OnDisable()
    {
        if (_isOpen) ForceClose();
    }

    private void Update()
    {
        if (_service == null) return;
        if (_isOpen && (!GameplayGate.IsActive || TutorialManager.IsRunning || _spaces.IsTransitioning))
            ForceClose();
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 1f;
        if (!_recoveryAttempted && GameplayGate.IsReady)
        {
            _recoveryAttempted = true;
            _recoverySucceeded = _service.TryRecoverSettlement();
        }
        Refresh();
    }

    private void OnActivated() { _recoveryAttempted = false; Refresh(); }
    private void OnTutorialStarted() { ForceClose(); Refresh(); }
    private void OnSpaceChanged(EGameplaySpace space) { ForceClose(); Refresh(); }

    private bool CanOpen() => _slimes.IsInitialized && _slimes.IsGachaUnlocked &&
        !_spaces.IsMainFieldActive && !_spaces.IsTransitioning && GameplayGate.IsActive &&
        !TutorialManager.IsRunning && !_info.IsVisible;

    private void Open()
    {
        if (_isOpen || _session.IsTransitioning || !CanOpen()) return;
        _spaces.RestoreDisplayRoomFocus(() =>
        {
            if (this == null || !CanOpen()) return;
            _recoveryAttempted = true;
            _recoverySucceeded = _service.TryRecoverSettlement();
            _party.Clear();
            _group.interactable = true;
            _notice.text = _recoverySucceeded ? "탐험에 보낼 슬라임을 골라 주세요." : "탐험 지급 기록을 확인할 수 없어 출발과 수령을 잠시 멈췄어요.";
            _isOpen = true;
            DisplayRoomCameraInputGate.Push(this);
            _session.Enter(ClickerInputMode.Blocked, ClickerInputPriority.Modal, TryClose, true);
            Refresh();
        });
    }

    private void Close() => TryClose();
    private bool TryClose()
    {
        if (!_isOpen) return false;
        if (_departing) return true;
        ForceClose();
        return true;
    }

    private void ForceClose()
    {
        _isOpen = false;
        _departing = false;
        if (_party != null) _party.Clear();
        if (_destinationPresentation != null) _destinationPresentation.Show(null, _slimes);
        DisplayRoomCameraInputGate.Release(this);
        _session?.Exit(animated: false);
    }

    private void OnApplicationPause(bool paused) { if (paused) ForceClose(); }

    private void SelectDestination(int index)
    {
        if (!_isOpen || _departing || _party.IsAnimating || index == _destinationIndex ||
            _slimes.NormalCollectionCount < _destinations[index].CreateRules().RequiredCollectionCount) return;
        _party.ReturnAll(() =>
        {
            _destinationIndex = index;
            _notice.text = "아래 목록에서 함께 떠날 슬라임 3종을 골라 주세요.";
            Refresh();
        });
        Refresh();
    }

    private void ReturnSlot(int slot)
    {
        if (!_isOpen || _departing || _party.IsAnimating) return;
        _party.ReturnSlot(slot, Refresh);
        Refresh();
    }

    private void SelectMember(int index)
    {
        if (!_isOpen || _departing || _party.IsAnimating) return;
        SlimeInstance member = _candidates[index];
        if (!CanSelect(member) || FindRun(_destinations[_destinationIndex].CreateRules()) != null) return;
        if (!_party.TrySelect(member, (RectTransform)_memberImages[index].transform, _slimes, Refresh))
            _notice.text = "서로 다른 3종을 골라 주세요. 위 아이콘을 누르면 편성을 취소해요.";
        Refresh();
    }

    private ExpeditionRun FindRun(ExpeditionDestination rules)
    {
        ExpeditionRun matched = _slimes.ActiveExpeditions.FirstOrDefault(run => run.DestinationId == rules.DestinationId);
        if (matched != null || rules.Type != EExpeditionDestinationType.Short) return matched;
        // 기존 1팀 검증 저장의 목적지도 짧은 탐험 카드에서 수령할 수 있게 유지한다.
        return _slimes.ActiveExpeditions.FirstOrDefault(run =>
            !_destinations.Any(view => view.CreateRules().DestinationId == run.DestinationId));
    }

    private bool CanSelect(SlimeInstance member) => member != null &&
        member.Grade <= _slimes.HighestGrade && member.Location == ESlimeLocation.DisplayRoom &&
        !_slimes.IsOnExpedition(member.InstanceId) &&
        _slimes.NormalCollectionCount >= _destinations[_destinationIndex].CreateRules().RequiredCollectionCount;

    private List<SlimeInstance> GetSelected()
    {
        var ids = new HashSet<string>(_party.MemberIds);
        return _slimes.ActiveSlimes.Where(slime => ids.Contains(slime.InstanceId)).ToList();
    }

    private void OnAction()
    {
        if (!_isOpen || _departing || _party.IsAnimating || !GameplayGate.IsActive || TutorialManager.IsRunning || !_recoverySucceeded) return;
        DestinationView view = _destinations[_destinationIndex];
        ExpeditionDestination rules = view.CreateRules();
        ExpeditionRun run = FindRun(rules);
        if (run != null)
        {
            bool alreadyOwned = run.Reward.Decoration.HasValue &&
                _slimes.GetOwnedPlaygroundObjectCount(run.Reward.Decoration.Value) > 0;
            if (_service.TryClaim(run.RunId, out ExpeditionReward reward))
            {
                _notice.text = $"돌아왔어요! 포인트 {reward.Points:N0} · 뽑기권 {reward.GachaTickets}장" +
                    (reward.Decoration.HasValue ? $" · {DecorationName(reward.Decoration.Value)} 발견!" +
                        (alreadyOwned ? " (이미 보유 중이에요)" : string.Empty) : string.Empty);
            }
            else _notice.text = "아직 도착 전이거나 지급 기록을 확인할 수 없어요.";
        }
        else
        {
            if (_slimes.NormalCollectionCount < rules.RequiredCollectionCount) return;
            List<SlimeInstance> members = GetSelected();
            if (!rules.HasRequiredGrades(members)) return;
            ExpeditionReward reward = _rewards.Roll(members, rules.Duration, view.Tickets, view.DecorationChance);
            _departing = true;
            bool started = _service.TryStart(rules, members.Select(member => member.InstanceId).ToArray(), reward);
            _notice.text = started ? "탐험을 떠났어요. 돌아오면 보상을 받을 수 있어요." : "지금은 출발할 수 없어요. 편성과 탐험 상태를 다시 확인해 주세요.";
            if (started)
            {
                _group.interactable = false;
                _party.PlayDeparture(_destinationArea, () => { _departing = false; _group.interactable = true; Refresh(); });
            }
            else _departing = false;
        }
        Refresh();
    }

    private void Refresh()
    {
        if (_service == null) return;
        _enterButton.gameObject.SetActive(_slimes.IsInitialized && _slimes.IsGachaUnlocked && !_spaces.IsMainFieldActive);
        _enterButton.interactable = CanOpen();
        if (!_isOpen || _departing) return;
        bool busy = _party.IsAnimating;
        _closeButton.interactable = true;
        for (int i = 0; i < _destinations.Length; i++)
        {
            DestinationView item = _destinations[i];
            ExpeditionRun active = FindRun(item.CreateRules());
            bool unlockedTab = _slimes.NormalCollectionCount >= item.CreateRules().RequiredCollectionCount || active != null;
            item.Button.interactable = unlockedTab && !busy;
            item.Button.image.sprite = i == _destinationIndex ? _selectedDestinationTabSprite : _destinationTabSprite;
            item.Button.image.color = Color.white;
            item.Label.text = item.Name;
            item.TimeLabel.text = $"{2 << i}시간";
            item.LockRoot.SetActive(!unlockedTab);
            item.LockLabel.text = $"도감 {item.CreateRules().RequiredCollectionCount}종\n해금 필요";
        }
        DestinationView view = _destinations[_destinationIndex];
        ExpeditionDestination rules = view.CreateRules();
        ExpeditionRun run = FindRun(rules);
        if (run != null && !busy && _party.MemberIds.Any()) _party.Clear();
        _destinationImage.sprite = view.PreviewSprite;
        _destinationPresentation.Show(run, _slimes);
        List<SlimeInstance> selected = GetSelected();
        _memberHeading.text = _slimes.ActiveSlimes.Any(member => member.Location == ESlimeLocation.DisplayRoom)
            ? "장식장 슬라임 · 좌우로 밀어 골라요" : "장식장에 슬라임을 먼저 보관해 주세요.";
        for (int i = 0; i < _memberButtons.Length; i++)
        {
            ESlimeGrade grade = (ESlimeGrade)(i + 1);
            var owned = _slimes.ActiveSlimes.Where(member => member.Grade == grade && member.Location == ESlimeLocation.DisplayRoom).ToList();
            SlimeInstance candidate = owned.FirstOrDefault(member => _party.MemberIds.Contains(member.InstanceId)) ??
                owned.FirstOrDefault(CanSelect) ?? owned.FirstOrDefault();
            _candidates[i] = candidate;
            _memberButtons[i].gameObject.SetActive(candidate != null);
            if (candidate == null) continue;
            bool chosen = _party.MemberIds.Contains(candidate.InstanceId);
            _memberImages[i].sprite = _slimes.Get(grade).SpecData.Sprite;
            _memberLabels[i].text = (candidate.IsSpecial ? "특별 " : "") + _slimes.GetName(grade) +
                (chosen ? "\n편성됨" : _slimes.IsOnExpedition(candidate.InstanceId) ? "\n파견 중" : "");
            _memberButtons[i].interactable = !busy && run == null && !chosen && CanSelect(candidate);
        }
        bool unlocked = _slimes.NormalCollectionCount >= rules.RequiredCollectionCount;
        _party.Refresh(run, _slimes, !busy && run == null && unlocked);
        _nameText.text = view.Name;
        double points = run?.Reward.Points ?? _rewards.CalculatePoints(selected, rules.Duration);
        int tickets = run?.Reward.GachaTickets ?? view.Tickets;
        _detail.text = $"예상 획득 포인트 {points:N0}\n획득 뽑기권 {tickets}장 · 추가 기념품 {view.DecorationChance:P0}";
        _actionLabel.text = run == null ? "탐험 보내기" : run.IsReadyToReturn(ServerClock.TrustedUtcNow) ? "보상 받기" : $"탐험 중이에요. {Remaining(run)}";
        _actionButton.interactable = !busy && _recoverySucceeded && (run != null ? run.IsReadyToReturn(ServerClock.TrustedUtcNow) : unlocked && rules.HasRequiredGrades(selected));
    }

    private static string Remaining(ExpeditionRun run)
    {
        TimeSpan time = run.GetRemaining(ServerClock.TrustedUtcNow);
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private static string DecorationName(EPlaygroundObjectType type) => type switch
    {
        EPlaygroundObjectType.FlowerPatch => "꽃밭",
        EPlaygroundObjectType.FlowerCushion => "꽃 방석",
        EPlaygroundObjectType.FlowerBasket => "꽃바구니",
        EPlaygroundObjectType.FireflyLantern => "반딧불 랜턴",
        _ => "장식",
    };
}

