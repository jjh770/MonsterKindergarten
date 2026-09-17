using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 떨어진 가챠권을 필드의 상태로 만들고 화면에 유지한다. 판정은 GachaTicketDropper가
// 하고 여기서는 그 결과를 저장에 반영한 뒤 오브젝트를 놓는다.
//
// 판정과 나눈 이유는 티켓의 수명이 판정보다 훨씬 길기 때문이다. 티켓은 주울 때까지
// 남고 재접속해도 살아 있어야 하는데, 판정은 60초마다 끝나는 일이다.
//
// 티켓이 속한 스테이지는 떨어뜨린 슬라임의 등급으로 드랍 시점에 정하고 그대로 굳힌다.
// 그 슬라임이 나중에 합성되어 하늘로 올라가도 이미 떨어진 티켓은 따라가지 않는다.
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
    [SerializeField] private GachaButtonUI _gachaButton;

    [Tooltip("티켓을 담을 월드 스페이스 캔버스입니다.")]
    [SerializeField] private Transform _ticketRoot;

    [Tooltip("떨어뜨린 슬라임에서 이 반경 안에 흩어 놓습니다.")]
    [SerializeField, Min(0f)] private float _dropScatterRadius = 0.4f;

    [Tooltip("한 스테이지에 동시에 놓을 오브젝트 수의 상한입니다. 저장된 장수는 줄지 않습니다.")]
    [SerializeField, Min(1)] private int _maxObjectsPerStage = 30;

    [Header("Collect Presentation")]
    [SerializeField, Min(0.05f)] private float _collectFlyDuration = 0.65f;
    [Tooltip("화면의 짧은 축을 기준으로 위로 띄우는 높이 비율입니다.")]
    [SerializeField, Range(0f, 0.5f)] private float _collectArcScreenRatio = 0.18f;
    [SerializeField, Range(0f, 0.5f)] private float _collectArcRandomness = 0.2f;

    [Tooltip("좌우로 흩어지는 폭의 비율입니다. 0이면 매번 같은 세로 궤적이 됩니다.")]
    [SerializeField, Range(0f, 0.3f)] private float _collectSideSpreadRatio = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float _collectEndScale = 0.35f;
    [SerializeField, Min(0f)] private float _targetPunchScale = 0.15f;

    private readonly List<GameObject> _groundTickets = new();
    private readonly List<GameObject> _skyTickets = new();

    private void Awake()
    {
        if (_dropper == null || _ticketPrefab == null || _ticketRoot == null ||
            _gachaButton == null)
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
            StageManager.Instance.StageChanged += OnStageChanged;
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
            StageManager.Instance.StageChanged -= OnStageChanged;
            StageManager.Instance.SpaceChanged -= OnSpaceChanged;
        }
    }

    private void OnAllDataInitialized()
    {
        Restore(EGameStage.Ground);
        Restore(EGameStage.Sky);
        ApplyVisibility();
    }

    private void OnStageChanged(EGameStage stage) => ApplyVisibility();

    private void OnSpaceChanged(EGameplaySpace space) => ApplyVisibility();

    private void OnDropped(SlimeController source)
    {
        if (source == null || SlimeManager.Instance == null) return;

        EGameStage stage = GameStageRules.GetStage(source.Grade);
        SlimeManager.Instance.AddPendingTicket(stage);

        Vector2 scatter = UnityEngine.Random.insideUnitCircle * _dropScatterRadius;
        Create(stage, (Vector2)source.transform.position + scatter);
        ApplyVisibility();
    }

    private void Restore(EGameStage stage)
    {
        int stored = SlimeManager.Instance != null
            ? SlimeManager.Instance.GetPendingTicketCount(stage)
            : 0;
        int target = Mathf.Min(stored, _maxObjectsPerStage);

        for (int i = GetTickets(stage).Count; i < target; i++)
        {
            Create(stage, GetRestorePosition());
        }
    }

    // 저장된 장수는 상한 없이 늘지만 화면에 놓는 오브젝트는 여기서 끊는다. 며칠을
    // 방치하면 수백 장이 쌓이는데 그만큼 오브젝트를 만들 이유가 없다. 상한을 넘은
    // 몫도 저장에는 남아 있으므로 장수를 잃지는 않는다.
    private void Create(EGameStage stage, Vector2 position)
    {
        List<GameObject> tickets = GetTickets(stage);
        if (tickets.Count >= _maxObjectsPerStage) return;

        GameObject ticket = Instantiate(_ticketPrefab, _ticketRoot);
        ticket.transform.position = position;

        Button button = ticket.GetComponentInChildren<Button>();
        if (button == null)
        {
            Debug.LogError("가챠권 프리팹에 버튼이 없습니다.", ticket);
        }
        else
        {
            button.onClick.AddListener(() => Collect(stage, ticket));
        }

        tickets.Add(ticket);
    }

    // 한 장씩 줍는다. 저장을 줄이는 것도 재화를 올리는 것도 연출이 끝나는 순간에
    // 한자리에서 한다. 탭할 때 줄여 두면 도착 전에 앱이 죽는 0.65초 동안 저장은
    // 줄었는데 재화는 오르지 않은 상태가 되어 한 장이 그대로 사라진다.
    //
    // 오브젝트는 날아가는 동안에도 목록에 남겨 둔다. 미리 빼면 아직 줄지 않은
    // 저장 장수와 개수가 어긋나, 그 사이에 도는 Restore가 대체 티켓을 하나 더 만든다.
    private void Collect(EGameStage stage, GameObject ticket)
    {
        if (SlimeManager.Instance == null || CurrencyManager.Instance == null) return;
        if (SlimeManager.Instance.GetPendingTicketCount(stage) <= 0) return;

        PlayCollectPresentation(ticket, () => CompleteCollect(stage, ticket));
    }

    private void CompleteCollect(EGameStage stage, GameObject ticket)
    {
        GetTickets(stage).Remove(ticket);

        if (SlimeManager.Instance == null || CurrencyManager.Instance == null) return;
        if (!SlimeManager.Instance.TryConsumePendingTicket(stage)) return;

        CurrencyManager.Instance.Add(ECurrencyType.GachaTicket, 1d);

        // 표시 상한에 걸려 못 만든 몫이 남아 있으면 빈 자리를 채운다.
        Restore(stage);
        ApplyVisibility();
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

        Button button = ticket.GetComponentInChildren<Button>();
        if (button != null)
        {
            // interactable을 끄면 Disabled Color가 적용되어 날기 전에 흐려진다.
            // 컴포넌트만 끄면 현재 색은 유지하면서 중복 입력만 차단할 수 있다.
            button.enabled = false;
        }

        RectTransform target = _gachaButton.ButtonTarget;
        Camera mainCamera = Camera.main;
        if (target == null || mainCamera == null)
        {
            Destroy(ticket);
            onArrived?.Invoke();
            return;
        }

        // 곡선은 화면 좌표에서만 계산한다. 시작점은 월드 스페이스 캔버스의 티켓이고
        // 도착점은 오버레이 캔버스의 버튼이라 두 좌표계가 애초에 다르다. 화면에서
        // 한 번 합쳐 두고 매 프레임 월드로 되돌리는 편이 둘을 섞어 쓰는 것보다 안전하다.
        Vector2 startScreenPosition = RectTransformUtility.WorldToScreenPoint(
            mainCamera,
            ticket.transform.position);
        Vector2 targetScreenPosition = RectTransformUtility.WorldToScreenPoint(
            null,
            target.position);

        // 월드로 되돌릴 때 쓸 값. 티켓은 깊이를 바꾸지 않으므로 처음 것을 그대로 쓴다.
        float ticketZ = ticket.transform.position.z;
        float cameraDistance = Mathf.Abs(
            ticketZ - mainCamera.transform.position.z);

        GetArcControlPoints(
            startScreenPosition,
            targetScreenPosition,
            out Vector2 firstControlPoint,
            out Vector2 secondControlPoint);

        Vector3 startScale = ticket.transform.localScale;

        ticket.transform.SetAsLastSibling();
        Sequence sequence = DOTween.Sequence();
        sequence.Append(
            DOVirtual.Float(0f, 1f, _collectFlyDuration, progress =>
            {
                Vector2 screenPosition = EvaluateCubicBezier(
                    startScreenPosition,
                    firstControlPoint,
                    secondControlPoint,
                    targetScreenPosition,
                    progress);
                Vector3 worldPosition = mainCamera.ScreenToWorldPoint(
                    new Vector3(
                        screenPosition.x,
                        screenPosition.y,
                        cameraDistance));
                worldPosition.z = ticketZ;
                ticket.transform.position = worldPosition;
            })
                .SetEase(Ease.InOutQuad));
        sequence.Join(
            ticket.transform.DOScale(
                startScale * _collectEndScale,
                _collectFlyDuration)
                .SetEase(Ease.InQuad));
        sequence.SetLink(ticket, LinkBehaviour.KillOnDestroy);
        sequence.OnComplete(() =>
        {
            if (target != null)
            {
                target.DOPunchScale(
                    Vector3.one * _targetPunchScale,
                    0.2f,
                    5,
                    0.5f);
            }

            onArrived?.Invoke();
            Destroy(ticket);
        });
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
        SetVisible(_groundTickets, IsStageVisible(EGameStage.Ground));
        SetVisible(_skyTickets, IsStageVisible(EGameStage.Sky));
    }

    // 슬라임 표시 규칙과 같다. 장식장을 보고 있으면 어느 스테이지의 티켓도 보이지
    // 않는다. 전환 연출 중인지는 보지 않는데, 슬라임 쪽도 보지 않기 때문이다.
    private static bool IsStageVisible(EGameStage stage)
    {
        StageManager stageManager = StageManager.Instance;
        return stageManager != null &&
               stageManager.IsMainStageActive &&
               stageManager.CurrentStage == stage;
    }

    private static void SetVisible(List<GameObject> tickets, bool isVisible)
    {
        foreach (GameObject ticket in tickets)
        {
            if (ticket == null) continue;

            ticket.SetActive(isVisible);
        }
    }

    private List<GameObject> GetTickets(EGameStage stage)
    {
        return stage == EGameStage.Sky ? _skyTickets : _groundTickets;
    }
}
