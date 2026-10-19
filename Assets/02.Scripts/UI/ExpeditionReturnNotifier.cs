using System.Collections.Generic;
using UnityEngine;

// 탐험이 끝난 것을 탐험 화면 밖에서 알린다. 받을 보상이 있으면 탐험 버튼과 장식장 가기 버튼에 느낌표를
// 띄우고, 플레이 중에 탐험이 끝나면 토스트로 한 번 알린다.
//
// 탐험은 이벤트가 아니라 시간이 흘러 끝나므로 1초마다 서버 시간으로 상태를 본다. 장식장 가기 버튼의
// 느낌표는 유치원에 있을 때만 켠다. 장식장에서는 그 버튼이 유치원으로 돌아가는 버튼이고, 탐험 버튼이
// 대신 보인다.
//
// 앱을 켰을 때 이미 끝나 있던 탐험은 토스트로 알리지 않는다. 접속하자마자 토스트가 몇 개씩 겹치지
// 않게 하고, 느낌표만으로 알린다.
public sealed class ExpeditionReturnNotifier : MonoBehaviour
{
    [SerializeField] private SlimeManager _slimes;
    [SerializeField] private GameplaySpaceManager _spaces;
    [SerializeField] private ExpeditionUI _expedition;
    [SerializeField] private ToastMessageUI _toast;
    [Tooltip("탐험 버튼 위의 느낌표입니다.")]
    [SerializeField] private GameObject _expeditionAlert;
    [Tooltip("장식장 가기 버튼 위의 느낌표입니다. 유치원에 있을 때만 켭니다.")]
    [SerializeField] private GameObject _spaceToggleAlert;

    private const float CheckInterval = 1f;

    // 이미 알린(또는 앱을 켤 때 이미 끝나 있던) 탐험이다. 받으면 목록에서 사라지므로 함께 지운다.
    private readonly HashSet<string> _known = new();
    private readonly List<string> _stale = new();
    private float _nextCheck;
    private bool _primed;
    private bool _isReady;

    private void Awake()
    {
        if (_slimes == null || _spaces == null || _expedition == null || _toast == null ||
            _expeditionAlert == null || _spaceToggleAlert == null)
        {
            Debug.LogError("탐험 귀환 알림의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        SetAlerts(false);
        _isReady = true;
    }

    private void Update()
    {
        if (!_isReady || Time.unscaledTime < _nextCheck) return;

        _nextCheck = Time.unscaledTime + CheckInterval;
        if (!_slimes.IsInitialized)
        {
            SetAlerts(false);
            return;
        }

        bool anyReady = false;
        string returnedDestination = null;
        IReadOnlyList<ExpeditionRun> runs = _slimes.ActiveExpeditions;
        foreach (ExpeditionRun run in runs)
        {
            if (!run.IsReadyToReturn(ServerClock.TrustedUtcNow)) continue;

            anyReady = true;
            // 처음 확인할 때 끝나 있던 것은 알리지 않고 기억만 한다. 플레이가 멈췄거나 튜토리얼이
            // 진행 중일 때 끝난 것은 그것이 끝난 뒤에 알린다.
            if (_known.Contains(run.RunId)) continue;
            if (_primed && (!GameplayGate.IsActive || TutorialManager.IsRunning)) continue;

            _known.Add(run.RunId);
            if (_primed && returnedDestination == null) returnedDestination = run.DestinationId;
        }

        _primed = true;
        ForgetClaimed(runs);
        SetAlerts(anyReady);

        if (returnedDestination != null)
        {
            _toast.Show(string.Format(UiMessages.ExpeditionReturned,
                _expedition.GetDestinationName(returnedDestination)));
        }
    }

    private void SetAlerts(bool anyReady)
    {
        _expeditionAlert.SetActive(anyReady);
        _spaceToggleAlert.SetActive(anyReady && _spaces.IsMainFieldActive);
    }

    private void ForgetClaimed(IReadOnlyList<ExpeditionRun> runs)
    {
        _stale.Clear();
        foreach (string runId in _known)
        {
            bool stillActive = false;
            foreach (ExpeditionRun run in runs)
            {
                if (run.RunId != runId) continue;

                stillActive = true;
                break;
            }

            if (!stillActive) _stale.Add(runId);
        }

        foreach (string runId in _stale)
        {
            _known.Remove(runId);
        }
    }
}
