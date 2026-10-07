using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 뽑기 기계 화면 한 번을 이끈다. 화면을 열고, 1회·5회 버튼을 기다리고, 눌리면 티켓을 쓰고 결과를 만들어
// 기계로 보여 주고, 결과를 필드로 보낸 뒤 닫는다. 기계 그림과 캡슐 움직임은 GachaMachineView가,
// 버튼과 토스트는 GachaPullPanel이 맡고, 여기서는 순서와 결과 슬라임의 등장을 정한다.
//
// 티켓은 버튼을 누르는 순간 쓰이고 결과가 만들어져 저장된다. 연출은 그 뒤에 보여 주기만 해서, 도중에
// 앱이 꺼져도 잃는 것이 없고 닫기 버튼은 그 전까지만 의미가 있다. 빛의 색은 확정된 희귀도를 표현할 뿐이다.
//
// 이 화면이 열려 있는 동안 자연 스폰은 정지 요청으로 멈춘다. 플레이어의 자동 스폰 설정은 건드리지 않는다.
// 정지는 닫기, 끝, 파괴 어느 길로 끝나도 풀린다.
public sealed class GachaResultDirector : MonoBehaviour
{
    // 한 번 뽑은 결과가 열린 뒤 필드로 날아가기 전까지 머무는 자리다. 여러 개를 뽑을 때 쓴다.
    [Serializable]
    private sealed class ResultSlot
    {
        public RectTransform Root;
        public Image Slime;
        public SlimeOutlineImage Outline;
        public GachaResultAura Aura;
        public TMP_Text Name;
    }

    private enum SessionState
    {
        Closed,
        Choosing,
        Presenting,
    }

    private const int RequestNone = -3;
    private const int RequestCancelled = -2;
    private const int RequestClose = -1;

    private static string CapsuleTapMessage => UiMessages.MachineCapsuleTap;

    private static readonly Color CommonColor = new(0.35f, 1f, 0.72f, 1f);
    private static readonly Color UncommonColor = new(0.32f, 0.78f, 1f, 1f);
    private static readonly Color RareColor = new(0.67f, 0.42f, 1f, 1f);
    private static readonly Color JackpotColor = new(1f, 0.76f, 0.22f, 1f);

    private static string SpecialSubtitle => UiMessages.SpecialSlimeSubtitle;

    // 파편이 퍼지는 모양이다. 거리는 캔버스 좌표 단위이고, 끝 거리는 파편마다
    // EndSpread씩 EndSpreadSteps 칸으로 엇갈려 한 줄로 늘어서지 않게 한다.
    private struct BurstShape
    {
        public float AngleJitter;
        public float StartDistance;
        public float EndDistance;
        public float EndSpread;
        public int EndSpreadSteps;
        public float StartScale;
        public float EndScale;
    }

    private static readonly BurstShape ArrivalBurstShape = new()
    {
        AngleJitter = 0.16f,
        StartDistance = 18f,
        EndDistance = 170f,
        EndSpread = 18f,
        EndSpreadSteps = 4,
        StartScale = 1.3f,
        EndScale = 0.2f,
    };

    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _resultImage;
    [SerializeField] private SlimeOutlineImage _resultOutline;
    [SerializeField] private GachaResultAura _resultAura;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private CurrencyManager _currencyManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private GameExitManager _gameExitManager;

    [Header("Machine")]
    [SerializeField] private GachaMachineView _machine;
    [SerializeField] private GachaPullPanel _pullPanel;
    [SerializeField] private Button _tapButton;
    [SerializeField] private TMP_Text _tapPrompt;
    [SerializeField] private TMP_Text _resultNameText;
    [SerializeField] private RectTransform _arrivalEffectRoot;
    [SerializeField] private RectTransform[] _arrivalSparks;

    [Header("Slots")]
    [SerializeField] private ResultSlot[] _slots;
    [SerializeField, Min(0f)] private float _slotEmergeSeconds = 0.5f;
    [SerializeField, Min(0f)] private float _flyStagger = 0.18f;
    [SerializeField, Min(0f)] private float _slotEndScale = 0.25f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float _emergeDuration = 0.45f;
    [SerializeField, Min(0f)] private float _revealDuration = 0.6f;
    [SerializeField, Min(0f)] private float _holdDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float _moveDuration = 0.5f;
    [SerializeField, Min(0f)] private float _arrivalDuration = 0.45f;

    [Header("Look")]
    [SerializeField, Min(0f)] private float _emergeScale = 1.35f;
    [SerializeField, Min(0f)] private float _revealScale = 1f;
    [SerializeField, Min(0f)] private float _endScale = 0.25f;
    [SerializeField, Min(0f)] private float _nameSlideDistance = 180f;
    [SerializeField, Min(0f)] private float _moveArcRadius = 160f;
    [SerializeField, Min(0f)] private float _moveSpinTurns = 1f;

    // 결과 슬라임이 솟아오르는 세로 이동 거리. 캔버스 좌표(anchoredPosition) 단위다.
    [SerializeField, Min(0f)] private float _emergeRise = 80f;

    private readonly List<SlimeController> _pulled = new();
    private RectTransform _resultRect;
    private RectTransform _resultParentRect;
    private RectTransform _resultNameRect;
    private Vector2 _resultNameRestPosition;
    private bool[] _slotShimmer;
    private SessionState _state;
    private int _pendingRequest = RequestNone;
    private int _pendingEmerges;
    private bool _isReady;
    private bool _isPlaying;
    private bool _tapped;
    private bool _isSpecialResult;
    // 결과 그림이 다 드러난 뒤에만 무지개 광택을 입힌다. 드러나는 중에는 실루엣 색이 먼저다.
    private bool _isResultImageShimmering;

    public bool IsPlaying => _isPlaying;

    // 티켓이 쓰이고 결과가 만들어져 저장된 순간이다. 이 뒤로는 되돌릴 수 없다.
    public event Action PullCommitted;

    private void Awake()
    {
        if (_root == null || _canvasGroup == null || _resultImage == null || _resultOutline == null ||
            _resultAura == null ||
            _machine == null || _pullPanel == null || _tapButton == null || _tapPrompt == null ||
            _resultNameText == null || _arrivalEffectRoot == null || _arrivalSparks == null ||
            _slimeManager == null || _currencyManager == null || _spawnManager == null ||
            !IsValidSlots())
        {
            Debug.LogError("뽑기 기계 연출의 필수 참조가 비어 있습니다.", this);
            return;
        }

        _resultRect = _resultImage.transform as RectTransform;
        _resultParentRect = _resultRect != null
            ? _resultRect.parent as RectTransform
            : null;
        _resultNameRect = _resultNameText.transform as RectTransform;
        _isReady = _resultRect != null && _resultParentRect != null && _resultNameRect != null;
        if (_resultNameRect != null)
        {
            _resultNameRestPosition = _resultNameRect.anchoredPosition;
        }

        _slotShimmer = new bool[_slots.Length];
        _tapButton.onClick.AddListener(OnTapped);
        _pullPanel.PullRequested += OnPullRequested;
        _pullPanel.CloseRequested += OnCloseRequested;
        _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_tapButton != null) _tapButton.onClick.RemoveListener(OnTapped);
        if (_pullPanel != null)
        {
            _pullPanel.PullRequested -= OnPullRequested;
            _pullPanel.CloseRequested -= OnCloseRequested;
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
            _isResultImageShimmering = false;
            Array.Clear(_slotShimmer, 0, _slotShimmer.Length);
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
        ShowPrompt(GetChoosePrompt(tickets, isTutorial));

        List<GachaPullItem> items = new();
        int pressed = 0;
        while (items.Count == 0)
        {
            int request = await WaitForRequest(isTutorial, token);
            if (request == RequestCancelled) return true;

            if (request == RequestClose)
            {
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
        bool[] specials = new bool[count];
        for (int i = 0; i < count; i++)
        {
            SlimeController slime = items[i].Slime;
            specials[i] = items[i].Rarity == EGachaRarity.Special;
            _pulled.Add(slime);
            slime.SetLocationPresentationActive(false);
        }

        _pullPanel.SetCommitted(pressedCount);
        _tapPrompt.gameObject.SetActive(false);
        PullCommitted?.Invoke();
        _machine.BeginPull(specials);

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
        _isResultImageShimmering = false;
        Vector2 center = GetLocalCenter();
        PrepareSingleResult(center, target.Grade);

        int index = await WaitForCapsule(CapsuleTapMessage, token);
        if (index < 0) return true;

        _tapPrompt.gameObject.SetActive(false);
        _machine.SetCapsulesInteractable(false);

        Color resultColor = GetResultColor(item.Rarity);
        if (await _machine.Open(0, item.Rarity, resultColor, token)) return true;
        if (await Emerge(GetSprite(target.Grade), item.Rarity, resultColor, token)) return true;
        if (await Reveal(resultColor, token)) return true;
        if (await Wait(_holdDuration, token)) return true;

        Vector2 destination = GetFieldLocalPosition(target, center);
        _resultAura.Hide();
        AudioManager.Instance?.PlaySFX(EAudioSfx.GachaResult);
        if (await MoveTo(destination, token)) return true;
        if (await PlayArrivalEffect(destination, resultColor, token)) return true;
        if (await Fade(1f, 0f, _fadeDuration, token)) return true;

        CloseOverlay();
        return false;
    }

    // 여러 개를 뽑았을 때: 캡슐 다섯이 자리에 늘어서고, 하나씩 눌러 열면 슬라임만 그 자리에 남는다.
    // 전부 열면 화면을 눌러 한꺼번에 필드로 보낸다.
    private async UniTask<bool> PresentMultiple(List<GachaPullItem> items, CancellationToken token)
    {
        int count = items.Count;
        PrepareSlots(items);
        _pendingEmerges = 0;

        // 기계는 뒤로 물러나 늘어선 캡슐만 남는다. 기다리지 않고 바로 누를 수 있다.
        _machine.FadeMachineTo(0f, 0.4f, token).Forget();

        int openedCount = 0;
        while (openedCount < count)
        {
            int index = await WaitForCapsule(CapsuleTapMessage, token);
            if (index < 0) return true;

            _machine.SetCapsulesInteractable(false);
            GachaPullItem item = items[index];
            Color color = GetResultColor(item.Rarity);
            if (await _machine.Open(index, item.Rarity, color, token)) return true;

            openedCount++;
            _pendingEmerges++;
            EmergeSlot(index, item, color, token).Forget();
            if (openedCount < count) _machine.SetCapsulesInteractable(true);
        }

        while (_pendingEmerges > 0)
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
        _tapButton.gameObject.SetActive(false);
        _tapPrompt.gameObject.SetActive(false);
        _pullPanel.Hide();
        _machine.Prepare();
        _resultAura.Hide();
        _resultOutline.Apply(null, false, false);
        _resultImage.enabled = false;
        _resultNameText.alpha = 0f;
        _resultNameText.gameObject.SetActive(true);
        _arrivalEffectRoot.gameObject.SetActive(false);
        _isResultImageShimmering = false;

        foreach (ResultSlot slot in _slots)
        {
            slot.Aura.Hide();
            slot.Outline.Apply(null, false, false);
            slot.Slime.enabled = false;
            slot.Name.gameObject.SetActive(false);
        }

        Array.Clear(_slotShimmer, 0, _slotShimmer.Length);
    }

    private void PrepareSingleResult(Vector2 centerLocal, ESlimeGrade grade)
    {
        _resultRect.anchoredPosition = centerLocal + Vector2.down * _emergeRise;
        _resultRect.localScale = Vector3.one * 0.2f;
        _resultImage.enabled = false;
        _resultImage.color = Color.white;

        _resultNameText.text = _isSpecialResult
            ? GetName(grade) + "\n<size=65%>" + SpecialSubtitle + "</size>"
            : GetName(grade);
        _resultNameText.alpha = 0f;
        _resultNameText.gameObject.SetActive(true);
        _resultNameRect.anchoredPosition =
            _resultNameRestPosition + Vector2.right * _nameSlideDistance;

        _arrivalEffectRoot.gameObject.SetActive(false);
        _arrivalEffectRoot.localScale = Vector3.one;
        HideSparks(_arrivalSparks);
    }

    private void PrepareSlots(List<GachaPullItem> items)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            ResultSlot slot = _slots[i];
            slot.Slime.enabled = false;
            slot.Name.gameObject.SetActive(false);
            slot.Root.gameObject.SetActive(i < items.Count);
        }
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

    // 1회·5회·닫기 중 하나가 눌릴 때까지 기다린다. 기다리는 동안 기계 안의 캡슐 더미가 살아 움직인다.
    private async UniTask<int> WaitForRequest(bool isTutorial, CancellationToken token)
    {
        _pendingRequest = RequestNone;
        float elapsed = 0f;
        while (_pendingRequest == RequestNone)
        {
            if (await NextFrame(token)) return RequestCancelled;

            elapsed += Time.unscaledDeltaTime;
            _machine.Idle(elapsed);
            if (isTutorial) _pullPanel.PulseSingle(elapsed);
            _tapPrompt.alpha = 0.68f + Mathf.Sin(elapsed * 3f) * 0.22f;
        }

        int request = _pendingRequest;
        _pendingRequest = RequestNone;
        return request;
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

    private async UniTask<bool> Emerge(
        Sprite resultSprite,
        EGachaRarity rarity,
        Color resultColor,
        CancellationToken token)
    {
        _resultAura.Show(rarity);
        _resultOutline.Apply(resultSprite, resultSprite != null, _isSpecialResult);
        _resultImage.enabled = resultSprite != null;
        _resultImage.color = Color.white;
        Vector2 start = _resultRect.anchoredPosition;
        Vector2 end = start + Vector2.up * _emergeRise;
        float elapsed = 0f;

        while (elapsed < _emergeDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _emergeDuration));
            _resultRect.anchoredPosition = Vector2.Lerp(start, end, ratio);
            _resultRect.localScale = Vector3.one * Mathf.Lerp(0.2f, _emergeScale, ratio);
            _machine.Sustain(elapsed, Tint(resultColor));
        }
        return false;
    }

    private async UniTask<bool> Reveal(Color resultColor, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < _revealDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _revealDuration));
            float sparkle = Mathf.Sin(ratio * Mathf.PI * 3f) * (1f - ratio) * 0.08f;
            float nameRatio = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.12f, 0.72f, ratio));
            _resultRect.localScale = Vector3.one *
                                     (Mathf.Lerp(_emergeScale, _revealScale, ratio) + sparkle);
            _resultNameText.alpha = nameRatio;
            _resultNameRect.anchoredPosition = Vector2.Lerp(
                _resultNameRestPosition + Vector2.right * _nameSlideDistance,
                _resultNameRestPosition,
                nameRatio);
            _machine.Sustain(_emergeDuration + elapsed, Tint(resultColor));
        }

        _resultImage.color = Color.white;
        _isResultImageShimmering = _isSpecialResult;
        _resultRect.localScale = Vector3.one * _revealScale;
        _resultNameText.alpha = 1f;
        _resultNameRect.anchoredPosition = _resultNameRestPosition;
        _machine.EndSustain();
        _machine.Hide();
        return false;
    }

    // 열린 캡슐 자리에서 슬라임이 솟아 이름과 함께 남는다. 다른 캡슐을 여는 것을 막지 않도록 기다리지
    // 않고 따로 돈다. 다 끝나면 _pendingEmerges가 줄어 "모두 열었다"를 알 수 있다.
    private async UniTaskVoid EmergeSlot(
        int index,
        GachaPullItem item,
        Color color,
        CancellationToken token)
    {
        try
        {
            ResultSlot slot = _slots[index];
            ESlimeGrade grade = item.Slime.Grade;
            bool isSpecial = item.Rarity == EGachaRarity.Special;
            Sprite sprite = GetSprite(grade);
            slot.Aura.Show(item.Rarity);
            slot.Outline.Apply(sprite, sprite != null, isSpecial);
            slot.Slime.enabled = slot.Slime.sprite != null;
            slot.Slime.color = Color.white;
            RectTransform rect = slot.Slime.rectTransform;
            rect.anchoredPosition = Vector2.down * 30f;
            rect.localScale = Vector3.one * 0.2f;
            slot.Name.text = isSpecial
                ? GetName(grade) + "\n<size=65%>" + UiMessages.SpecialSlimeShort + "</size>"
                : GetName(grade);
            slot.Name.alpha = 0f;
            slot.Name.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < _slotEmergeSeconds)
            {
                if (await NextFrame(token)) return;

                elapsed += Time.unscaledDeltaTime;
                float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _slotEmergeSeconds));
                rect.anchoredPosition = Vector2.Lerp(Vector2.down * 30f, Vector2.zero, ratio);
                rect.localScale = Vector3.one * (Mathf.Lerp(0.2f, 1f, ratio) +
                                                 Mathf.Sin(ratio * Mathf.PI) * 0.15f);
                slot.Name.alpha = Mathf.InverseLerp(0.35f, 0.9f, ratio);
            }

            rect.localScale = Vector3.one;
            slot.Slime.color = Color.white;
            slot.Name.alpha = 1f;
            _slotShimmer[index] = isSpecial;
        }
        finally
        {
            _pendingEmerges--;
        }
    }

    // 열린 슬라임이 자기 자리에서 필드의 제자리로 호를 그리며 날아간다. 잇따라 출발해 연달아 도착한다.
    private async UniTask<bool> FlySlot(int index, GachaPullItem item, CancellationToken token)
    {
        float delay = index * _flyStagger;
        if (delay > 0f && await Wait(delay, token)) return true;

        ResultSlot slot = _slots[index];
        slot.Aura.Hide();
        RectTransform rect = slot.Slime.rectTransform;
        Vector2 destination = GetFieldLocalPosition(item.Slime, slot.Root.anchoredPosition) -
                              slot.Root.anchoredPosition;
        Vector2 start = rect.anchoredPosition;
        Vector2 path = destination - start;
        Vector2 forward = path.sqrMagnitude > 0.01f ? path.normalized : Vector2.up;
        Vector2 perpendicular = new(-forward.y, forward.x);
        float startScale = rect.localScale.x;
        _slotShimmer[index] = false;

        float elapsed = 0f;
        while (elapsed < _moveDuration)
        {
            if (await NextFrame(token)) return true;

            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _moveDuration));
            float angle = ratio * Mathf.PI * 2f;
            float envelope = Mathf.Sin(ratio * Mathf.PI);
            Vector2 orbit =
                (perpendicular * Mathf.Sin(angle) +
                 forward * (1f - Mathf.Cos(angle)) * 0.45f) *
                (_moveArcRadius * 0.6f * envelope);
            rect.anchoredPosition = Vector2.Lerp(start, destination, ratio) + orbit;
            rect.localScale = Vector3.one * Mathf.Lerp(startScale, _slotEndScale, ratio);
            rect.localRotation = Quaternion.Euler(0f, 0f, -360f * ratio);
            slot.Slime.color = Color.white;
            slot.Name.alpha = 1f - Mathf.Clamp01(ratio * 2.5f);
        }

        rect.anchoredPosition = destination;
        rect.localRotation = Quaternion.identity;
        item.Slime.SetLocationPresentationActive(true);
        slot.Slime.enabled = false;
        slot.Name.gameObject.SetActive(false);

        return false;
    }

    // 바깥으로 퍼지며 작아지고 옅어지는 파편이다. 거리, 크기, 엇갈림은 BurstShape이 든다.
    private static void AnimateSparkBurst(
        RectTransform[] sparks,
        in BurstShape shape,
        float ratio,
        Color color)
    {
        int count = sparks.Length;
        for (int i = 0; i < count; i++)
        {
            RectTransform spark = sparks[i];
            if (spark == null) continue;

            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) +
                          (i % 2) * shape.AngleJitter;
            float endDistance = shape.EndDistance + i % shape.EndSpreadSteps * shape.EndSpread;
            SetRadial(spark, angle, Mathf.Lerp(shape.StartDistance, endDistance, ratio));
            spark.localScale = Vector3.one * Mathf.Lerp(shape.StartScale, shape.EndScale, ratio);
            SetImageColor(spark.GetComponent<Image>(), color, 1f - ratio);
        }
    }

    // 중심에서 angle(라디안) 방향으로 distance만큼 떨어진 자리에 놓고 바깥을 향해 세운다.
    // 스프라이트가 위쪽을 보고 그려져 있어서 90도를 뺀다.
    private static void SetRadial(RectTransform spark, float angle, float distance)
    {
        spark.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        spark.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
    }

    private void HideSparks(RectTransform[] sparks)
    {
        foreach (RectTransform spark in sparks)
        {
            if (spark == null) continue;
            spark.anchoredPosition = Vector2.zero;
            spark.localScale = Vector3.zero;
            SetImageAlpha(spark.GetComponent<Image>(), 0f);
        }
    }

    private void AnimateArrivalSparks(float ratio, Color color)
    {
        AnimateSparkBurst(_arrivalSparks, ArrivalBurstShape, ratio, color);
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

    private void Update()
    {
        if (_isResultImageShimmering)
        {
            _resultImage.color = RainbowTint.Shimmer();
        }

        if (_slotShimmer == null) return;

        for (int i = 0; i < _slotShimmer.Length; i++)
        {
            if (_slotShimmer[i]) _slots[i].Slime.color = RainbowTint.Shimmer();
        }
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

    private static void SetImageColor(Image image, Color color, float alpha)
    {
        if (image != null)
            image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
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
        return _resultParentRect != null ? _resultParentRect.rect.center : Vector2.zero;
    }

    // 슬라임의 월드 위치를 화면 좌표로 옮긴 뒤 결과 이미지 부모의 로컬 좌표로 되돌린다.
    // 정상 파일들이 쓰는 표준 경로(WorldToScreenPoint -> ScreenPointToLocalPointInRectangle)다.
    private Vector2 GetFieldLocalPosition(SlimeController target, Vector2 fallbackLocal)
    {
        Camera camera = Camera.main;
        if (camera == null || _resultParentRect == null) return fallbackLocal;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            camera, target.transform.position);

        // 오버레이 캔버스면 카메라를 넘기지 않는다. 카메라 캔버스면 그 캔버스 카메라를 쓴다.
        Camera uiCamera = ResolveUiCamera();
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _resultParentRect, screenPoint, uiCamera, out Vector2 local)
            ? local
            : fallbackLocal;
    }

    private Camera ResolveUiCamera()
    {
        Canvas canvas = _resultParentRect != null
            ? _resultParentRect.GetComponentInParent<Canvas>()
            : null;
        if (canvas == null) return null;

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    // destination은 결과 이미지 부모의 로컬 좌표다. 시작점도 anchoredPosition을 써서
    // 두 끝점이 같은 좌표계 위에 놓이게 한다.
    private async UniTask<bool> MoveTo(Vector2 destination, CancellationToken token)
    {
        Vector2 start = _resultRect.anchoredPosition;
        Vector2 path = destination - start;
        Vector2 forward = path.sqrMagnitude > 0.01f ? path.normalized : Vector2.up;
        Vector2 perpendicular = new(-forward.y, forward.x);
        float startScale = _resultRect.localScale.x;
        float elapsed = 0f;
        while (elapsed < _moveDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _moveDuration));
            float angle = ratio * Mathf.PI * 2f;
            float envelope = Mathf.Sin(ratio * Mathf.PI);
            Vector2 orbit =
                (perpendicular * Mathf.Sin(angle) +
                 forward * (1f - Mathf.Cos(angle)) * 0.45f) *
                (_moveArcRadius * envelope);
            _resultRect.anchoredPosition = Vector2.Lerp(start, destination, ratio) + orbit;
            _resultRect.localScale = Vector3.one * Mathf.Lerp(startScale, _endScale, ratio);
            _resultRect.localRotation = Quaternion.Euler(
                0f,
                0f,
                -360f * _moveSpinTurns * ratio);
            _resultNameText.alpha = 1f - Mathf.Clamp01(ratio * 2.5f);
            _resultNameRect.anchoredPosition =
                _resultNameRestPosition + Vector2.left * _nameSlideDistance * ratio;
        }


        _resultRect.anchoredPosition = destination;
        _resultRect.localScale = Vector3.one * _endScale;
        _resultRect.localRotation = Quaternion.identity;
        _resultNameText.alpha = 0f;
        return false;
    }

    private async UniTask<bool> PlayArrivalEffect(
        Vector2 destination,
        Color color,
        CancellationToken token)
    {
        // 도착 이펙트 루트는 결과 이미지와 같은 부모를 공유하므로 로컬 좌표를 그대로 쓴다.
        _arrivalEffectRoot.anchoredPosition = destination;
        _arrivalEffectRoot.localScale = Vector3.one;
        _arrivalEffectRoot.gameObject.SetActive(true);
        HideSparks(_arrivalSparks);
        float elapsed = 0f;

        while (elapsed < _arrivalDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Normalized(elapsed, _arrivalDuration);
            AnimateArrivalSparks(ratio, Tint(color));
            _resultRect.localScale = Vector3.one *
                                     (_endScale * (1f + Mathf.Sin(ratio * Mathf.PI) * 0.28f));
        }

        _resultRect.localScale = Vector3.one * _endScale;
        _arrivalEffectRoot.gameObject.SetActive(false);
        return false;
    }

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
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _pullPanel.Hide();
        _machine.Hide();
        _root.SetActive(false);
    }

    private bool IsValidSlots()
    {
        if (_slots == null || _slots.Length == 0) return false;

        foreach (ResultSlot slot in _slots)
        {
            if (slot == null || slot.Root == null || slot.Slime == null || slot.Outline == null ||
                slot.Aura == null ||
                slot.Name == null)
            {
                return false;
            }
        }

        return true;
    }

    private static async UniTask<bool> NextFrame(CancellationToken token) =>
        await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
}
