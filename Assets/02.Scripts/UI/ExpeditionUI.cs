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
        public ExpeditionDestination CreateRules() => new(Type, Array.Empty<ESlimeGrade>(), freeParty: true);
    }

    [SerializeField] private SlimeManager _slimes;
    [SerializeField] private CurrencyManager _currency;
    [SerializeField] private GameplaySpaceManager _spaces;
    [SerializeField] private SlimeSpawner _spawner;
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
    [SerializeField] private Button[] _memberButtons;
    [SerializeField] private Image[] _memberImages;
    [SerializeField] private TextMeshProUGUI[] _memberLabels;

    private readonly List<string> _selected = new();
    private readonly SlimeInstance[] _candidates = new SlimeInstance[20];
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
        if (_slimes == null || _currency == null || _spaces == null || _spawner == null || _game == null ||
            _info == null || _shop == null || _clicker == null || _exit == null || _hud == null ||
            _enterButton == null || _root == null || _group == null || _closeButton == null ||
            _actionButton == null || _actionLabel == null || _detail == null || _notice == null ||
            _destinations == null || _destinations.Length != 3 || _memberButtons.Length != 20 ||
            _memberImages.Length != 20 || _memberLabels.Length != 20)
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
            _memberButtons[i].onClick.AddListener(() => ToggleMember(index));
        }
        _slimes.ExpeditionChanged += Refresh;
        _spaces.SpaceChanged += OnSpaceChanged;
        _game.OnGameplayActivated += OnActivated;
        TutorialManager.Started += OnTutorialStarted;
        Refresh();
    }

    private void OnDestroy()
    {
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

    private void Update()
    {
        if (_service == null) return;
        if (_isOpen && (!GameplayGate.IsActive || TutorialManager.IsRunning || _spaces.IsTransitioning))
            TryClose();
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
    private void OnTutorialStarted() { TryClose(); Refresh(); }
    private void OnSpaceChanged(EGameplaySpace space) { TryClose(); Refresh(); }

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
            _selected.Clear();
            _notice.text = _recoverySucceeded ? "탐험에 보낼 슬라임을 골라 주세요." : "탐험 지급 기록을 확인할 수 없어 출발과 수령을 잠시 멈췄어요.";
            _isOpen = true;
            _session.Enter(ClickerInputMode.Blocked, ClickerInputPriority.Modal, TryClose, true);
            Refresh();
        });
    }

    private void Close() => TryClose();
    private bool TryClose()
    {
        if (!_isOpen) return false;
        _isOpen = false;
        _selected.Clear();
        _session.Exit(animated: false);
        return true;
    }

    private void SelectDestination(int index)
    {
        _destinationIndex = index;
        _selected.Clear();
        _notice.text = "필요한 종류를 포함해 최대 3마리까지 보낼 수 있어요.";
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

    private bool CanSelect(SlimeInstance slime)
    {
        if (slime == null || slime.Grade > _slimes.HighestGrade || slime.Location != ESlimeLocation.DisplayRoom || _slimes.IsOnExpedition(slime.InstanceId)) return false;
        SlimeController target = _spawner.GetActiveTargets().FirstOrDefault(item => item != null && item.InstanceId == slime.InstanceId);
        return target != null && !target.IsDragging && !target.IsPresentationLocked;
    }

    private List<SlimeInstance> GetSelected() => _slimes.ActiveSlimes.Where(slime => _selected.Contains(slime.InstanceId)).ToList();

    private void ToggleMember(int index)
    {
        SlimeInstance candidate = _candidates[index];
        if (candidate == null || !CanSelect(candidate)) return;
        if (!_selected.Remove(candidate.InstanceId))
        {
            if (_selected.Count >= ExpeditionDestination.MaxMembers)
            {
                _notice.text = "탐험대는 최대 3마리까지 편성할 수 있어요.";
                return;
            }
            _selected.Add(candidate.InstanceId);
        }
        Refresh();
    }

    private void OnAction()
    {
        if (!_isOpen || !GameplayGate.IsActive || TutorialManager.IsRunning || !_recoverySucceeded) return;
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
            if (!rules.HasRequiredGrades(members) || members.Any(member => !CanSelect(member))) return;
            ExpeditionReward reward = _rewards.Roll(members, rules.Duration, view.Tickets, view.DecorationChance);
            bool started = _service.TryStart(rules, members.Select(member => member.InstanceId).ToArray(), reward);
            _notice.text = started ? "탐험을 떠났어요. 돌아오면 보상을 받을 수 있어요." : "지금은 출발할 수 없어요. 편성과 탐험 상태를 다시 확인해 주세요.";
            if (started) _selected.Clear();
        }
        Refresh();
    }

    private void Refresh()
    {
        if (_service == null) return;
        _enterButton.gameObject.SetActive(_slimes.IsInitialized && _slimes.IsGachaUnlocked && !_spaces.IsMainFieldActive);
        _enterButton.interactable = CanOpen();
        if (!_isOpen) return;
        for (int i = 0; i < _destinations.Length; i++)
        {
            DestinationView item = _destinations[i];
            ExpeditionRun active = FindRun(item.CreateRules());
            string status = active == null ? (_slimes.NormalCollectionCount < item.CreateRules().RequiredCollectionCount ? $"도감 {item.CreateRules().RequiredCollectionCount}종에 열려요" : "편성하기") : active.IsReadyToReturn(ServerClock.TrustedUtcNow) ? "보상 받기" : Remaining(active);
            item.Button.image.color = i == _destinationIndex ? new Color(.72f, .83f, .63f) : new Color(.89f, .82f, .66f);
            item.Label.text = (i == _destinationIndex ? "선택 · " : "") + item.Name + "\n" + (2 << i) + "시간 · " + status;
        }
        DestinationView view = _destinations[_destinationIndex];
        ExpeditionDestination rules = view.CreateRules();
        ExpeditionRun run = FindRun(rules);
        _selected.RemoveAll(id => !_slimes.ActiveSlimes.Any(slime => slime.InstanceId == id && CanSelect(slime)));
        List<SlimeInstance> selected = GetSelected();
        for (int i = 0; i < _memberButtons.Length; i++)
        {
            ESlimeGrade grade = (ESlimeGrade)(i + 1);
            var owned = _slimes.ActiveSlimes.Where(slime => slime.Grade == grade && slime.Location == ESlimeLocation.DisplayRoom).ToList();
            SlimeInstance candidate = owned.FirstOrDefault(slime => _selected.Contains(slime.InstanceId)) ??
                owned.FirstOrDefault(CanSelect) ?? owned.FirstOrDefault();
            _candidates[i] = candidate;
            SlimeSpecData spec = _slimes.Get(grade).SpecData;
            _memberImages[i].sprite = spec.Sprite;
            _memberImages[i].color = candidate == null ? new Color(0f, 0f, 0f, 0.35f) : Color.white;
            bool chosen = candidate != null && _selected.Contains(candidate.InstanceId);
            _memberLabels[i].text = $"Lv.{i + 1}" + (candidate?.IsSpecial == true ? " 특별" : "") + "\n" +
                (chosen ? "편성됨" : candidate == null ? "장식장에 없어요" : _slimes.IsOnExpedition(candidate.InstanceId) ? "파견 중" : "선택하기");
            _memberButtons[i].interactable = run == null && _slimes.NormalCollectionCount >= rules.RequiredCollectionCount && CanSelect(candidate);
        }
        string required = $"최고 Lv.{(int)_slimes.HighestGrade} 이하 · 서로 다른 3종";
        string party = run == null ? $"편성 {selected.Count}/3마리" : "함께 간 슬라임: " + string.Join(" · ", run.Members.Select(member => $"Lv.{(int)member.Grade}"));
        double points = run?.Reward.Points ?? _rewards.CalculatePoints(selected, rules.Duration);
        int tickets = run?.Reward.GachaTickets ?? view.Tickets;
        bool unlocked = _slimes.NormalCollectionCount >= rules.RequiredCollectionCount;
        _detail.text = (unlocked ? "" : $"도감 {rules.RequiredCollectionCount}종 등록 시 열려요. 현재 {_slimes.NormalCollectionCount}종이에요.\n\n") + $"{view.Name}\n필요한 슬라임: {required}\n{party}\n\n포인트 {points:N0} · 뽑기권 {tickets}장\n장식 발견 확률 {view.DecorationChance:P0}\n" +
            (run == null ? "출발할 때의 자동 생산량으로 보상이 정해져요." : run.IsReadyToReturn(ServerClock.TrustedUtcNow) ? "탐험을 마쳤어요. 보상을 받아 주세요." : $"돌아오기까지 {Remaining(run)} 남았어요.") +
            "\n이미 가진 장식은 보유 수가 늘지 않아요.";
        _actionLabel.text = run == null ? "탐험 보내기" : run.IsReadyToReturn(ServerClock.TrustedUtcNow) ? "보상 받기" : "탐험 중이에요";
        _actionButton.interactable = _recoverySucceeded && (run != null ? run.IsReadyToReturn(ServerClock.TrustedUtcNow) : unlocked && rules.HasRequiredGrades(selected));
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

