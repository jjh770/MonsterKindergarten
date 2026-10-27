using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 뽑기 기계 화면 한 번을 이끈다. 화면을 열고, 1회·5회 버튼을 기다리고, 눌리면 티켓을 쓰고 결과를 만들어
// 기계로 보여 주고, 결과를 필드로 보낸 뒤 닫는다. 기계 그림과 캡슐 움직임은 GachaMachineView가,
// 버튼과 토스트는 GachaPullPanel이, 1회 결과 슬라임은 GachaSingleResult가, 여러 개를 뽑았을 때 슬라임이
// 머무는 자리는 GachaResultSlots가 맡고, 여기서는 순서를 정한다.
//
// 티켓은 버튼을 누르는 순간 쓰이고 결과가 만들어져 저장된다. 연출은 그 뒤에 보여 주기만 해서, 도중에
// 앱이 꺼져도 잃는 것이 없고 닫기 버튼은 그 전까지만 의미가 있다. 빛의 색은 확정된 희귀도를 표현할 뿐이다.
//
// 이 화면이 열려 있는 동안 자연 스폰은 정지 요청으로 멈춘다. 플레이어의 자동 스폰 설정은 건드리지 않는다.
// 정지는 닫기, 끝, 파괴 어느 길로 끝나도 풀린다.
public sealed class GachaResultDirector : MonoBehaviour
{
    private enum SessionState
    {
        Closed,
        Choosing,
        Presenting,
    }

    private const int RequestNone = -3;
    private const int RequestCancelled = -2;
    private const int RequestClose = -1;
    private const int RequestOpenAll = -4;

    private static string CapsuleTapMessage => UiMessages.MachineCapsuleTap;

    private static readonly Color CommonColor = new(0.35f, 1f, 0.72f, 1f);
    private static readonly Color UncommonColor = new(0.32f, 0.78f, 1f, 1f);
    private static readonly Color RareColor = new(0.67f, 0.42f, 1f, 1f);
    private static readonly Color JackpotColor = new(1f, 0.76f, 0.22f, 1f);

    private static string SpecialSubtitle => UiMessages.SpecialSlimeSubtitle;

    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _canvasGroup;
    [Tooltip("하나만 뽑았을 때 결과 슬라임이 솟고 필드로 날아가는 연출입니다.")]
    [SerializeField] private GachaSingleResult _singleResult;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private CurrencyManager _currencyManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private GameExitManager _gameExitManager;

    [Header("Machine")]
    [SerializeField] private GachaMachineView _machine;
    [Tooltip("버튼을 고르는 동안 통을 잡고 흔들게 하는 컴포넌트입니다. 기계 오브젝트에 있습니다.")]
    [SerializeField] private GachaMachineGrab _machineGrab;
    [SerializeField] private GachaPullPanel _pullPanel;
    [SerializeField] private Button _tapButton;
    [SerializeField] private TMP_Text _tapPrompt;

    [Header("Slots")]
    [Tooltip("여러 개를 뽑았을 때 열린 슬라임이 머물다 필드로 날아가는 자리들입니다.")]
    [SerializeField] private GachaResultSlots _resultSlots;
    [Tooltip("여러 개를 뽑았을 때 남은 캡슐을 차례로 모두 여는 버튼입니다. 캡슐을 고르는 동안에만 보입니다.")]
    [SerializeField] private Button _openAllButton;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float _holdDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float _moveDuration = 0.5f;

    [Header("Look")]
    [SerializeField, Min(0f)] private float _moveArcRadius = 160f;
    [SerializeField, Min(0f)] private float _moveSpinTurns = 3f;

    private readonly List<SlimeController> _pulled = new();
    private RectTransform _overlayRect;
    private SessionState _state;
    private int _pendingRequest = RequestNone;
    private bool _isReady;
    private bool _isPlaying;
    private bool _tapped;
    private bool _openAllRequested;
    private bool _isSpecialResult;

    public bool IsPlaying => _isPlaying;

    // 티켓이 쓰이고 결과가 만들어져 저장된 순간이다. 이 뒤로는 되돌릴 수 없다.
    public event Action PullCommitted;

    private void Awake()
    {
        if (_root == null || _canvasGroup == null || _singleResult == null ||
            _machine == null || _machineGrab == null ||
            _pullPanel == null || _tapButton == null || _tapPrompt == null ||
            _openAllButton == null ||
            _slimeManager == null || _currencyManager == null || _spawnManager == null ||
            _resultSlots == null)
        {
            Debug.LogError("뽑기 기계 연출의 필수 참조가 비어 있습니다.", this);
            return;
        }

        _overlayRect = _singleResult.OverlayRect;
        _isReady = _overlayRect != null;

        _tapButton.onClick.AddListener(OnTapped);
        _openAllButton.onClick.AddListener(OnOpenAllClicked);
        _pullPanel.PullRequested += OnPullRequested;
        _pullPanel.CloseRequested += OnCloseRequested;
        _pullPanel.BuyRequested += OnBuyRequested;
        _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_tapButton != null) _tapButton.onClick.RemoveListener(OnTapped);
        if (_openAllButton != null) _openAllButton.onClick.RemoveListener(OnOpenAllClicked);
        if (_pullPanel != null)
        {
            _pullPanel.PullRequested -= OnPullRequested;
            _pullPanel.CloseRequested -= OnCloseRequested;
            _pullPanel.BuyRequested -= OnBuyRequested;
        }

        AudioManager.Instance?.StopLoopingSFX();
        _clicker?.ReleaseMode(this);
    }

    // 뽑기 기계 화면을 연다. 닫히면 뽑은 슬라임 목록과 함께 onFinished가 불린다. 아무것도 뽑지 않고 닫았으면
    // 목록이 비어 있다.
    public void Open(Action<IReadOnlyList<SlimeController>> onFinished)
    {
        if (!_isReady || _isPlaying)
        {
            onFinished?.Invoke(Array.Empty<SlimeController>());
            return;
        }

        RunSession(onFinished).Forget();
    }

    private async UniTaskVoid RunSession(Action<IReadOnlyList<SlimeController>> onFinished)
    {
        _isPlaying = true;
        _pulled.Clear();
        CancellationToken token = this.GetCancellationTokenOnDestroy();
        _spawnManager.PushSpawnPause(this);
        _clicker?.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        _gameExitManager?.RegisterBackHandler(this, OnBackRequested);
        bool isCancelled = false;
        try
        {
            isCancelled = await Session(token);
        }
        finally
        {
            AudioManager.Instance?.StopLoopingSFX();
            _gameExitManager?.UnregisterBackHandler(this);
            _spawnManager?.ReleaseSpawnPause(this);
            _clicker?.ReleaseMode(this);
            _singleResult.StopShimmer();
            _resultSlots.StopShimmer();
            _state = SessionState.Closed;
            _pendingRequest = RequestNone;
            _isPlaying = false;
        }

        if (isCancelled) return;

        foreach (SlimeController slime in _pulled)
        {
            if (slime != null) slime.SetLocationPresentationActive(true);
        }

        onFinished?.Invoke(_pulled);
    }

    private async UniTask<bool> Session(CancellationToken token)
    {
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        _root.SetActive(true);
        ResetPresentation();

        if (await Fade(0f, 1f, _fadeDuration, token)) return true;
        if (await _machine.Appear(token)) return true;

        bool isTutorial = IsTutorialPull();
        _state = SessionState.Choosing;
        int tickets = GetTicketCount();
        _pullPanel.Show(tickets, isTutorial);
        if (!isTutorial) RefreshBuyOffer();
        ShowPrompt(GetChoosePrompt(tickets, isTutorial));
        // 튜토리얼은 스포트라이트가 1회 버튼만 열어 두므로 통을 잡을 수 없다. 잡으라는 떨림도 띄우지 않는다.
        _machineGrab.SetGrabbable(!isTutorial);

        List<GachaPullItem> items = new();
        int pressed = 0;
        while (items.Count == 0)
        {
            int request = await WaitForRequest(isTutorial, token);
            if (request == RequestCancelled) return true;

            if (request == RequestClose)
            {
                _machineGrab.SetGrabbable(false);
                _state = SessionState.Presenting;
                _tapPrompt.gameObject.SetActive(false);
                _pullPanel.Hide();
                if (await Fade(1f, 0f, _fadeDuration, token)) return true;

                CloseOverlay();
                return false;
            }

            EGachaFailure failure = GachaService.TryPullMany(request, items, isTutorial);
            if (failure == EGachaFailure.None)
            {
                pressed = request;
                break;
            }

            _pullPanel.ShowToast(GetFailureMessage(failure));
        }

        if (await _machineGrab.ReturnToRest(token)) return true;

        return await Present(items, pressed, token);
    }

    // 버튼이 눌려 티켓이 쓰였다. 이 뒤의 모든 것은 이미 정해진 결과를 보여 주는 일이다.
    private async UniTask<bool> Present(
        List<GachaPullItem> items,
        int pressedCount,
        CancellationToken token)
    {
        _state = SessionState.Presenting;
        int count = items.Count;
        for (int i = 0; i < count; i++)
        {
            SlimeController slime = items[i].Slime;
            _pulled.Add(slime);
            slime.SetLocationPresentationActive(false);
        }

        _pullPanel.SetCommitted(pressedCount);
        _tapPrompt.gameObject.SetActive(false);
        PullCommitted?.Invoke();
        _machine.BeginPull(count);

        bool isStack = pressedCount == GachaPullPanel.MultiCount;
        if (await _machine.InsertTicket(
                _pullPanel.GetIcon(pressedCount),
                _pullPanel.GetIconSprite(pressedCount),
                isStack,
                () => _pullPanel.HideIcon(pressedCount),
                token))
        {
            return true;
        }

        if (await _machine.Dispense(token)) return true;

        return count == 1
            ? await PresentSingle(items[0], token)
            : await PresentMultiple(items, token);
    }

    // 하나만 뽑았을 때: 배출구 앞의 캡슐을 누르면 화면 가운데로 올라와 열리고 슬라임이 솟는다.
    private async UniTask<bool> PresentSingle(GachaPullItem item, CancellationToken token)
    {
        SlimeController target = item.Slime;
        _isSpecialResult = item.Rarity == EGachaRarity.Special;
        Vector2 center = GetLocalCenter();
        string name = _isSpecialResult
            ? GetName(target.Grade) + "\n<size=65%>" + SpecialSubtitle + "</size>"
            : GetName(target.Grade);
        _singleResult.Prepare(center, name, _isSpecialResult);

        int index = await WaitForCapsule(CapsuleTapMessage, token);
        if (index < 0) return true;

        _tapPrompt.gameObject.SetActive(false);
        _machine.SetCapsulesInteractable(false);

        Color resultColor = GetResultColor(item.Rarity);
        if (await _machine.Open(0, item.Rarity, resultColor, token)) return true;
        // 슬라임이 솟고 드러나는 동안 기계의 빛이 계속 새로 퍼진다.
        Action<float> sustain = elapsed => _machine.Sustain(elapsed, Tint(resultColor));
        if (await _singleResult.Emerge(GetSprite(target.Grade), item.Rarity, sustain, token)) return true;
        if (await _singleResult.Reveal(sustain, token)) return true;

        _machine.EndSustain();
        _machine.Hide();
        if (await Wait(_holdDuration, token)) return true;

        Vector2 destination = GetFieldLocalPosition(target, center);
        _singleResult.PlayTrail(resultColor);
        AudioManager.Instance?.PlaySFX(EAudioSfx.GachaResult);
        if (await _singleResult.MoveTo(destination, Flight, token)) return true;

        // 도착하는 순간 진짜 슬라임이 필드에 나타나고, 그 자리에서 빛과 파편이 터진다.
        target.SetLocationPresentationActive(true);
        if (await _singleResult.Arrive(destination, resultColor, token)) return true;
        if (await Fade(1f, 0f, _fadeDuration, token)) return true;

        CloseOverlay();
        return false;
    }

    // 여러 개를 뽑았을 때: 캡슐 다섯이 자리에 늘어서고, 하나씩 눌러 열면 슬라임만 그 자리에 남는다.
    // 모두 열기 버튼으로 남은 것을 차례로 열 수도 있다. 전부 열면 화면을 눌러 한꺼번에 필드로 보낸다.
    private async UniTask<bool> PresentMultiple(List<GachaPullItem> items, CancellationToken token)
    {
        int count = items.Count;
        _resultSlots.Prepare(count);

        // 기계는 뒤로 물러나 늘어선 캡슐만 남는다. 기다리지 않고 바로 누를 수 있다.
        _machine.FadeMachineTo(0f, 0.4f, token).Forget();

        // 모두 열기를 누르면 남은 캡슐이 한꺼번에 열린다. 하나를 여는 중에 눌러도 그 캡슐이 끝난 뒤 나머지가 열린다.
        bool[] opened = new bool[count];
        _openAllRequested = false;
        _openAllButton.gameObject.SetActive(true);

        int openedCount = 0;
        while (openedCount < count)
        {
            int index = await WaitForCapsule(CapsuleTapMessage, token);
            if (index == RequestOpenAll)
            {
                _openAllButton.gameObject.SetActive(false);
                _tapPrompt.gameObject.SetActive(false);
                _machine.SetCapsulesInteractable(false);
                if (await OpenRemaining(items, opened, token)) return true;

                break;
            }

            if (index < 0) return true;

            _machine.SetCapsulesInteractable(false);
            GachaPullItem item = items[index];
            Color color = GetResultColor(item.Rarity);
            if (await _machine.Open(index, item.Rarity, color, token)) return true;

            opened[index] = true;
            openedCount++;
            StartSlotEmerge(index, item, token);
            if (openedCount < count) _machine.SetCapsulesInteractable(true);
        }

        _openAllButton.gameObject.SetActive(false);

        while (_resultSlots.IsEmerging)
        {
            if (await NextFrame(token)) return true;
        }

        _tapPrompt.text = UiMessages.MachineAllOpened;
        _tapPrompt.gameObject.SetActive(true);
        if (await WaitForScreenTap(token)) return true;

        _tapPrompt.gameObject.SetActive(false);
        AudioManager.Instance?.PlaySFX(EAudioSfx.GachaResult);
        UniTask<bool>[] flights = new UniTask<bool>[count];
        for (int i = 0; i < count; i++)
        {
            flights[i] = FlySlot(i, items[i], token);
        }

        bool[] results = await UniTask.WhenAll(flights);
        foreach (bool flightCancelled in results)
        {
            if (flightCancelled) return true;
        }

        if (await Fade(1f, 0f, _fadeDuration, token)) return true;

        CloseOverlay();
        return false;
    }

    // 화면을 처음 상태로 돌린다. 지난번 연출의 흔적이 남지 않게 모든 조각을 감춘다.
    private void ResetPresentation()
    {
        _tapped = false;
        _openAllRequested = false;
        _tapButton.gameObject.SetActive(false);
        _openAllButton.gameObject.SetActive(false);
        _tapPrompt.gameObject.SetActive(false);
        _pullPanel.Hide();
        _machineGrab.Stop();
        _machine.Prepare();
        _singleResult.ResetAll();

        _resultSlots.ResetAll();
    }

    // 안내 문구를 띄우고 캡슐이 눌릴 때까지 기다린다. 눌린 캡슐 번호를 돌려주고, 취소되면 음수를 돌려준다.
    private async UniTask<int> WaitForCapsule(string prompt, CancellationToken token)
    {
        _tapPrompt.text = prompt;
        _tapPrompt.gameObject.SetActive(true);
        _machine.SetCapsulesInteractable(true);
        float elapsed = 0f;
        while (true)
        {
            if (_machine.TryConsumeCapsuleTap(out int index)) return index;

            // 모두 열기 버튼은 여러 개를 뽑았을 때만 보이므로, 1회 뽑기에서는 이 요청이 오지 않는다.
            if (_openAllRequested)
            {
                _openAllRequested = false;
                return RequestOpenAll;
            }

            if (await NextFrame(token)) return RequestCancelled;

            elapsed += Time.unscaledDeltaTime;
            _machine.IdleCapsules(elapsed);
            _tapPrompt.alpha = 0.68f + Mathf.Sin(elapsed * 3f) * 0.22f;
        }
    }

    // 화면 어디든 한 번 눌릴 때까지 기다린다. 이 버튼은 화면 전체를 덮어 아래의 캡슐과 버튼을 막으므로
    // 기다리는 동안에만 켠다.
    private async UniTask<bool> WaitForScreenTap(CancellationToken token)
    {
        _tapped = false;
        _tapButton.gameObject.SetActive(true);
        float elapsed = 0f;
        while (!_tapped)
        {
            if (await NextFrame(token)) return true;

            elapsed += Time.unscaledDeltaTime;
            _tapPrompt.alpha = 0.68f + Mathf.Sin(elapsed * 3f) * 0.22f;
        }

        _tapButton.gameObject.SetActive(false);
        return false;
    }

    // 1회·5회·닫기 중 하나가 눌릴 때까지 기다린다.
    private async UniTask<int> WaitForRequest(bool isTutorial, CancellationToken token)
    {
        _pendingRequest = RequestNone;
        float elapsed = 0f;
        while (_pendingRequest == RequestNone)
        {
            if (await NextFrame(token)) return RequestCancelled;

            elapsed += Time.unscaledDeltaTime;
            if (isTutorial) _pullPanel.PulseSingle(elapsed);
            else if (!HasEverBoughtTicket()) _pullPanel.PulseBuy(elapsed);
            _tapPrompt.alpha = 0.68f + Mathf.Sin(elapsed * 3f) * 0.22f;
        }

        int request = _pendingRequest;
        _pendingRequest = RequestNone;
        return request;
    }

    // 아직 안 연 캡슐을 한꺼번에 열고, 갈라진 자리마다 슬라임을 솟게 한다. 등급은 각 자리의 오라 색이 보여 준다.
    private async UniTask<bool> OpenRemaining(
        List<GachaPullItem> items,
        bool[] opened,
        CancellationToken token)
    {
        List<int> indices = new();
        List<EGachaRarity> rarities = new();
        List<Color> colors = new();
        for (int i = 0; i < items.Count; i++)
        {
            if (opened[i]) continue;

            indices.Add(i);
            rarities.Add(items[i].Rarity);
            colors.Add(GetResultColor(items[i].Rarity));
        }

        if (await _machine.OpenMany(indices, rarities, colors, token)) return true;

        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            opened[index] = true;
            StartSlotEmerge(index, items[index], token);
        }

        return false;
    }

    private void OnOpenAllClicked()
    {
        if (!_isPlaying) return;

        _openAllRequested = true;
    }

    private void OnPullRequested(int count)
    {
        if (_state != SessionState.Choosing || _pendingRequest != RequestNone) return;

        _pendingRequest = count;
    }

    private void OnCloseRequested()
    {
        if (_state != SessionState.Choosing || _pendingRequest != RequestNone) return;

        _pendingRequest = RequestClose;
    }

    // 졸업식 뒤 포인트로 한 장 산다. 고르는 중에만 받고, 산 뒤에는 버튼과 안내의 장수를 고친다.
    // 구매는 뽑기 요청과 따로 바로 처리한다. 뽑기권만 늘 뿐 화면 흐름은 그대로 고르는 중이다.
    private void OnBuyRequested()
    {
        if (_state != SessionState.Choosing || _pendingRequest != RequestNone || IsTutorialPull()) return;

        EPointTicketPurchaseFailure failure = PointTicketShop.TryBuy(_slimeManager, _currencyManager);
        switch (failure)
        {
            case EPointTicketPurchaseFailure.None:
                int tickets = GetTicketCount();
                _pullPanel.SetTickets(tickets);
                ShowPrompt(GetChoosePrompt(tickets, false));
                _pullPanel.ShowToast(UiMessages.PointTicketBought);
                AudioManager.Instance?.PlaySFX(EAudioSfx.ShopPurchase);
                break;
            case EPointTicketPurchaseFailure.DailyLimit:
                _pullPanel.ShowToast(UiMessages.PointTicketDailyLimit);
                break;
            case EPointTicketPurchaseFailure.NotEnoughPoints:
                _pullPanel.ShowToast(UiMessages.NotEnoughPoints);
                break;
            default:
                _pullPanel.ShowToast(UiMessages.PullFailed);
                break;
        }

        RefreshBuyOffer();
    }

    // 하루 기록은 처음 산 날에 0이 아닌 날짜를 갖는다. 그 뒤로는 날이 바뀌어도 0으로 돌아가지 않는다.
    private bool HasEverBoughtTicket() => _currencyManager.PointTicketPurchase.DayIndex > 0;

    private void RefreshBuyOffer()
    {
        if (!PointTicketShop.IsOpen(_slimeManager)) return;

        int remaining = PointTicketShop.GetRemainingToday(_currencyManager);
        Currency price = PointTicketPurchase.PricePerTicket * PointTicketShop.AmountPerPurchase;
        string label = string.Format(
            UiMessages.PointTicketBuyLabel,
            price,
            remaining,
            PointTicketPurchase.DailyLimit);
        _pullPanel.ShowBuyOffer(label, remaining > 0 && _currencyManager.CanAfford(ECurrencyType.Point, price));
    }

    // 뒤로 가기: 버튼을 고르는 중에만 닫는다. 연출 중에는 다른 곳으로 새지 않게 삼키기만 한다.
    // 튜토리얼에서는 닫기 버튼이 없으므로 같은 이유로 닫지 않는다.
    private bool OnBackRequested()
    {
        if (_state == SessionState.Choosing && !IsTutorialPull())
        {
            OnCloseRequested();
        }

        return true;
    }

    private void ShowPrompt(string text)
    {
        _tapPrompt.text = text;
        _tapPrompt.alpha = 1f;
        _tapPrompt.gameObject.SetActive(true);
    }

    private string GetChoosePrompt(int tickets, bool isTutorial)
    {
        string count = string.Format(UiMessages.MachineTicketCount, tickets);
        if (isTutorial) return UiMessages.MachineTutorialPull + "\n" + count;

        return tickets <= 0
            ? UiMessages.NoTicket + "\n" + count
            : UiMessages.MachineChoose + "\n" + count;
    }

    private static string GetFailureMessage(EGachaFailure failure)
    {
        switch (failure)
        {
            case EGachaFailure.NoRoom:
                return UiMessages.NoRoomForPull;
            case EGachaFailure.NoTicket:
                return UiMessages.NoTicket;
            default:
                Debug.LogError($"뽑기에 실패했습니다. : {failure}");
                return UiMessages.PullFailed;
        }
    }

    // 뽑기 튜토리얼이 끝나기 전의 첫 뽑기는 튜토리얼이 준 무료 한 장이다. 그동안은 1회만 허용한다.
    private static bool IsTutorialPull()
    {
        return !TutorialProgress.IsCompleted(TutorialIds.Gacha);
    }

    private int GetTicketCount()
    {
        return Mathf.FloorToInt((float)(double)_currencyManager.Get(ECurrencyType.GachaTicket));
    }

    // 열린 캡슐 자리에서 슬라임이 솟아 이름과 함께 남는다. 무엇을 보일지만 정하고 움직임은 자리가 맡는다.
    private void StartSlotEmerge(int index, GachaPullItem item, CancellationToken token)
    {
        ESlimeGrade grade = item.Slime.Grade;
        string name = item.Rarity == EGachaRarity.Special
            ? GetName(grade) + "\n<size=65%>" + UiMessages.SpecialSlimeShort + "</size>"
            : GetName(grade);
        _resultSlots.StartEmerge(index, GetSprite(grade), name, item.Rarity, token);
    }

    // 열린 슬라임이 자기 자리에서 필드의 제자리로 날아간다. 도착 크기는 1회 결과가 줄어드는 실제 크기에 맞춘다.
    private UniTask<bool> FlySlot(int index, GachaPullItem item, CancellationToken token)
    {
        float endScale = _resultSlots.GetEndScaleForWidth(index, _singleResult.ArrivalWidth);
        return _resultSlots.Fly(
            index,
            () => GetFieldLocalPosition(item.Slime, _resultSlots.GetRootPosition(index)),
            endScale,
            Flight,
            GetResultColor(item.Rarity),
            item.Rarity == EGachaRarity.Special,
            () => item.Slime.SetLocationPresentationActive(true),
            token);
    }

    private void OnTapped()
    {
        if (!_isPlaying || _tapped) return;

        _tapped = true;
    }

    // 특별한 결과는 정해진 색 대신 색상환을 도는 색을 쓴다. 나머지는 그대로 돌려준다.
    private Color Tint(Color color)
    {
        if (!_isSpecialResult) return color;

        return RainbowTint.Pure();
    }

    private static Color GetResultColor(EGachaRarity rarity)
    {
        return rarity switch
        {
            // 색은 Tint가 프레임마다 무지개로 덮는다. 여기서는 시작 색만 정한다.
            EGachaRarity.Special => Color.white,
            EGachaRarity.Jackpot => JackpotColor,
            EGachaRarity.Rare => RareColor,
            EGachaRarity.Uncommon => UncommonColor,
            _ => CommonColor
        };
    }

    private static float Normalized(float elapsed, float duration) =>
        duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

    private Sprite GetSprite(ESlimeGrade grade)
    {
        Slime slime = _slimeManager.Get(grade);
        return slime?.SpecData?.Sprite;
    }

    private string GetName(ESlimeGrade grade)
    {
        Slime slime = _slimeManager.Get(grade);
        return slime?.SpecData?.Name ?? grade.ToString();
    }

    // 부모 rect의 중앙 로컬 좌표. pivot이 가운데가 아니어도 맞도록 rect.center를 쓴다.
    private Vector2 GetLocalCenter()
    {
        return _overlayRect != null ? _overlayRect.rect.center : Vector2.zero;
    }

    // 슬라임의 월드 위치를 화면 좌표로 옮긴 뒤 결과 이미지 부모의 로컬 좌표로 되돌린다.
    // 정상 파일들이 쓰는 표준 경로(WorldToScreenPoint -> ScreenPointToLocalPointInRectangle)다.
    private Vector2 GetFieldLocalPosition(SlimeController target, Vector2 fallbackLocal)
    {
        Camera camera = Camera.main;
        if (camera == null || _overlayRect == null) return fallbackLocal;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            camera, target.transform.position);

        // 오버레이 캔버스면 카메라를 넘기지 않는다. 카메라 캔버스면 그 캔버스 카메라를 쓴다.
        Camera uiCamera = ResolveUiCamera();
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayRect, screenPoint, uiCamera, out Vector2 local)
            ? local
            : fallbackLocal;
    }

    private Camera ResolveUiCamera()
    {
        Canvas canvas = _overlayRect != null
            ? _overlayRect.GetComponentInParent<Canvas>()
            : null;
        if (canvas == null) return null;

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    // 1회와 5회가 함께 쓰는 비행 모양이다.
    private GachaResultFlight Flight => new(_moveDuration, _moveArcRadius, _moveSpinTurns);

    private async UniTask<bool> Fade(float from, float to, float duration, CancellationToken token)
    {
        if (duration <= 0f)
        {
            _canvasGroup.alpha = to;
            return false;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Normalized(elapsed, duration));
        }
        _canvasGroup.alpha = to;
        return false;
    }

    private static async UniTask<bool> Wait(float seconds, CancellationToken token) =>
        await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime,
            cancellationToken: token).SuppressCancellationThrow();

    private void CloseOverlay()
    {
        _tapButton.gameObject.SetActive(false);
        _openAllButton.gameObject.SetActive(false);
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _pullPanel.Hide();
        _machineGrab.Stop();
        _machine.Hide();
        _root.SetActive(false);
    }

    private static async UniTask<bool> NextFrame(CancellationToken token) =>
        await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
}
