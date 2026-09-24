using System;
using System.Collections.Generic;

public class SlimeStatus
{
    // 최고 해금 등급 (한번 올라가면 내려가지 않음)
    public ESlimeGrade HighestGrade { get; private set; }

    private readonly List<SlimeInstance> _activeSlimes = new();
    private readonly HashSet<ESlimeGrade> _registeredNormalCollection = new();
    public IReadOnlyList<SlimeInstance> ActiveSlimes => _activeSlimes;
    public int NormalCollectionCount => _registeredNormalCollection.Count;
    public EBackgroundTheme SelectedBackgroundTheme { get; private set; }
    public bool BackgroundUnlockCompleted { get; private set; }

    // 아직 줍지 않은 가챠권 수. 모든 티켓이 한 필드에 있으므로 위치를 나누지 않는다.
    public int PendingTickets { get; private set; }

    // 플레이어가 켜고 끄는 자연 스폰. 튜토리얼의 일시정지와는 다른 축이다.
    public bool IsAutoSpawnEnabled { get; private set; }
    public bool MainEndingSeen { get; private set; }
    public int SpecialGachaMissCount { get; private set; }

    // 이 계정이 마친 튜토리얼. 기기 로컬 표시는 앱 데이터를 지우면 사라지므로
    // 계정 문서에도 남긴다. 값은 튜토리얼 쪽이 정하는 식별자이고 여기서는 해석하지 않는다.
    private readonly List<string> _completedTutorials = new();
    public IReadOnlyList<string> CompletedTutorials => _completedTutorials;

    private readonly List<PlacedPlaygroundObject> _placedObjects = new();
    private readonly int[] _ownedPlaygroundObjects =
        new int[(int)EPlaygroundObjectType.Count];
    private readonly HashSet<EBackgroundTheme> _ownedBackgroundThemes = new();

    public IReadOnlyList<PlacedPlaygroundObject> PlacedObjects => _placedObjects;
    public IReadOnlyCollection<EBackgroundTheme> OwnedBackgroundThemes =>
        _ownedBackgroundThemes;

    public SlimeStatus(
        ESlimeGrade highestGrade,
        IEnumerable<SlimeInstance> activeSlimes,
        IEnumerable<ESlimeGrade> registeredNormalCollection,
        EBackgroundTheme selectedBackgroundTheme,
        bool backgroundUnlockCompleted,
        int pendingTickets,
        bool isAutoSpawnEnabled,
        bool mainEndingSeen,
        int specialGachaMissCount,
        IEnumerable<string> completedTutorials = null,
        IEnumerable<PlacedPlaygroundObject> placedObjects = null,
        IReadOnlyList<int> ownedPlaygroundObjects = null,
        IEnumerable<EBackgroundTheme> ownedBackgroundThemes = null)
    {
        ValidateGrade(highestGrade);
        HighestGrade = highestGrade;

        RestoreOwnedBackgroundThemes(ownedBackgroundThemes);
        RestorePlayground(placedObjects, ownedPlaygroundObjects);

        // 가지지 않은 테마가 선택되어 있으면 기본 테마로 돌린다. 상점에서 산 것을
        // 잃는 개편이 있어도 화면이 빈 배경으로 남지 않는다.
        bool isBackgroundUnlocked = BackgroundThemeRules.IsUnlocked(highestGrade);
        SelectedBackgroundTheme = isBackgroundUnlocked &&
                                  BackgroundThemeRules.IsValid(selectedBackgroundTheme) &&
                                  IsBackgroundThemeOwned(selectedBackgroundTheme)
            ? selectedBackgroundTheme
            : EBackgroundTheme.Ground;
        BackgroundUnlockCompleted = isBackgroundUnlocked &&
                                    backgroundUnlockCompleted;

        // 음수는 쓰는 쪽에서 나올 수 없는 값이다. 재화와 같은 성격의 개수라
        // 같은 규율로 다룬다. 여기서 던지면 SlimeManager가 다른 손상과 같은
        // 경로로 보낸다.
        if (pendingTickets < 0)
        {
            throw new ArgumentException(
                $"미수령 가챠권 수가 올바르지 않습니다. : {pendingTickets}");
        }

        PendingTickets = pendingTickets;
        IsAutoSpawnEnabled = isAutoSpawnEnabled;
        MainEndingSeen = mainEndingSeen;

        if (specialGachaMissCount < 0 ||
            specialGachaMissCount > SpecialGachaFever.MaximumMissCount)
        {
            throw new ArgumentException(
                $"스페셜 가챠 실패 횟수가 올바르지 않습니다. : {specialGachaMissCount}");
        }

        SpecialGachaMissCount = specialGachaMissCount;

        if (completedTutorials != null)
        {
            // 빈 식별자는 쓰는 쪽에서 나올 수 없다. 다른 손상과 같은 경로로 보낸다.
            // 같은 식별자가 두 번 있으면 한 번만 남긴다.
            foreach (string tutorialId in completedTutorials)
            {
                if (string.IsNullOrWhiteSpace(tutorialId))
                {
                    throw new ArgumentException("완료한 튜토리얼 식별자가 비어 있습니다.");
                }

                if (!_completedTutorials.Contains(tutorialId))
                {
                    _completedTutorials.Add(tutorialId);
                }
            }
        }

        if (activeSlimes == null)
        {
            throw new ArgumentNullException(nameof(activeSlimes));
        }

        var instanceIds = new HashSet<string>();
        foreach (SlimeInstance instance in activeSlimes)
        {
            ValidateInstance(instance);
            if (!instanceIds.Add(instance.InstanceId))
            {
                throw new ArgumentException(
                    $"중복된 슬라임 개체 ID입니다. : {instance.InstanceId}");
            }

            _activeSlimes.Add(instance);
        }

        if (registeredNormalCollection == null)
        {
            throw new ArgumentNullException(nameof(registeredNormalCollection));
        }

        foreach (ESlimeGrade grade in registeredNormalCollection)
        {
            ValidateGrade(grade);
            _registeredNormalCollection.Add(grade);
        }

        foreach (SlimeInstance instance in _activeSlimes)
        {
            if (instance.Location == ESlimeLocation.DisplayRoom &&
                !instance.IsSpecial)
            {
                _registeredNormalCollection.Add(instance.Grade);
            }
        }

        if (MainEndingSeen &&
            NormalCollectionCount < NormalCollectionRules.MainEndingCount)
        {
            throw new ArgumentException("도감 완성 전에 메인 엔딩이 완료된 저장입니다.");
        }

        if (SpecialGachaMissCount > 0 &&
            NormalCollectionCount < NormalCollectionRules.HiddenFeverCount)
        {
            throw new ArgumentException("피버 해금 전에 실패 횟수가 저장되어 있습니다.");
        }

    }

    // --- 배경 테마 소유 ---

    public bool IsBackgroundThemeOwned(EBackgroundTheme theme)
    {
        return BackgroundThemeRules.IsFree(theme) ||
               _ownedBackgroundThemes.Contains(theme);
    }

    public bool TryAddBackgroundTheme(EBackgroundTheme theme)
    {
        if (!BackgroundThemeRules.IsValid(theme) ||
            IsBackgroundThemeOwned(theme))
        {
            return false;
        }

        _ownedBackgroundThemes.Add(theme);
        return true;
    }

    // 모르는 번호는 흘려보낸다. 종류를 줄이는 개편이 있어도 막히지 않아야 한다.
    // 기본 테마는 담지 않는다. 담아 두면 저장에도 실려 나가 규칙이 둘이 된다.
    private void RestoreOwnedBackgroundThemes(IEnumerable<EBackgroundTheme> themes)
    {
        if (themes == null) return;

        foreach (EBackgroundTheme theme in themes)
        {
            if (!BackgroundThemeRules.IsValid(theme)) continue;
            if (BackgroundThemeRules.IsFree(theme)) continue;

            _ownedBackgroundThemes.Add(theme);
        }
    }

    // --- 놀이터 오브젝트 ---

    public int GetOwnedPlaygroundObjectCount(EPlaygroundObjectType type)
    {
        return PlaygroundRules.IsValid(type) ? _ownedPlaygroundObjects[(int)type] : 0;
    }

    public int GetPlacedPlaygroundObjectCount(EPlaygroundObjectType type)
    {
        int count = 0;
        foreach (PlacedPlaygroundObject placed in _placedObjects)
        {
            if (placed.Type == type) count++;
        }

        return count;
    }

    public bool TryBuyPlaygroundObject(EPlaygroundObjectType type)
    {
        if (!PlaygroundRules.IsValid(type)) return false;
        if (_ownedPlaygroundObjects[(int)type] >= PlaygroundRules.MaxPerType) return false;

        _ownedPlaygroundObjects[(int)type]++;
        return true;
    }

    // 산 것 중 아직 놓지 않은 것이 있어야 놓을 수 있다.
    public bool TryPlacePlaygroundObject(EPlaygroundObjectType type, float x, float y)
    {
        if (!PlaygroundRules.IsValid(type)) return false;
        if (GetPlacedPlaygroundObjectCount(type) >= GetOwnedPlaygroundObjectCount(type))
        {
            return false;
        }

        if (!IsPlaygroundPositionAvailable(x, y)) return false;

        _placedObjects.Add(new PlacedPlaygroundObject(
            type,
            PlaygroundRules.ClampX(x),
            PlaygroundRules.ClampY(y)));
        return true;
    }

    public bool TryMovePlacedObject(int index, float x, float y)
    {
        if (index < 0 || index >= _placedObjects.Count) return false;
        if (!IsPlaygroundPositionAvailable(x, y, index)) return false;

        PlacedPlaygroundObject placed = _placedObjects[index];
        _placedObjects[index] = new PlacedPlaygroundObject(
            placed.Type,
            PlaygroundRules.ClampX(x),
            PlaygroundRules.ClampY(y));
        return true;
    }

    // 배치 규칙은 저장 상태를 소유한 도메인이 최종 판정한다. UI가 미리 물어보는
    // 것은 안내 문구를 고르기 위한 것이고, 실제 변경도 반드시 이 검사를 다시 거친다.
    public bool IsPlaygroundPositionAvailable(float x, float y, int ignoreIndex = -1)
    {
        if (!IsFinite(x) || !IsFinite(y)) return false;
        if (ignoreIndex < -1 || ignoreIndex >= _placedObjects.Count) return false;

        float clampedX = PlaygroundRules.ClampX(x);
        float clampedY = PlaygroundRules.ClampY(y);
        float minimumDistanceSquared =
            PlaygroundRules.MinimumSpacing * PlaygroundRules.MinimumSpacing;

        for (int i = 0; i < _placedObjects.Count; i++)
        {
            if (i == ignoreIndex) continue;

            float dx = _placedObjects[i].X - clampedX;
            float dy = _placedObjects[i].Y - clampedY;
            if (dx * dx + dy * dy < minimumDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    // 치우면 보유로 돌아간다. 보유 수는 놓은 것을 포함한 총량이라 건드리지 않는다.
    public bool TryRemovePlacedObject(int index)
    {
        if (index < 0 || index >= _placedObjects.Count) return false;

        _placedObjects.RemoveAt(index);
        return true;
    }

    // 쓰는 쪽에서 나올 수 없는 값만 막는다. 밸런스로 달라질 수 있는 값은 흡수한다.
    //  - 모르는 종류, 상한을 넘은 배치 : 흘려보내거나 잘라낸다
    //  - 방 밖 좌표 : 방 크기는 바뀔 수 있으므로 가둔다
    //  - 음수 보유 수 : 정상적으로 만들 수 없으므로 막는다
    private void RestorePlayground(
        IEnumerable<PlacedPlaygroundObject> placedObjects,
        IReadOnlyList<int> ownedCounts)
    {
        if (ownedCounts != null)
        {
            for (int i = 0; i < ownedCounts.Count && i < _ownedPlaygroundObjects.Length; i++)
            {
                int owned = ownedCounts[i];
                if (owned < 0)
                {
                    throw new ArgumentException(
                        $"놀이터 오브젝트 보유 수가 올바르지 않습니다. : {owned}");
                }

                _ownedPlaygroundObjects[i] = Math.Min(owned, PlaygroundRules.MaxPerType);
            }
        }

        if (placedObjects != null)
        {
            foreach (PlacedPlaygroundObject placed in placedObjects)
            {
                if (!PlaygroundRules.IsValid(placed.Type)) continue;
                if (!IsFinite(placed.X) || !IsFinite(placed.Y))
                {
                    throw new ArgumentException(
                        $"놀이터 오브젝트 위치가 올바르지 않습니다. : ({placed.X}, {placed.Y})");
                }

                if (GetPlacedPlaygroundObjectCount(placed.Type) >=
                    PlaygroundRules.MaxPerType)
                {
                    continue;
                }

                // 간격은 밸런스 값이라 나중에 넓어질 수 있다. 예전 저장끼리 겹치면
                // 먼저 저장된 것만 놓고 나머지는 보유 상태로 돌려 막지 않고 흡수한다.
                if (!IsPlaygroundPositionAvailable(placed.X, placed.Y)) continue;

                _placedObjects.Add(new PlacedPlaygroundObject(
                    placed.Type,
                    PlaygroundRules.ClampX(placed.X),
                    PlaygroundRules.ClampY(placed.Y)));
            }
        }

        // 상한을 낮추는 개편이 있으면 놓은 수가 보유 수를 넘을 수 있다.
        // 놓여 있는 것이 사실이므로 보유 수를 그쪽에 맞춘다.
        for (int i = 0; i < _ownedPlaygroundObjects.Length; i++)
        {
            int placedCount = GetPlacedPlaygroundObjectCount((EPlaygroundObjectType)i);
            if (_ownedPlaygroundObjects[i] < placedCount)
            {
                _ownedPlaygroundObjects[i] = placedCount;
            }
        }
    }

    public void UpdateBackgroundProgress(
        EBackgroundTheme selectedBackgroundTheme,
        bool backgroundUnlockCompleted)
    {
        if (!BackgroundThemeRules.IsValid(selectedBackgroundTheme))
        {
            throw new ArgumentException(
                $"올바른 배경 테마가 아닙니다. : {selectedBackgroundTheme}");
        }

        if (selectedBackgroundTheme != EBackgroundTheme.Ground &&
            !BackgroundThemeRules.IsUnlocked(HighestGrade))
        {
            throw new InvalidOperationException("배경 테마가 아직 해금되지 않았습니다.");
        }

        if (!IsBackgroundThemeOwned(selectedBackgroundTheme))
        {
            throw new InvalidOperationException("가지고 있지 않은 배경 테마입니다.");
        }

        SelectedBackgroundTheme = selectedBackgroundTheme;
        BackgroundUnlockCompleted = backgroundUnlockCompleted &&
                                    BackgroundThemeRules.IsUnlocked(HighestGrade);
    }

    public void AddPendingTicket()
    {
        PendingTickets++;
    }

    // 한 장 줍는다. 남은 장수가 없으면 아무것도 하지 않고 false를 준다.
    //
    // 화면의 오브젝트 수와 저장된 장수는 어긋날 수 있다. 표시 상한을 넘은 몫은
    // 저장에만 남기 때문이다. 그래서 오브젝트가 아니라 저장이 판정 근거다.
    public bool TryConsumePendingTicket()
    {
        if (PendingTickets <= 0) return false;

        PendingTickets--;

        return true;
    }

    public void SetAutoSpawnEnabled(bool isEnabled)
    {
        IsAutoSpawnEnabled = isEnabled;
    }

    public bool IsTutorialCompleted(string tutorialId)
    {
        return !string.IsNullOrWhiteSpace(tutorialId) &&
               _completedTutorials.Contains(tutorialId);
    }

    // 새로 기록했으면 true. 이미 있으면 저장할 필요가 없다.
    public bool TryMarkTutorialCompleted(string tutorialId)
    {
        if (string.IsNullOrWhiteSpace(tutorialId))
        {
            throw new ArgumentException("튜토리얼 식별자가 비어 있습니다.", nameof(tutorialId));
        }

        if (_completedTutorials.Contains(tutorialId)) return false;

        _completedTutorials.Add(tutorialId);
        return true;
    }

    public bool TryMarkMainEndingSeen()
    {
        if (MainEndingSeen || NormalCollectionCount < NormalCollectionRules.MainEndingCount)
        {
            return false;
        }

        MainEndingSeen = true;
        return true;
    }

    public bool RecordSpecialGachaResult(bool wasSpecial)
    {
        if (NormalCollectionCount < NormalCollectionRules.HiddenFeverCount)
        {
            return false;
        }

        int nextMissCount = wasSpecial
            ? 0
            : Math.Min(SpecialGachaMissCount + 1, SpecialGachaFever.MaximumMissCount);
        if (nextMissCount == SpecialGachaMissCount) return false;

        SpecialGachaMissCount = nextMissCount;
        return true;
    }

    public void UpdateHighestGrade(ESlimeGrade newGrade)
    {
        ValidateGrade(newGrade);
        if (newGrade <= HighestGrade)
        {
            throw new ArgumentException(
                $"새 등급은 현재 최고 등급보다 높아야 합니다. : {newGrade} <= {HighestGrade}");
        }

        HighestGrade = newGrade;
    }

    public void AddSlime(SlimeInstance instance)
    {
        ValidateInstance(instance);
        if (_activeSlimes.Exists(
                active => active.InstanceId == instance.InstanceId))
        {
            throw new InvalidOperationException(
                $"이미 등록된 슬라임 개체입니다. : {instance.InstanceId}");
        }

        _activeSlimes.Add(instance);
    }

    public ESlimeGrade? MoveSlime(string instanceId, ESlimeLocation location)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("이동할 슬라임 개체 ID가 비어 있습니다.", nameof(instanceId));
        }

        if (!SlimeLocationRules.IsValid(location))
        {
            throw new ArgumentException(
                $"유효하지 않은 슬라임 위치입니다. : {location}",
                nameof(location));
        }

        SlimeInstance instance = _activeSlimes.Find(
            active => active.InstanceId == instanceId);
        if (instance == null)
        {
            throw new InvalidOperationException(
                $"저장 상태에 없는 슬라임은 이동할 수 없습니다. : {instanceId}");
        }

        if (instance.Location == location)
        {
            throw new InvalidOperationException(
                $"이미 해당 위치에 있는 슬라임입니다. : {instanceId}, {location}");
        }

        if (location == ESlimeLocation.DisplayRoom &&
            HasDisplayRoomSlime(instance.Grade, instance.IsSpecial))
        {
            throw new InvalidOperationException(
                "장식장에는 같은 종류와 타입의 슬라임을 한 마리만 보관할 수 있습니다.");
        }

        instance.MoveTo(location);
        if (location == ESlimeLocation.DisplayRoom &&
            !instance.IsSpecial &&
            _registeredNormalCollection.Add(instance.Grade))
        {
            return instance.Grade;
        }

        return null;
    }

    public bool IsNormalCollectionRegistered(ESlimeGrade grade)
    {
        ValidateGrade(grade);
        return _registeredNormalCollection.Contains(grade);
    }

    public bool HasDisplayRoomSlime(ESlimeGrade grade, bool isSpecial)
    {
        return _activeSlimes.Exists(instance =>
            instance.Location == ESlimeLocation.DisplayRoom &&
            instance.Grade == grade &&
            instance.IsSpecial == isSpecial);
    }

    // 모든 대상을 먼저 검증한 뒤 반영해 중간 실패와 같은 Tick 연쇄 합성을 막는다.
    public void MergeSlimesBatch(IReadOnlyList<SlimeMergeRequest> requests)
    {
        if (requests == null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        var usedIds = new HashSet<string>();
        var validated = new List<(SlimeInstance Keeper, SlimeInstance Removed, ESlimeGrade ToGrade)>(requests.Count);
        foreach (SlimeMergeRequest request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.KeeperId) ||
                string.IsNullOrWhiteSpace(request.RemovedId) ||
                request.KeeperId == request.RemovedId ||
                !usedIds.Add(request.KeeperId) ||
                !usedIds.Add(request.RemovedId))
            {
                throw new ArgumentException("한 발동의 합성 대상이 중복되거나 올바르지 않습니다.");
            }

            SlimeInstance keeper = _activeSlimes.Find(
                instance => instance.InstanceId == request.KeeperId);
            SlimeInstance removed = _activeSlimes.Find(
                instance => instance.InstanceId == request.RemovedId);
            if (keeper == null || removed == null)
            {
                throw new InvalidOperationException("저장 상태에 없는 슬라임은 합성할 수 없습니다.");
            }

            if (keeper.Grade != removed.Grade ||
                keeper.Location != removed.Location ||
                request.ToGrade != keeper.Grade + 1)
            {
                throw new InvalidOperationException("같은 위치의 동일 등급만 합성할 수 있습니다.");
            }

            ValidateGrade(request.ToGrade);
            validated.Add((keeper, removed, request.ToGrade));
        }

        foreach (var merge in validated)
        {
            merge.Keeper.PromoteTo(merge.ToGrade);
            _activeSlimes.Remove(merge.Removed);
        }
    }

    private static void ValidateInstance(SlimeInstance instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (string.IsNullOrWhiteSpace(instance.InstanceId))
        {
            throw new ArgumentException("슬라임 개체 ID가 비어 있습니다.");
        }

        ValidateGrade(instance.Grade);
        if (!SlimeLocationRules.IsValid(instance.Location))
        {
            throw new ArgumentException(
                $"유효하지 않은 슬라임 위치입니다. : {instance.Location}");
        }
    }

    private static void ValidateGrade(ESlimeGrade grade)
    {
        if (grade < ESlimeGrade.Grade1 || grade >= ESlimeGrade.Count)
        {
            throw new ArgumentException($"올바른 등급 설정이 아닙니다. : {grade}");
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
