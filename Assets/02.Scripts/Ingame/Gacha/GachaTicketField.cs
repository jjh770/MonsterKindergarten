using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 떨어진 뽑기권을 필드의 상태로 만들고 화면에 유지한다. 판정은 GachaTicketDropper가
// 하고 여기서는 그 결과를 저장에 반영한 뒤 오브젝트를 놓는다.
//
// 판정과 나눈 이유는 티켓의 수명이 판정보다 훨씬 길기 때문이다. 티켓은 주울 때까지
// 남고 재접속해도 살아 있어야 하는데, 판정은 60초마다 끝나는 일이다.
//
// 티켓은 월드 콜라이더가 아니라 UI 버튼으로 줍는다. 필드에 콜라이더를 두면
// Clicker의 Physics2D.Raycast가 그것을 먼저 맞고 슬라임 선택이 통째로 사라진다.
// 슬라임 터치는 초당 여러 번 일어나는 핵심 조작이라 한 자리도 내주면 안 된다.
// UI는 물리 경로와 따로 판정되므로, 티켓이 슬라임 위에 겹쳐도 슬라임은 그대로
// 눌린다. 겹친 자리를 누르면 티켓도 줍고 슬라임도 눌리며, 둘 다 이득이라 문제가
// 되지 않는다.
//
// 좌표는 저장하지 않는다. 슬라임도 좌표를 저장하지 않고 복원할 때 다시 흩뿌리므로,
// 티켓만 남길 이유가 없다. 떨어지는 순간에만 슬라임 주변에 놓이고, 재접속 뒤에는
// 필드 아무 곳에나 놓인다.
public class GachaTicketField : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private GameplaySpaceManager _gameplaySpaceManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private CurrencyManager _currencyManager;
    [SerializeField] private SpawnManager _spawnManager;

    [SerializeField] private GachaTicketDropper _dropper;
    [SerializeField] private GameObject _ticketPrefab;

    // 주운 티켓이 날아가 앉을 자리. 보유 장수를 보여 주는 상단 바의 티켓 아이콘이다.
    // 뽑기 버튼이 아니라 개수가 있는 곳으로 보내야 날아간 티켓이 어디에 더해졌는지 보인다.
    [Tooltip("주운 티켓이 날아갈 상단 바의 티켓 아이콘입니다.")]
    [SerializeField] private RectTransform _collectTarget;

    [Tooltip("주운 티켓을 HUD보다 앞에서 날려 보내는 공용 비행 연출입니다(TicketCollectOverlay).")]
    [SerializeField] private UiIconFlight _iconFlight;

    [Tooltip("티켓을 담을 월드 스페이스 캔버스입니다.")]
    [SerializeField] private Transform _ticketRoot;

    [Tooltip("떨어뜨린 슬라임에서 이 반경 안에 흩어 놓습니다.")]
    [SerializeField, Min(0f)] private float _dropScatterRadius = 0.4f;

    [Tooltip("필드에 동시에 놓을 오브젝트 수의 상한입니다. 저장된 장수는 줄지 않습니다.")]
    [FormerlySerializedAs("_maxObjectsPerStage")]
    [SerializeField, Min(1)] private int _maxObjects = 30;

    [Header("Drop Presentation")]
    [Tooltip("떨어진 뽑기권이 슬라임 머리에서 튀어 오르는 높이입니다(월드 단위).")]
    [SerializeField, Min(0f)] private float _dropJumpPower = 0.6f;
    [SerializeField, Min(0.05f)] private float _dropDuration = 0.45f;

    [Header("Collect Presentation")]
    [Tooltip("주운 티켓이 상단 아이콘에 닿을 때의 크기 비율입니다.")]
    [SerializeField, Range(0.1f, 1f)] private float _collectEndScale = 0.35f;

    [Header("Audio")]
    [SerializeField, Min(0f)] private float _collectSoundCooldown = 0.06f;

    private readonly List<GameObject> _tickets = new();
    private readonly HashSet<GameObject> _collectingTickets = new();
    private bool _isBulkCollecting;
    private bool _isBulkCollectScheduled;
    private int _reservedGradeRewardTickets;

    public event Action CollectionStateChanged;

    public int PendingTicketCount
    {
        get
        {
            if (_slimeManager == null) return 0;

            // 해금 보상 팝업이 잡아 둔 몫은 필드의 장수가 아니다.
            return Mathf.Max(0, _slimeManager.GetPendingTicketCount() - _reservedGradeRewardTickets);
        }
    }

    public bool CanCollectAll =>
        _slimeManager != null &&
        _slimeManager.IsTicketBulkCollectUnlocked &&
        PendingTicketCount > 0 &&
        !_isBulkCollecting &&
        HasCollectableTicket();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureTweenCapacity()
    {
        // 화면 상한만큼의 버스트와 비행이 동시에 재생될 수 있다.
        // 플레이 도중 자동 확장하면 프레임 히치와 경고가 생기므로 씬 로드 전에 확보한다.
        DOTween.SetTweensCapacity(2000, 250);
    }

    private void Awake()
    {
        if (_dropper == null || _ticketPrefab == null || _ticketRoot == null ||
            _collectTarget == null || _iconFlight == null ||
            _gameManager == null || _gameplaySpaceManager == null ||
            _slimeManager == null || _currencyManager == null || _spawnManager == null)
        {
            Debug.LogError("뽑기권 필드에 필요한 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _dropper.Dropped += OnDropped;
    }

    private void Start()
    {
        if (!enabled) return;

        _gameManager.AllDataInitialized += OnAllDataInitialized;
        _gameplaySpaceManager.SpaceChanged += OnSpaceChanged;
        _slimeManager.HighestGradeChanged += OnHighestGradeChanged;

        // 이미 발화한 뒤에 붙었으면 이벤트를 다시 기다릴 수 없다.
        if (_gameManager.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        if (_dropper != null)
        {
            _dropper.Dropped -= OnDropped;
        }

        if (_gameManager != null)
        {
            _gameManager.AllDataInitialized -= OnAllDataInitialized;
        }
        if (_gameplaySpaceManager != null)
        {
            _gameplaySpaceManager.SpaceChanged -= OnSpaceChanged;
        }
        if (_slimeManager != null)
        {
            _slimeManager.HighestGradeChanged -= OnHighestGradeChanged;
        }

        _collectingTickets.Clear();
    }

    private void OnAllDataInitialized()
    {
        Restore();
        ApplyVisibility();
        CollectionStateChanged?.Invoke();
    }

    private void OnSpaceChanged(EGameplaySpace space) => ApplyVisibility();

    private void OnDropped(SlimeController source)
    {
        if (source == null || _slimeManager == null) return;

        _slimeManager.AddPendingTicket();

        Vector2 scatter = UnityEngine.Random.insideUnitCircle * _dropScatterRadius;
        GameObject ticket = Create((Vector2)source.transform.position + scatter);
        if (ticket != null) PlayDrop(ticket, source);
        ApplyVisibility();
        CollectionStateChanged?.Invoke();
    }

    // 후반 등급의 첫 도달 보상은 SlimeStatus가 최고 등급과 함께 PendingTickets에 이미 더했다
    // (GradeReachTicketRewards). 그 몫은 필드에 놓지 않고 해금 보상 팝업이 지갑으로 옮길 때까지
    // 따로 잡아 둔다. 필드가 그 몫까지 오브젝트로 만들면 팝업이 넣을 장수를 플레이어가 먼저 줍는다.
    //
    // 잡아 둔 장수는 저장하지 않는다. 팝업이 옮기기 전에 앱이 꺼지면 다음 접속에서는 잡아 둔
    // 것이 없으므로 그 몫이 필드 뽑기권으로 나타난다. 보상을 잃지 않는 쪽으로 무너진다.
    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        _reservedGradeRewardTickets += GradeReachTicketRewards.GetTicketCount(grade);
    }

    // 해금 보상 팝업이 부른다. 티켓이 상단 바에 닿은 순간이나, 팝업이 다른 해금에 밀려 끊길 때다.
    // 필드에서 줍는 것과 같은 거래(저장에서 빼고 재화를 올림)를 장수만큼 한다.
    // 장수는 팝업이 보여 준 만큼만 옮긴다. 다음 등급의 몫이 이미 잡혀 있을 수 있어서다.
    public void ClaimGradeReward(int count)
    {
        count = Mathf.Clamp(count, 0, _reservedGradeRewardTickets);
        _reservedGradeRewardTickets -= count;
        if (_slimeManager == null || _currencyManager == null) return;

        int granted = 0;
        for (int i = 0; i < count; i++)
        {
            if (!EconomyTransactionService.TryGrantAfterConsume(
                    _currencyManager,
                    ECurrencyType.GachaTicket,
                    1d,
                    _slimeManager.TryConsumePendingTicket,
                    _slimeManager.AddPendingTicket))
            {
                break;
            }

            granted++;
        }

        if (granted > 0)
        {
            _slimeManager.RecordGachaTicketsObtained(granted);
        }

        CollectionStateChanged?.Invoke();
    }

    private void Restore()
    {
        int stored = PendingTicketCount;
        int target = Mathf.Min(stored, _maxObjects);

        for (int i = _tickets.Count; i < target; i++)
        {
            Create(GetRestorePosition());
        }
    }

    // 저장된 장수는 상한 없이 늘지만 화면에 놓는 오브젝트는 여기서 끊는다. 며칠을
    // 방치하면 수백 장이 쌓이는데 그만큼 오브젝트를 만들 이유가 없다. 상한을 넘은
    // 몫도 저장에는 남아 있으므로 장수를 잃지는 않는다.
    private GameObject Create(Vector2 position)
    {
        if (_tickets.Count >= _maxObjects) return null;

        GameObject ticket = Instantiate(_ticketPrefab, _ticketRoot);
        ticket.transform.position = position;

        Button button = ticket.GetComponentInChildren<Button>();
        if (button == null)
        {
            Debug.LogError("뽑기권 프리팹에 버튼이 없습니다.", ticket);
        }
        else
        {
            button.onClick.AddListener(() => Collect(ticket));
        }

        _tickets.Add(ticket);
        return ticket;
    }

    // 슬라임 머리 위에서 톡 튀어나와 옆 바닥의 제자리로 떨어진다. 자리와 저장은 이미 정해졌고
    // 모양만 움직이므로, 도중에 주워도 줍는 연출이 먼저 이 움직임을 끝내고 시작한다.
    private void PlayDrop(GameObject ticket, SlimeController source)
    {
        Transform body = ticket.transform;
        Vector3 landing = body.position;
        Vector3 scale = body.localScale;
        // 슬라임 그림은 둘레가 넉넉한 사각형이라 그림 끝이 머리보다 한참 위다. 몸통 콜라이더의 위쪽 끝을 머리로 본다.
        Collider2D slimeCollider = source.GetComponent<Collider2D>();
        float headY = slimeCollider != null && slimeCollider.enabled
            ? slimeCollider.bounds.max.y
            : source.transform.position.y + .3f;
        body.position = new Vector3(source.transform.position.x, headY, landing.z);
        body.localScale = scale * .3f;

        AudioManager.Instance?.PlaySFXRandomPitch(EAudioSfx.SlimeBounce, 1.35f, 1.55f, .7f);
        Sequence sequence = DOTween.Sequence().SetTarget(body).SetLink(ticket);
        sequence.Join(body.DOScale(scale, .2f).SetEase(Ease.OutBack));
        sequence.Join(body.DOJump(landing, _dropJumpPower, 1, _dropDuration).SetEase(Ease.Linear));
        sequence.Append(body.DOPunchScale(new Vector3(scale.x * .2f, -scale.y * .2f, 0f), .2f, 5, .5f));
    }

    public bool TryCollectAll()
    {
        if (!CanCollectAll) return false;

        BeginBulkCollect();
        CollectionStateChanged?.Invoke();
        return true;
    }

    private void BeginBulkCollect()
    {
        if (_slimeManager == null ||
            PendingTicketCount <= 0)
        {
            return;
        }

        _isBulkCollecting = true;
        ContinueBulkCollect();
    }

    private void ContinueBulkCollect()
    {
        if (!_isBulkCollecting) return;

        if (_slimeManager == null ||
            PendingTicketCount <= 0)
        {
            _isBulkCollecting = false;
            CollectionStateChanged?.Invoke();
            return;
        }

        // 완료 콜백이 목록을 바꾸므로 복사본을 순회한다.
        foreach (GameObject ticket in _tickets.ToArray())
        {
            if (ticket == null) continue;

            Button button = ticket.GetComponentInChildren<Button>();
            if (button != null && !button.enabled) continue;

            CollectBulk(ticket);
        }
    }

    private void ScheduleBulkCollect()
    {
        if (!_isBulkCollecting || _isBulkCollectScheduled)
        {
            return;
        }

        _isBulkCollectScheduled = true;
        StartCoroutine(ContinueBulkCollectNextFrame());
    }

    private System.Collections.IEnumerator ContinueBulkCollectNextFrame()
    {
        yield return null;
        _isBulkCollectScheduled = false;
        ContinueBulkCollect();
    }

    // 한 장씩 줍는다. 저장을 줄이는 것도 재화를 올리는 것도 연출이 끝나는 순간에
    // 한자리에서 한다. 탭할 때 줄여 두면 도착 전에 앱이 죽는 0.65초 동안 저장은
    // 줄었는데 재화는 오르지 않은 상태가 되어 한 장이 그대로 사라진다.
    //
    // 오브젝트는 날아가는 동안에도 목록에 남겨 둔다. 미리 빼면 아직 줄지 않은
    // 저장 장수와 개수가 어긋나, 그 사이에 도는 Restore가 대체 티켓을 하나 더 만든다.
    private void Collect(GameObject ticket)
    {
        if (_slimeManager == null || _currencyManager == null) return;
        if (PendingTicketCount <= 0) return;

        PlayCollectPresentation(ticket, () => CompleteCollect(ticket));
        CollectionStateChanged?.Invoke();
    }

    private void CollectBulk(GameObject ticket)
    {
        if (_slimeManager == null || _currencyManager == null) return;
        if (PendingTicketCount <= 0) return;

        ticket.SetActive(true);
        PlayCollectPresentation(
            ticket,
            () => CompleteCollect(ticket));

        CollectionStateChanged?.Invoke();
    }

    private void CompleteCollect(GameObject ticket)
    {
        _collectingTickets.Remove(ticket);
        _tickets.Remove(ticket);

        if (_slimeManager == null || _currencyManager == null)
        {
            _isBulkCollecting = false;
            CollectionStateChanged?.Invoke();
            return;
        }

        if (!EconomyTransactionService.TryGrantAfterConsume(
                _currencyManager,
                ECurrencyType.GachaTicket,
                1d,
                _slimeManager.TryConsumePendingTicket,
                _slimeManager.AddPendingTicket))
        {
            _isBulkCollecting = false;
            Restore();
            ApplyVisibility();
            CollectionStateChanged?.Invoke();
            return;
        }

        _slimeManager.RecordGachaTicketsObtained(1);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXWithCooldown(
                EAudioSfx.TicketCollect,
                _collectSoundCooldown);
        }

        // 표시 상한에 걸려 못 만든 몫이 남아 있으면 빈 자리를 채운다.
        Restore();
        ApplyVisibility();

        if (PendingTicketCount <= 0)
        {
            _isBulkCollecting = false;
        }
        else
        {
            ScheduleBulkCollect();
        }

        CollectionStateChanged?.Invoke();
    }

    private void PlayCollectPresentation(GameObject ticket, Action onArrived)
    {
        // 떨어지던 중이면 제자리에 내려놓고 줍는 연출을 시작한다.
        if (ticket != null) ticket.transform.DOKill(complete: true);

        // 어느 경로로 빠지든 보상은 준다. 연출을 못 보여 준 것이 티켓을 잃을
        // 이유는 되지 않는다.
        if (ticket == null)
        {
            onArrived?.Invoke();
            return;
        }

        // 공간 전환처럼 티켓이 비활성이라면 보상은 연출 없이 안전하게 완료한다.
        if (!ticket.activeInHierarchy)
        {
            Destroy(ticket);
            onArrived?.Invoke();
            return;
        }

        Button button = ticket.GetComponentInChildren<Button>();
        if (button != null)
        {
            // interactable을 끄면 Disabled Color가 적용되어 날기 전에 흐려진다.
            // 컴포넌트만 끄면 현재 색은 유지하면서 중복 입력만 차단할 수 있다.
            button.enabled = false;
        }

        RectTransform ticketRect = ticket.transform as RectTransform;
        UnityEngine.UI.Image ticketImage = ticket.GetComponentInChildren<UnityEngine.UI.Image>();
        Camera mainCamera = Camera.main;
        if (mainCamera == null || ticketRect == null)
        {
            Destroy(ticket);
            onArrived?.Invoke();
            return;
        }

        // 시작점은 월드 스페이스 캔버스, 도착점은 Screen Space Overlay다. 먼저 화면
        // 좌표에서 시작 위치와 크기를 측정한 뒤 비행 레이어로 옮겨야, HUD보다 앞에
        // 그리면서도 순간이동하거나 크기가 튀지 않는다.
        Vector2 startScreenPosition = RectTransformUtility.WorldToScreenPoint(
            mainCamera,
            ticket.transform.position);
        if (!_iconFlight.TryGetLayerSize(ticketRect, mainCamera, out Vector2 overlaySize))
        {
            overlaySize = new Vector2(80f, 80f);
        }

        if (!_iconFlight.TryGetLayerPosition(startScreenPosition, out Vector2 startOverlayPosition))
        {
            Destroy(ticket);
            onArrived?.Invoke();
            return;
        }

        ticketRect.SetParent(_iconFlight.Layer, false);
        ticketRect.anchorMin = new Vector2(0.5f, 0.5f);
        ticketRect.anchorMax = new Vector2(0.5f, 0.5f);
        ticketRect.pivot = new Vector2(0.5f, 0.5f);
        ticketRect.anchoredPosition = startOverlayPosition;
        ticketRect.sizeDelta = overlaySize;
        ticketRect.localRotation = Quaternion.identity;
        ticketRect.localScale = Vector3.one;
        ticketRect.SetAsLastSibling();
        _collectingTickets.Add(ticket);
        if (ticketImage != null) ticketImage.raycastTarget = false;

        Sequence sequence = _iconFlight.Fly(
            ticketRect,
            startScreenPosition,
            overlaySize,
            _collectTarget,
            _collectEndScale,
            ticketImage != null ? ticketImage.sprite : null,
            1f,
            0.65f,
            () =>
            {
                onArrived?.Invoke();
                Destroy(ticket);
            });
        sequence?.SetLink(ticket, LinkBehaviour.KillOnDestroy);
    }

    private Vector2 GetRestorePosition()
    {
        return _spawnManager != null
            ? _spawnManager.GetRandomSpawnPosition()
            : Vector2.zero;
    }

    private void ApplyVisibility()
    {
        SetVisible(
            _tickets,
            _gameplaySpaceManager != null && _gameplaySpaceManager.IsMainFieldActive);
    }

    private void SetVisible(List<GameObject> tickets, bool isVisible)
    {
        foreach (GameObject ticket in tickets)
        {
            if (ticket == null) continue;
            if (_collectingTickets.Contains(ticket)) continue;

            ticket.SetActive(isVisible);
        }
    }

    private bool HasCollectableTicket()
    {
        return HasCollectableTicket(_tickets);
    }

    private static bool HasCollectableTicket(List<GameObject> tickets)
    {
        foreach (GameObject ticket in tickets)
        {
            if (ticket == null) continue;

            Button button = ticket.GetComponentInChildren<Button>();
            if (button == null || button.enabled) return true;
        }

        return false;
    }
}
