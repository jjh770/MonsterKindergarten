using System.Collections.Generic;
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

    [Tooltip("티켓을 담을 월드 스페이스 캔버스입니다.")]
    [SerializeField] private Transform _ticketRoot;

    [Tooltip("떨어뜨린 슬라임에서 이 반경 안에 흩어 놓습니다.")]
    [SerializeField, Min(0f)] private float _dropScatterRadius = 0.4f;

    [Tooltip("한 스테이지에 동시에 놓을 오브젝트 수의 상한입니다. 저장된 장수는 줄지 않습니다.")]
    [SerializeField, Min(1)] private int _maxObjectsPerStage = 30;

    private readonly List<GameObject> _groundTickets = new();
    private readonly List<GameObject> _skyTickets = new();

    private void Awake()
    {
        if (_dropper == null || _ticketPrefab == null || _ticketRoot == null)
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

        Vector2 scatter = Random.insideUnitCircle * _dropScatterRadius;
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

    // 한 장씩 줍는다. 저장을 먼저 줄이고 그 결과로만 재화를 올린다. 순서를 뒤집으면
    // 남은 장수가 없을 때 재화만 늘어난다.
    private void Collect(EGameStage stage, GameObject ticket)
    {
        if (SlimeManager.Instance == null || CurrencyManager.Instance == null) return;
        if (!SlimeManager.Instance.TryConsumePendingTicket(stage)) return;

        CurrencyManager.Instance.Add(ECurrencyType.GachaTicket, 1d);

        GetTickets(stage).Remove(ticket);
        Destroy(ticket);

        // 표시 상한에 걸려 못 만든 몫이 남아 있으면 빈 자리를 채운다.
        Restore(stage);
        ApplyVisibility();
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
