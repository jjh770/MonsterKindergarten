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
        [Tooltip("보상을 받을 수 있을 때 탭에 띄우는 느낌표입니다.")]
        public GameObject AlertRoot;

        // 규칙은 Type으로만 정해지고 바뀌지 않으므로 한 번 만들어 둔다.
        [NonSerialized] private ExpeditionDestination _rules;

        public ExpeditionDestination Rules => _rules ??= new ExpeditionDestination(Type, Array.Empty<ESlimeGrade>(), freeParty: true);
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
    [Tooltip("보상을 받을 수 있을 때 보상 받기 버튼에 띄우는 느낌표입니다.")]
    [SerializeField] private GameObject _actionAlert;
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
    [SerializeField] private ExpeditionRewardPopupUI _rewardPopup;
    [SerializeField] private ExpeditionRewardFlight _rewardFlight;
    [Tooltip("탐험 화면 본체가 열릴 때 튀어 오르고 닫힐 때 움츠러드는 공용 팝업 연출입니다.")]
    [SerializeField] private PopupMotion _panelMotion;
    private readonly SlimeInstance[] _candidates = new SlimeInstance[20];
    private readonly List<ExpeditionPartyPresentation.ReturningMember> _returning = new(3);
    private bool _departing;
    private ExpeditionService _service;
    private ExpeditionRewardCalculator _rewards;
    private GameplayModeSession _session;
    private int _destinationIndex;
    private bool _isOpen;
    private bool _recoveryAttempted;
    private bool _recoverySucceeded;
    private float _nextRefresh;
    // 안내 문구가 고른 탐험지의 상태를 따라가는 중이다. 보상, 출발, 오류처럼 일이 생겨 띄운 안내는
    // 따라가지 않고 그대로 둔다.
    private bool _noticeFollowsState;

    private DestinationView SelectedView => _destinations[_destinationIndex];

    // 탐험 튜토리얼이 가리키는 자리들이다.
    public RectTransform EnterButtonTarget => _enterButton != null ? (RectTransform)_enterButton.transform : null;
    public RectTransform DestinationTabsTarget =>
        _destinations != null && _destinations.Length > 0 && _destinations[0].Button != null
            ? _destinations[0].Button.transform.parent as RectTransform
            : null;
    public RectTransform MemberListTarget =>
        _memberButtons != null && _memberButtons.Length > 0 && _memberButtons[0] != null
            ? _memberButtons[0].transform.parent as RectTransform
            : null;
    public RectTransform ActionButtonTarget => _actionButton != null ? (RectTransform)_actionButton.transform : null;
    public bool IsOpen => _isOpen;
    // 화면이 열리고 본체가 튀어 오르기 시작한 순간이다.
    public event Action Opened;

    // 다른 튜토리얼이 도는 동안은 화면을 열지 않고, 열려 있으면 닫는다. 탐험 튜토리얼만은 이 화면을
    // 직접 열어 보여 주므로 예외다.
    private static bool IsBlockedByTutorial =>
        TutorialManager.IsRunning && !TutorialManager.IsActive(TutorialIds.Expedition);

    private void Start()
    {
        if (_slimes == null || _currency == null || _spaces == null || _game == null ||
            _info == null || _shop == null || _clicker == null || _exit == null || _hud == null ||
            _enterButton == null || _root == null || _group == null || _closeButton == null ||
            _actionButton == null || _actionLabel == null || _detail == null || _notice == null ||
            _destinations == null || _destinations.Length != 3 || _party == null ||
            _destinationImage == null || _destinationArea == null || _destinationPresentation == null ||
            _memberHeading == null || _memberButtons == null || _memberButtons.Length != 20 || _memberImages == null || _memberImages.Length != 20 ||
            _memberLabels == null || _memberLabels.Length != 20 || _nameText == null ||
            _rewardPopup == null || _rewardFlight == null || _panelMotion == null || _actionAlert == null ||
            _destinations.Any(view => view.AlertRoot == null))
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
        if (_isOpen && (!GameplayGate.IsActive || IsBlockedByTutorial || _spaces.IsTransitioning))
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

    private void OnActivated()
    {
        _recoveryAttempted = false;
        Refresh();
    }

    private void OnTutorialStarted()
    {
        if (!IsBlockedByTutorial) return;
        ForceClose();
        Refresh();
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        ForceClose();
        Refresh();
    }

    private bool CanOpen() => _slimes.IsInitialized && _slimes.IsGachaUnlocked &&
        !_spaces.IsMainFieldActive && !_spaces.IsTransitioning && GameplayGate.IsActive &&
        !IsBlockedByTutorial && !_info.IsVisible;

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
            // 받을 보상이 있으면 그 탐험지를 바로 보여 준다.
            int ready = FindReadyDestination();
            if (ready >= 0) _destinationIndex = ready;
            if (_recoverySucceeded) ShowStateNotice();
            else ShowEventNotice(UiMessages.ExpeditionSettlementUnavailable);
            _isOpen = true;
            DisplayRoomCameraInputGate.Push(this);
            _session.Enter(ClickerInputMode.Blocked, ClickerInputPriority.Modal, TryClose, true);
            _panelMotion.PlayOpen();
            Refresh();
            Opened?.Invoke();
        });
    }

    private void Close() => TryClose();

    private bool TryClose()
    {
        if (!_isOpen) return false;
        // 보상 팝업이 떠 있으면 뒤로 가기는 받기와 같다.
        if (_rewardPopup.IsVisible)
        {
            _rewardPopup.Confirm();
            return true;
        }

        if (_departing) return true;
        CloseAnimated();
        return true;
    }

    // 닫기 버튼, 뒤로 가기, 보상 받기로 닫을 때다. 본체가 움츠러들며 화면이 흐려진다. 일시정지나
    // 튜토리얼처럼 바로 치워야 하는 경로는 ForceClose를 쓴다. 닫히는 동안 다시 열기는 Open이
    // _session.IsTransitioning으로 막는다.
    private void CloseAnimated()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _departing = false;
        _group.interactable = false;
        _rewardPopup.HideImmediate();
        DisplayRoomCameraInputGate.Release(this);
        _panelMotion.PlayClose();
        _session.Exit(animated: true, onCompleted: () =>
        {
            _party.Clear();
            _destinationPresentation.Show(null, _slimes);
        });
    }

    private void ForceClose()
    {
        _isOpen = false;
        _departing = false;
        if (_rewardPopup != null) _rewardPopup.HideImmediate();
        if (_party != null) _party.Clear();
        if (_destinationPresentation != null) _destinationPresentation.Show(null, _slimes);
        DisplayRoomCameraInputGate.Release(this);
        _session?.Exit(animated: false);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) ForceClose();
    }

    private void SelectDestination(int index)
    {
        if (!_isOpen || _departing || _party.IsAnimating || index == _destinationIndex ||
            _slimes.NormalCollectionCount < _destinations[index].Rules.RequiredCollectionCount) return;
        _party.ReturnAll(() =>
        {
            _destinationIndex = index;
            ShowStateNotice();
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
        if (member == null || FindRun(SelectedView.Rules) != null) return;
        // 이미 탐험지에 들어간 슬라임을 다시 누르면 목록으로 돌아온다.
        if (_party.TryReturnMember(member.InstanceId, Refresh))
        {
            Refresh();
            return;
        }

        if (!CanSelect(member)) return;
        if (!_party.TrySelect(member, (RectTransform)_memberImages[index].transform, _slimes, Refresh))
            ShowEventNotice(UiMessages.ExpeditionPickDifferentKinds);
        Refresh();
    }

    // 탐험지 화면에서 보이던 슬라임의 그림, 자리, 크기다. 보이지 않는 자리는 탐험지 가운데에서 출발한다.
    private void CaptureReturn(ExpeditionRun run)
    {
        _returning.Clear();
        for (int i = 0; i < run.Members.Count && i < ExpeditionDestination.MaxMembers; i++)
        {
            Sprite sprite = _slimes.Get(run.Members[i].Grade).SpecData.Sprite;
            // 돌아갈 곳은 아래 장식장 목록의 같은 단계 자리다. 목록에 없으면 그 자리에서 사라진다.
            int home = (int)run.Members[i].Grade - 1;
            Image homeImage = home >= 0 && home < _memberImages.Length && _memberButtons[home].gameObject.activeSelf
                ? _memberImages[home] : null;
            if (!_destinationPresentation.TryGetVisitor(i, out Vector3 position, out Vector2 size))
            {
                position = _destinationArea.TransformPoint(Vector3.zero);
                size = new Vector2(120f, 120f);
            }

            _returning.Add(new ExpeditionPartyPresentation.ReturningMember(sprite, position, size, homeImage));
        }
    }

    private ExpeditionRun FindRun(ExpeditionDestination rules)
    {
        ExpeditionRun matched = _slimes.ActiveExpeditions.FirstOrDefault(run => run.DestinationId == rules.DestinationId);
        if (matched != null || rules.Type != EExpeditionDestinationType.Short) return matched;
        // 기존 1팀 검증 저장의 목적지도 짧은 탐험 카드에서 수령할 수 있게 유지한다.
        return _slimes.ActiveExpeditions.FirstOrDefault(run =>
            !_destinations.Any(view => view.Rules.DestinationId == run.DestinationId));
    }

    private static bool IsReady(ExpeditionRun run) =>
        run != null && run.IsReadyToReturn(ServerClock.TrustedUtcNow);

    private bool CanSelect(SlimeInstance member) => member != null &&
        member.Grade <= _slimes.HighestGrade && member.Location == ESlimeLocation.DisplayRoom &&
        !_slimes.IsOnExpedition(member.InstanceId) &&
        _slimes.NormalCollectionCount >= SelectedView.Rules.RequiredCollectionCount;

    // 편성 칸 순서대로 돌려준다. 출발한 탐험이 이 순서로 저장돼야 탐험지 화면이 슬라임들을 서 있던
    // 자리에서 이어받는다.
    private List<SlimeInstance> GetSelected()
    {
        var selected = new List<SlimeInstance>(ExpeditionDestination.MaxMembers);
        foreach (string id in _party.MemberIds)
        {
            SlimeInstance slime = _slimes.ActiveSlimes.FirstOrDefault(active => active.InstanceId == id);
            if (slime != null) selected.Add(slime);
        }

        return selected;
    }

    private void OnAction()
    {
        if (!_isOpen || _departing || _party.IsAnimating || !GameplayGate.IsActive || TutorialManager.IsRunning || !_recoverySucceeded) return;
        DestinationView view = SelectedView;
        ExpeditionRun run = FindRun(view.Rules);
        if (run != null) Claim(view, run);
        else Depart(view);
        Refresh();
    }

    private void Claim(DestinationView view, ExpeditionRun run)
    {
        bool alreadyOwned = run.Reward.Decoration.HasValue &&
            _slimes.GetOwnedPlaygroundObjectCount(run.Reward.Decoration.Value) > 0;
        // 수령하면 탐험이 목록에서 사라져 탐험지 화면이 비므로, 귀환 연출이 출발할 자리를 먼저 읽는다.
        // 수령 중에 오는 ExpeditionChanged가 화면을 비우지 않도록 출발처럼 _departing을 먼저 세운다.
        CaptureReturn(run);
        _departing = true;
        if (!_service.TryClaim(run.RunId, out ExpeditionReward reward))
        {
            _departing = false;
            ShowEventNotice(UiMessages.ExpeditionCannotClaim);
            return;
        }

        ShowEventNotice(BuildClaimNotice(reward, alreadyOwned));
        _group.interactable = false;
        _actionAlert.SetActive(false);
        _destinationPresentation.Show(null, _slimes);
        string destinationName = view.Name;
        // 돌아온 뒤 보상 팝업을 띄우고, 받으면 화면을 닫아 숨겨 둔 상단 바로 보상을 날린다.
        // 팝업도 이 화면의 CanvasGroup 아래라, 막아 둔 입력을 팝업을 띄우기 전에 푼다. 뒤의 화면은
        // 팝업의 블로커가 가리고, _departing이 그대로라 탭과 버튼도 반응하지 않는다.
        _party.PlayReturn(_destinationArea, _returning, () =>
        {
            _group.interactable = true;
            _rewardPopup.Show(destinationName, reward, alreadyOwned, (pointFrom, ticketFrom) =>
            {
                CloseAnimated();
                _rewardFlight.Play(pointFrom, ticketFrom, reward.GachaTickets);
            });
        });
    }

    // 가운데점(·)은 두 글꼴 모두에 없어 빈칸으로 그려진다. 글꼴에 있는 |를 쓴다.
    private static string BuildClaimNotice(ExpeditionReward reward, bool alreadyOwned)
    {
        string notice = $"돌아왔어요! 포인트 {reward.Points:N0} | 뽑기권 {reward.GachaTickets}장";
        if (!reward.Decoration.HasValue) return notice;

        notice += $" | {ExpeditionRewardPopupUI.DecorationName(reward.Decoration.Value)} 발견!";
        return alreadyOwned ? notice + " (이미 보유 중이에요)" : notice;
    }

    private void Depart(DestinationView view)
    {
        ExpeditionDestination rules = view.Rules;
        if (_slimes.NormalCollectionCount < rules.RequiredCollectionCount) return;
        List<SlimeInstance> members = GetSelected();
        if (!rules.HasRequiredGrades(members)) return;
        ExpeditionReward reward = _rewards.Roll(members, rules.Duration, view.Tickets, view.DecorationChance);
        _departing = true;
        if (!_service.TryStart(rules, members.Select(member => member.InstanceId).ToArray(), reward))
        {
            _departing = false;
            ShowEventNotice(UiMessages.ExpeditionCannotDepart);
            return;
        }

        ShowEventNotice(UiMessages.ExpeditionDeparted);
        _group.interactable = false;
        // 출발 안내는 연출 동안만 두고, 끝나면 탐험지 상태(탐험 중, 도착)를 따라가게 한다.
        _party.PlayDeparture(_destinationArea, () =>
        {
            _departing = false;
            _group.interactable = true;
            ShowStateNotice();
            Refresh();
        });
    }

    private void Refresh()
    {
        if (_service == null) return;
        _enterButton.gameObject.SetActive(_slimes.IsInitialized && _slimes.IsGachaUnlocked && !_spaces.IsMainFieldActive);
        _enterButton.interactable = CanOpen();
        if (!_isOpen || _departing) return;
        bool busy = _party.IsAnimating;
        _closeButton.interactable = true;
        RefreshTabs(busy);

        DestinationView view = SelectedView;
        ExpeditionDestination rules = view.Rules;
        ExpeditionRun run = FindRun(rules);
        if (run != null && !busy && _party.MemberIds.Any()) _party.Clear();
        _destinationImage.sprite = view.PreviewSprite;
        _destinationPresentation.Show(run, _slimes);
        RefreshMembers(run, busy);
        bool unlocked = _slimes.NormalCollectionCount >= rules.RequiredCollectionCount;
        _party.Refresh(run, !busy && run == null && unlocked);
        RefreshDetail(view, run, unlocked, busy);
        if (_noticeFollowsState) _notice.text = GetStateNotice();
    }

    private void RefreshTabs(bool busy)
    {
        for (int i = 0; i < _destinations.Length; i++)
        {
            DestinationView item = _destinations[i];
            ExpeditionRun active = FindRun(item.Rules);
            bool unlockedTab = _slimes.NormalCollectionCount >= item.Rules.RequiredCollectionCount || active != null;
            item.Button.interactable = unlockedTab && !busy;
            item.Button.image.sprite = i == _destinationIndex ? _selectedDestinationTabSprite : _destinationTabSprite;
            item.Button.image.color = Color.white;
            item.Label.text = item.Name;
            // 비어 있으면 걸리는 시간, 떠나 있으면 남은 시간, 돌아왔으면 도착을 보여 준다.
            bool returned = IsReady(active);
            item.TimeLabel.text = active == null
                ? $"{(int)item.Rules.Duration.TotalHours}시간"
                : returned ? UiMessages.ExpeditionArrived : Remaining(active);
            item.AlertRoot.SetActive(returned);
            item.LockRoot.SetActive(!unlockedTab);
            item.LockLabel.text = $"도감 {item.Rules.RequiredCollectionCount}종\n해금 필요";
        }
    }

    private void RefreshMembers(ExpeditionRun run, bool busy)
    {
        _memberHeading.text = _slimes.ActiveSlimes.Any(member => member.Location == ESlimeLocation.DisplayRoom)
            ? UiMessages.ExpeditionMemberHeading : UiMessages.ExpeditionNoMembers;
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
            bool away = _slimes.IsOnExpedition(candidate.InstanceId);
            // 편성됐거나 탐험을 떠난 슬라임은 위로 올라간 것이라 자리를 빈자리처럼 회색으로 둔다.
            if (chosen || away) _party.ApplyEmptyLook(_memberImages[i]);
            else
            {
                _memberImages[i].sprite = _slimes.Get(grade).SpecData.Sprite;
                _memberImages[i].material = null;
                _memberImages[i].color = Color.white;
            }
            string status = chosen ? UiMessages.ExpeditionMemberChosen : away ? UiMessages.ExpeditionMemberAway : null;
            _memberLabels[i].text = ExpeditionPartyPresentation.MemberName(_slimes, grade, candidate.IsSpecial) +
                (status == null ? "" : "\n" + status);
            _memberButtons[i].interactable = !busy && run == null && (chosen || CanSelect(candidate));
        }
    }

    private void RefreshDetail(DestinationView view, ExpeditionRun run, bool unlocked, bool busy)
    {
        ExpeditionDestination rules = view.Rules;
        List<SlimeInstance> selected = GetSelected();
        bool ready = IsReady(run);
        _nameText.text = BuildTitle(view, run, selected.Count);
        double points = run?.Reward.Points ?? _rewards.CalculatePoints(selected, rules.Duration);
        int tickets = run?.Reward.GachaTickets ?? view.Tickets;
        _detail.text = $"예상 획득 포인트 {points:N0}\n획득 뽑기권 {tickets}장 | 소품 1종 {view.DecorationChance:P0}";
        _actionLabel.text = run == null ? UiMessages.ExpeditionSend : ready ? UiMessages.ExpeditionClaim : $"탐험 중이에요. {Remaining(run)}";
        _actionButton.interactable = !busy && _recoverySucceeded && (run != null ? ready : unlocked && rules.HasRequiredGrades(selected));
        _actionAlert.SetActive(ready);
    }

    // 탐험지 이름 아래 작은 줄에 상태를 적는다. 비어 있으면 편성한 수(편성 0/3), 떠나 있으면
    // 탐험 중, 돌아왔으면 도착이다.
    private static string BuildTitle(DestinationView view, ExpeditionRun run, int selectedCount)
    {
        if (run == null)
            return $"{view.Name}\n<size=35>편성 {selectedCount}/{ExpeditionDestination.MaxMembers}</size>";

        return string.Format(IsReady(run) ? UiMessages.ExpeditionTitleArrived : UiMessages.ExpeditionTitleInProgress, view.Name);
    }

    private void ShowStateNotice()
    {
        _noticeFollowsState = true;
        _notice.text = GetStateNotice();
    }

    private void ShowEventNotice(string text)
    {
        _noticeFollowsState = false;
        _notice.text = text;
    }

    // 고른 탐험지가 비어 있으면 편성 안내, 떠나 있으면 탐험 중, 돌아왔으면 보상을 받으라는 안내다.
    private string GetStateNotice()
    {
        DestinationView view = SelectedView;
        ExpeditionRun run = FindRun(view.Rules);
        if (run == null) return UiMessages.ExpeditionChooseParty;

        return IsReady(run)
            ? UiMessages.ExpeditionReadyToClaim
            : string.Format(UiMessages.ExpeditionInProgress, view.Name);
    }

    // 보상을 받을 수 있는 첫 탐험지다. 없으면 -1이다.
    private int FindReadyDestination()
    {
        for (int i = 0; i < _destinations.Length; i++)
        {
            if (IsReady(FindRun(_destinations[i].Rules))) return i;
        }

        return -1;
    }

    // 탐험지 이름은 이 화면의 목적지 카드에만 있다. 알 수 없는 목적지는 기존 1팀 저장의 것이라 짧은
    // 탐험 카드로 수령하므로(FindRun) 그 이름을 쓴다.
    public string GetDestinationName(string destinationId)
    {
        if (_destinations == null || _destinations.Length == 0) return string.Empty;

        foreach (DestinationView view in _destinations)
        {
            if (view.Rules.DestinationId == destinationId) return view.Name;
        }

        return _destinations[0].Name;
    }

    private static string Remaining(ExpeditionRun run)
    {
        TimeSpan time = run.GetRemaining(ServerClock.TrustedUtcNow);
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

}
