using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 떨어진 가챠권을 필드의 상태로 만들고 화면에 유지한다. 판정은 GachaTicketDropper가
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
    [SerializeField] private GachaTicketDropper _dropper;
    [SerializeField] private GameObject _ticketPrefab;

    // 주운 티켓이 날아가 앉을 자리. 보유 장수를 보여 주는 상단 바의 티켓 아이콘이다.
    // 뽑기 버튼이 아니라 개수가 있는 곳으로 보내야 날아간 티켓이 어디에 더해졌는지 보인다.
    [Tooltip("주운 티켓이 날아갈 상단 바의 티켓 아이콘입니다.")]
    [SerializeField] private RectTransform _collectTarget;

    [Tooltip("수집 중인 티켓과 이펙트를 HUD보다 앞에 그리는 오버레이 루트입니다.")]
    [SerializeField] private RectTransform _collectOverlayRoot;

    [Tooltip("티켓을 담을 월드 스페이스 캔버스입니다.")]
    [SerializeField] private Transform _ticketRoot;

    [Tooltip("떨어뜨린 슬라임에서 이 반경 안에 흩어 놓습니다.")]
    [SerializeField, Min(0f)] private float _dropScatterRadius = 0.4f;

    [Tooltip("필드에 동시에 놓을 오브젝트 수의 상한입니다. 저장된 장수는 줄지 않습니다.")]
    [FormerlySerializedAs("_maxObjectsPerStage")]
    [SerializeField, Min(1)] private int _maxObjects = 30;

    [Header("Collect Presentation")]
    [SerializeField, Min(0.05f)] private float _collectFlyDuration = 0.65f;
    [Tooltip("화면의 짧은 축을 기준으로 위로 띄우는 높이 비율입니다.")]
    [SerializeField, Range(0f, 0.5f)] private float _collectArcScreenRatio = 0.18f;
    [SerializeField, Range(0f, 0.5f)] private float _collectArcRandomness = 0.2f;

    [Tooltip("좌우로 흩어지는 폭의 비율입니다. 0이면 매번 같은 세로 궤적이 됩니다.")]
    [SerializeField, Range(0f, 0.3f)] private float _collectSideSpreadRatio = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float _collectEndScale = 0.35f;
    [SerializeField, Min(0f)] private float _targetPunchScale = 0.15f;

    [Header("Collect Burst")]
    [SerializeField, Range(4, 16)] private int _collectBurstParticleCount = 8;
    [SerializeField, Min(0.05f)] private float _collectBurstDuration = 0.3f;
    [SerializeField, Min(0f)] private float _collectBurstLeadTime = 0.12f;
    [SerializeField, Min(0.1f)] private float _collectBurstDistanceScale = 0.85f;

    private readonly List<GameObject> _tickets = new();
    private readonly HashSet<GameObject> _collectingTickets = new();
    private readonly HashSet<Tween> _targetPunchTweens = new();

    private Vector3 _collectTargetBaseScale;
    private bool _isBulkCollecting;
    private bool _isBulkCollectScheduled;

    public event Action CollectionStateChanged;

    public int PendingTicketCount
    {
        get
        {
            if (SlimeManager.Instance == null) return 0;

            return SlimeManager.Instance.GetPendingTicketCount();
        }
    }

    public bool CanCollectAll =>
        SlimeManager.Instance != null &&
        SlimeManager.Instance.IsTicketBulkCollectUnlocked &&
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
        if (_collectTarget != null)
        {
            _collectTargetBaseScale = _collectTarget.localScale;
        }

        if (_dropper == null || _ticketPrefab == null || _ticketRoot == null ||
            _collectTarget == null || _collectOverlayRoot == null)
        {
            Debug.LogError("가챠권 필드에 필요한 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _dropper.Dropped += OnDropped;
    }

    private void Start()
    {
        if (!enabled) return;

        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        if (StageManager.Instance != null)
        {
            StageManager.Instance.SpaceChanged += OnSpaceChanged;
        }

        // 이미 발화한 뒤에 붙었으면 이벤트를 다시 기다릴 수 없다.
        if (GameManager.Instance != null &&
            GameManager.Instance.IsAllDataInitialized)
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

        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        if (StageManager.Instance != null)
        {
            StageManager.Instance.SpaceChanged -= OnSpaceChanged;
        }

        foreach (Tween tween in new List<Tween>(_targetPunchTweens))
        {
            tween?.Kill(complete: false);
        }

        _targetPunchTweens.Clear();
        _collectingTickets.Clear();
        RestoreCollectTargetScale();
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
        if (source == null || SlimeManager.Instance == null) return;

        SlimeManager.Instance.AddPendingTicket();

        Vector2 scatter = UnityEngine.Random.insideUnitCircle * _dropScatterRadius;
        Create((Vector2)source.transform.position + scatter);
        ApplyVisibility();
        CollectionStateChanged?.Invoke();
    }

    private void Restore()
    {
        int stored = SlimeManager.Instance != null
            ? SlimeManager.Instance.GetPendingTicketCount()
            : 0;
        int target = Mathf.Min(stored, _maxObjects);

        for (int i = _tickets.Count; i < target; i++)
        {
            Create(GetRestorePosition());
        }
    }

    // 저장된 장수는 상한 없이 늘지만 화면에 놓는 오브젝트는 여기서 끊는다. 며칠을
    // 방치하면 수백 장이 쌓이는데 그만큼 오브젝트를 만들 이유가 없다. 상한을 넘은
    // 몫도 저장에는 남아 있으므로 장수를 잃지는 않는다.
    private void Create(Vector2 position)
    {
        if (_tickets.Count >= _maxObjects) return;

        GameObject ticket = Instantiate(_ticketPrefab, _ticketRoot);
        ticket.transform.position = position;

        Button button = ticket.GetComponentInChildren<Button>();
        if (button == null)
        {
            Debug.LogError("가챠권 프리팹에 버튼이 없습니다.", ticket);
        }
        else
        {
            button.onClick.AddListener(() => Collect(ticket));
        }

        _tickets.Add(ticket);
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
        if (SlimeManager.Instance == null ||
            SlimeManager.Instance.GetPendingTicketCount() <= 0)
        {
            return;
        }

        _isBulkCollecting = true;
        ContinueBulkCollect();
    }

    private void ContinueBulkCollect()
    {
        if (!_isBulkCollecting) return;

        if (SlimeManager.Instance == null ||
            SlimeManager.Instance.GetPendingTicketCount() <= 0)
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
        if (SlimeManager.Instance == null || CurrencyManager.Instance == null) return;
        if (SlimeManager.Instance.GetPendingTicketCount() <= 0) return;

        PlayCollectPresentation(ticket, () => CompleteCollect(ticket));
        CollectionStateChanged?.Invoke();
    }

    private void CollectBulk(GameObject ticket)
    {
        if (SlimeManager.Instance == null || CurrencyManager.Instance == null) return;
        if (SlimeManager.Instance.GetPendingTicketCount() <= 0) return;

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

        if (SlimeManager.Instance == null || CurrencyManager.Instance == null)
        {
            _isBulkCollecting = false;
            CollectionStateChanged?.Invoke();
            return;
        }

        if (!SlimeManager.Instance.TryConsumePendingTicket())
        {
            _isBulkCollecting = false;
            Restore();
            ApplyVisibility();
            CollectionStateChanged?.Invoke();
            return;
        }

        CurrencyManager.Instance.Add(ECurrencyType.GachaTicket, 1d);

        // 표시 상한에 걸려 못 만든 몫이 남아 있으면 빈 자리를 채운다.
        Restore();
        ApplyVisibility();

        if (SlimeManager.Instance.GetPendingTicketCount() <= 0)
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
        RectTransform target = _collectTarget;
        Camera mainCamera = Camera.main;
        if (target == null || mainCamera == null || ticketRect == null ||
            _collectOverlayRoot == null)
        {
            Destroy(ticket);
            onArrived?.Invoke();
            return;
        }

        // 시작점은 월드 스페이스 캔버스, 도착점은 Screen Space Overlay다. 먼저 화면
        // 좌표에서 시작 위치와 크기를 측정한 뒤 수집 오버레이로 옮겨야, HUD보다 앞에
        // 그리면서도 순간이동하거나 크기가 튀지 않는다.
        Vector2 startScreenPosition = RectTransformUtility.WorldToScreenPoint(
            mainCamera,
            ticket.transform.position);
        Vector2 targetScreenPosition = RectTransformUtility.WorldToScreenPoint(
            null,
            target.position);

        Vector2 overlaySize = GetOverlaySize(ticketRect, mainCamera);
        if (!TryGetOverlayPosition(startScreenPosition, out Vector2 startOverlayPosition) ||
            !TryGetOverlayPosition(targetScreenPosition, out Vector2 targetOverlayPosition))
        {
            Destroy(ticket);
            onArrived?.Invoke();
            return;
        }

        ticketRect.SetParent(_collectOverlayRoot, false);
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

        GetArcControlPoints(
            startScreenPosition,
            targetScreenPosition,
            out Vector2 firstControlPoint,
            out Vector2 secondControlPoint);

        Sprite burstSprite = ticketImage != null ? ticketImage.sprite : null;
        PlayCollectBurst(burstSprite, startOverlayPosition, overlaySize, 1f);
        ticketRect.SetAsLastSibling();

        Sequence sequence = DOTween.Sequence();
        sequence.AppendInterval(_collectBurstLeadTime);
        sequence.Append(
            DOVirtual.Float(0f, 1f, _collectFlyDuration, progress =>
            {
                Vector2 screenPosition = EvaluateCubicBezier(
                    startScreenPosition,
                    firstControlPoint,
                    secondControlPoint,
                    targetScreenPosition,
                    progress);
                if (TryGetOverlayPosition(screenPosition, out Vector2 overlayPosition))
                {
                    ticketRect.anchoredPosition = overlayPosition;
                }
            })
                .SetEase(Ease.InOutQuad));
        sequence.Join(
            ticketRect.DOScale(
                Vector3.one * _collectEndScale,
                _collectFlyDuration)
                .SetEase(Ease.InQuad));
        sequence.SetLink(ticket, LinkBehaviour.KillOnDestroy);
        sequence.OnComplete(() =>
        {
            PlayCollectBurst(
                burstSprite,
                targetOverlayPosition,
                overlaySize * _collectEndScale,
                0.65f);

            PlayCollectTargetPunch();

            onArrived?.Invoke();
            Destroy(ticket);
        });
    }

    private Vector2 GetOverlaySize(RectTransform ticketRect, Camera mainCamera)
    {
        var corners = new Vector3[4];
        ticketRect.GetWorldCorners(corners);

        Vector2 bottomLeftScreen = RectTransformUtility.WorldToScreenPoint(
            mainCamera,
            corners[0]);
        Vector2 topRightScreen = RectTransformUtility.WorldToScreenPoint(
            mainCamera,
            corners[2]);

        if (!TryGetOverlayPosition(bottomLeftScreen, out Vector2 bottomLeft) ||
            !TryGetOverlayPosition(topRightScreen, out Vector2 topRight))
        {
            return new Vector2(80f, 80f);
        }

        return new Vector2(
            Mathf.Max(1f, Mathf.Abs(topRight.x - bottomLeft.x)),
            Mathf.Max(1f, Mathf.Abs(topRight.y - bottomLeft.y)));
    }

    private bool TryGetOverlayPosition(Vector2 screenPosition, out Vector2 localPosition)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _collectOverlayRoot,
            screenPosition,
            null,
            out localPosition);
    }

    // 별도 텍스처를 늘리지 않고 티켓 이미지를 작은 반짝이 조각으로 재사용한다.
    // 시작점에서는 터치 반응, 도착점에서는 보유 UI로 흡수됐다는 반응을 만든다.
    private void PlayCollectBurst(
        Sprite sprite,
        Vector2 position,
        Vector2 ticketSize,
        float intensity)
    {
        if (_collectOverlayRoot == null || sprite == null) return;

        GameObject rootObject = new GameObject(
            "TicketCollectBurst",
            typeof(RectTransform));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(_collectOverlayRoot, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = position;
        root.sizeDelta = Vector2.zero;
        root.SetAsLastSibling();

        int count = Mathf.Max(1, _collectBurstParticleCount);
        float baseSize = Mathf.Max(20f, Mathf.Min(ticketSize.x, ticketSize.y) * 0.38f);
        float distance = Mathf.Max(ticketSize.x, ticketSize.y) *
                         _collectBurstDistanceScale * intensity;

        Sequence burst = DOTween.Sequence();

        // 티켓 중심에서 한 번 크게 번지는 잔상을 먼저 보여 줘 작은 조각만 흩어질 때보다
        // 터치와 도착 순간을 또렷하게 읽을 수 있게 한다.
        GameObject flashObject = new GameObject(
            "Flash",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image));
        RectTransform flash = flashObject.GetComponent<RectTransform>();
        flash.SetParent(root, false);
        flash.anchorMin = new Vector2(0.5f, 0.5f);
        flash.anchorMax = new Vector2(0.5f, 0.5f);
        flash.pivot = new Vector2(0.5f, 0.5f);
        flash.sizeDelta = ticketSize * (1.35f * Mathf.Max(0.8f, intensity));
        flash.localScale = Vector3.one * 0.55f;

        UnityEngine.UI.Image flashImage = flashObject.GetComponent<UnityEngine.UI.Image>();
        flashImage.sprite = sprite;
        flashImage.preserveAspect = true;
        flashImage.raycastTarget = false;
        flashImage.color = new Color(1f, 0.84f, 0.2f, 0.85f);

        float flashDuration = _collectBurstDuration * 0.75f;
        burst.Join(flash.DOScale(1.7f, flashDuration).SetEase(Ease.OutCubic));
        burst.Join(flashImage.DOFade(0f, flashDuration).SetEase(Ease.InQuad));

        for (int i = 0; i < count; ++i)
        {
            float angle = 360f * i / count + UnityEngine.Random.Range(-12f, 12f);
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;

            GameObject particleObject = new GameObject(
                $"Spark{i + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image));
            RectTransform particle = particleObject.GetComponent<RectTransform>();
            particle.SetParent(root, false);
            particle.anchorMin = new Vector2(0.5f, 0.5f);
            particle.anchorMax = new Vector2(0.5f, 0.5f);
            particle.pivot = new Vector2(0.5f, 0.5f);
            particle.sizeDelta = Vector2.one * baseSize;
            particle.localScale = Vector3.one * 0.65f;

            UnityEngine.UI.Image image = particleObject.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 0.88f, 0.25f, 1f);

            float particleDistance = distance * UnityEngine.Random.Range(0.75f, 1.15f);
            burst.Join(
                particle.DOAnchorPos(
                        direction * particleDistance,
                        _collectBurstDuration)
                    .SetEase(Ease.OutCubic));
            burst.Join(
                particle.DOScale(
                        UnityEngine.Random.Range(1.15f, 1.55f),
                        _collectBurstDuration)
                    .SetEase(Ease.OutBack));
            burst.Join(image.DOFade(0f, _collectBurstDuration));
        }

        burst.SetLink(rootObject, LinkBehaviour.KillOnDestroy);
        burst.OnComplete(() => Destroy(rootObject));
    }

    // 위로 한 번 띄웠다가 버튼으로 내려앉게 만든다. 경로에 수직인 방향으로 휘면
    // 티켓과 버튼의 위치 관계에 따라 궤적이 옆으로 눕거나 아래로 처진다. 화면 기준
    // 위쪽은 그 관계와 무관하게 늘 같은 방향이라 던져 올린 느낌이 유지된다.
    //
    // 좌우 흔들림은 따로 뽑는다. 높이만 무작위로 두면 여러 장을 연달아 주울 때
    // 궤적이 한 평면에 겹쳐 보인다.
    private void GetArcControlPoints(
        Vector2 startScreenPosition,
        Vector2 targetScreenPosition,
        out Vector2 firstControlPoint,
        out Vector2 secondControlPoint)
    {
        Vector2 screenPath = targetScreenPosition - startScreenPosition;

        // 화면의 짧은 축을 기준으로 삼아야 해상도가 달라져도 궤적이 같아 보인다.
        float shortSide = Mathf.Min(Screen.width, Screen.height);
        float lift = shortSide * _collectArcScreenRatio * RandomArcScale();
        float spread = shortSide * _collectSideSpreadRatio;

        firstControlPoint = startScreenPosition +
                            screenPath * UnityEngine.Random.Range(0.2f, 0.35f) +
                            Vector2.up * lift +
                            Vector2.right * UnityEngine.Random.Range(-spread, spread);

        // 두 번째는 덜 띄운다. 같은 높이로 두면 정점이 평평해져 포물선이 아니라
        // 위로 밀려 올라간 직선처럼 보인다.
        secondControlPoint = startScreenPosition +
                             screenPath * UnityEngine.Random.Range(0.6f, 0.8f) +
                             Vector2.up * (lift * 0.35f) +
                             Vector2.right * UnityEngine.Random.Range(-spread, spread);
    }

    private float RandomArcScale()
    {
        return UnityEngine.Random.Range(
            1f - _collectArcRandomness,
            1f + _collectArcRandomness);
    }

    private static Vector2 EvaluateCubicBezier(
        Vector2 start,
        Vector2 firstControlPoint,
        Vector2 secondControlPoint,
        Vector2 end,
        float progress)
    {
        float inverse = 1f - progress;
        return inverse * inverse * inverse * start +
               3f * inverse * inverse * progress * firstControlPoint +
               3f * inverse * progress * progress * secondControlPoint +
               progress * progress * progress * end;
    }

    private static Vector2 GetRestorePosition()
    {
        return SpawnManager.Instance != null
            ? SpawnManager.Instance.GetRandomSpawnPosition()
            : Vector2.zero;
    }

    private void ApplyVisibility()
    {
        StageManager stageManager = StageManager.Instance;
        SetVisible(
            _tickets,
            stageManager != null && stageManager.IsMainStageActive);
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

    private void PlayCollectTargetPunch()
    {
        if (_collectTarget == null) return;

        Tween punch = _collectTarget.DOPunchScale(
            Vector3.one * _targetPunchScale,
            0.2f,
            5,
            0.5f);
        _targetPunchTweens.Add(punch);
        punch.OnComplete(() => FinishCollectTargetPunch(punch));
        punch.OnKill(() => FinishCollectTargetPunch(punch));
    }

    private void FinishCollectTargetPunch(Tween punch)
    {
        if (!_targetPunchTweens.Remove(punch)) return;
        if (_targetPunchTweens.Count > 0) return;

        RestoreCollectTargetScale();
    }

    private void RestoreCollectTargetScale()
    {
        if (_collectTarget != null)
        {
            _collectTarget.localScale = _collectTargetBaseScale;
        }
    }
}
