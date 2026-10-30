using System.Collections.Generic;
using DG.Tweening;
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
    [Tooltip("탐험이 돌아온 순간 느낌표가 붙은 버튼 둘레에서 터지는 반짝이입니다. 비워 두면 터지지 않습니다.")]
    [SerializeField] private UiSparkleBurst _arrivalBurst;
    [Tooltip("느낌표가 떠 있는 동안 한 번씩 흔들리는 간격입니다(초).")]
    [SerializeField, Min(0.5f)] private float _alertWiggleInterval = 2.4f;

    private const float CheckInterval = 1f;

    // 이미 알린(또는 앱을 켤 때 이미 끝나 있던) 탐험이다. 받으면 목록에서 사라지므로 함께 지운다.
    private readonly HashSet<string> _known = new();
    private readonly List<string> _stale = new();
    private float _nextCheck;
    private bool _primed;
    private bool _isReady;
    private bool _alertsShown;
    private Sequence _wiggle;
    private Tween _arrivalTween;

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
        _spaces.SpaceChanged += OnSpaceChanged;
        _isReady = true;
    }

    private void OnDestroy()
    {
        if (_spaces != null) _spaces.SpaceChanged -= OnSpaceChanged;
    }

    // 장식장 가기 버튼의 글자는 공간이 바뀌는 순간 바뀐다. 느낌표는 1초마다만 다시 보므로, 그 사이에
    // "유치원 가기"가 된 버튼에 느낌표가 잠깐 남았다. 공간이 바뀌면 바로 다시 붙이거나 뗀다.
    private void OnSpaceChanged(EGameplaySpace space)
    {
        if (_isReady) SetAlerts(_alertsShown);
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
            PlayArrival();
        }
    }

    private void OnDisable()
    {
        StopWiggle();
        _arrivalTween?.Kill(complete: true);
        _alertsShown = false;
    }

    // 플레이 중에 탐험이 돌아온 순간이다. 지금 보이는 느낌표의 버튼이 통통 튀고, 느낌표가 톡 튀어나오며
    // 버튼 둘레에서 반짝이가 터지고 축하음이 난다.
    private void PlayArrival()
    {
        GameObject alert = _spaceToggleAlert.activeInHierarchy ? _spaceToggleAlert : _expeditionAlert;
        if (!alert.activeInHierarchy) return;

        Transform button = alert.transform.parent;
        _arrivalTween?.Kill(complete: true);
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(button.gameObject);
        // 버튼 크기는 누름 연출과 함께 ButtonPressFeedback 한 곳에서만 바꾼다. 따로 흔들면
        // 출렁이는 도중에 누른 크기가 기준이 되어 버튼이 커진 채 남는다.
        if (button.TryGetComponent(out ButtonPressFeedback feedback)) feedback.Punch(.2f, .45f);
        else sequence.Join(button.DOPunchScale(Vector3.one * .2f, .45f, 6, .5f));
        alert.transform.localScale = Vector3.zero;
        sequence.Join(alert.transform.DOScale(Vector3.one, .35f).SetEase(Ease.OutBack));
        _arrivalTween = sequence.OnKill(() => _arrivalTween = null);
        if (_arrivalBurst != null) _arrivalBurst.Play(button.position);
        AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock);
    }

    private void SetAlerts(bool anyReady)
    {
        _expeditionAlert.SetActive(anyReady);
        _spaceToggleAlert.SetActive(anyReady && _spaces.IsMainFieldActive);
        if (anyReady == _alertsShown) return;

        // 느낌표가 떠 있는 동안은 가끔 한 번씩 흔들어 받을 보상이 있다는 것을 눈에 띄게 한다.
        _alertsShown = anyReady;
        StopWiggle();
        if (!anyReady) return;

        _wiggle = DOTween.Sequence().SetUpdate(true).SetLink(gameObject).SetLoops(-1);
        _wiggle.AppendInterval(_alertWiggleInterval);
        _wiggle.Append(_expeditionAlert.transform.DOPunchRotation(new Vector3(0f, 0f, 18f), .45f, 6, .5f));
        _wiggle.Join(_spaceToggleAlert.transform.DOPunchRotation(new Vector3(0f, 0f, 18f), .45f, 6, .5f));
    }

    private void StopWiggle()
    {
        _wiggle?.Kill();
        _wiggle = null;
        _expeditionAlert.transform.localRotation = Quaternion.identity;
        _spaceToggleAlert.transform.localRotation = Quaternion.identity;
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
