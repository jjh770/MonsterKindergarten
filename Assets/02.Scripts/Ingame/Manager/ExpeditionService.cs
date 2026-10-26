using System;
using System.Collections.Generic;

// 탐험 출발과 두 저장 도메인의 보상 정산 순서를 맡는다. 화면과 보상량 계산은 호출자가 맡는다.
public sealed class ExpeditionService
{
    private readonly SlimeManager _slimes;
    private readonly CurrencyManager _currency;
    private readonly int _maxMembers;
    private readonly TimeSpan _duration;
    private bool _isBusy;

    public ExpeditionService(SlimeManager slimes, CurrencyManager currency,
        int maxMembers = ExpeditionDestination.MaxMembers, TimeSpan? duration = null)
    {
        _slimes = slimes ?? throw new ArgumentNullException(nameof(slimes));
        _currency = currency ?? throw new ArgumentNullException(nameof(currency));
        _maxMembers = maxMembers > 0 && maxMembers <= ExpeditionDestination.MaxMembers ? maxMembers : throw new ArgumentOutOfRangeException(nameof(maxMembers));
        _duration = duration ?? TimeSpan.FromHours(1);
        if (_duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
    }

    public bool TryStart(ExpeditionDestination destination, IReadOnlyList<string> memberIds, ExpeditionReward reward)
    {
        if (destination == null || !_slimes.IsInitialized ||
            _slimes.NormalCollectionCount < destination.RequiredCollectionCount) return false;
        return TryStartCore(destination.DestinationId, memberIds, reward, destination);
    }

    // 기존 단시간 Editor 확인 경로다. 운영 화면에서는 탐험지 규칙을 전달한다.
    public bool TryStart(string destinationId, IReadOnlyList<string> memberIds, ExpeditionReward reward)
        => TryStartCore(destinationId, memberIds, reward, null);

    private bool TryStartCore(string destinationId, IReadOnlyList<string> memberIds, ExpeditionReward reward,
        ExpeditionDestination destination)
    {
        if (!CanOperate() || memberIds == null || memberIds.Count == 0 || memberIds.Count > _maxMembers)
            return false;
        _isBusy = true;
        try
        {
            if (!RecoverCore() || _slimes.ActiveExpeditions.Count >= ExpeditionDestination.MaxActiveExpeditions ||
                _slimes.ExpeditionSettlement.Sequence == long.MaxValue) return false;

            var members = new List<SlimeInstance>(memberIds.Count);
            foreach (string id in memberIds)
            {
                SlimeInstance found = null;
                foreach (SlimeInstance slime in _slimes.ActiveSlimes)
                {
                    if (slime.InstanceId == id) { found = slime; break; }
                }
                if (found == null || found.Grade > _slimes.HighestGrade) return false;
                members.Add(found);
            }

            long sequence;
            try { sequence = _slimes.ExpeditionSettlement.GetNextDepartureSequence(_slimes.ActiveExpeditions); }
            catch (OverflowException) { return false; }
            ExpeditionRun run;
            bool created = destination != null
                ? ExpeditionDeparture.TryCreateRun(destination, members, ServerClock.TrustedUtcNow, reward,
                    out run, out _, sequence)
                : ExpeditionDeparture.TryCreateRun(destinationId, members, _maxMembers,
                    ServerClock.TrustedUtcNow, _duration, reward, out run, out _, sequence);
            return created && _slimes.TryStartExpedition(run);
        }
        finally { _isBusy = false; }
    }

    public bool TryClaim(string runId, out ExpeditionReward reward)
    {
        reward = null;
        if (!CanOperate()) return false;
        _isBusy = true;
        try
        {
            if (!RecoverCore()) return false;
            ExpeditionRun run = null;
            foreach (ExpeditionRun active in _slimes.ActiveExpeditions)
                if (active.RunId == runId) { run = active; break; }
            if (run == null || !_slimes.TryCompleteExpedition(run.RunId, ServerClock.TrustedUtcNow)) return false;
            if (!RecoverCore()) return false;
            reward = run.Reward;
            return true;
        }
        finally { _isBusy = false; }
    }

    // 로드 후와 출발·수령 전에 호출한다. 누적 정산 순번과 탐험지별 완료 기록으로 재개한다.
    public bool TryRecoverSettlement()
    {
        if (!CanOperate()) return false;
        _isBusy = true;
        try { return RecoverCore(); }
        finally { _isBusy = false; }
    }

    private bool CanOperate() => !_isBusy && _slimes.IsInitialized && _currency.IsInitialized &&
                                 GameplayGate.IsReady && GameplaySaveGate.IsSavingEnabled && !SaveDataLoadGuard.HasFailure;

    private bool RecoverCore()
    {
        ExpeditionSettlement progress = _slimes.ExpeditionSettlement;
        ExpeditionSettlement paid = _currency.ExpeditionSettlement;
        return paid.Sequence <= progress.Sequence
            ? _currency.TryApplyExpeditionSettlement(progress)
            : _slimes.TryAdvanceExpeditionSettlement(paid);
    }
}
