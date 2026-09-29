using System;
using System.Collections.Generic;

// 저장 도메인을 추가할 때 갱신할 단일 등록 지점이다. GameManager의 초기화/전체
// 저장과 GameDataResetService의 로컬/클라우드 삭제가 모두 이 목록을 사용한다.
public sealed class GameDataDomainDefinition
{
    private readonly Action<string> _localDataDeleter;

    public string DisplayName { get; }
    public string CloudCollectionName { get; }
    public int CurrentSchemaVersion { get; }

    public GameDataDomainDefinition(
        string displayName,
        string cloudCollectionName,
        int currentSchemaVersion,
        Action<string> localDataDeleter)
    {
        DisplayName = displayName;
        CloudCollectionName = cloudCollectionName;
        CurrentSchemaVersion = currentSchemaVersion;
        _localDataDeleter = localDataDeleter;
    }

    public void DeleteLocalData(string userId)
    {
        _localDataDeleter(userId);
    }
}

public static class GameDataDomains
{
    // v2: ECurrencyType에 가챠권을 추가해 재화 배열 길이가 늘었다.
    public static readonly GameDataDomainDefinition Currency = new(
        displayName: "재화",
        cloudCollectionName: "Currency",
        currentSchemaVersion: 2,
        localDataDeleter: userId => new LocalCurrencyRepository(userId).Delete());

    // v11: 엔딩 크레딧용 게임 시작일과 메인 엔딩 도달일을 추가했다.
    public static readonly GameDataDomainDefinition SlimeStatus = new(
        displayName: "슬라임",
        cloudCollectionName: "SlimeStatus",
        currentSchemaVersion: 11,
        localDataDeleter: userId =>
            new PlayerPrefsSlimeStatusRepository(userId).Delete());

    // v1: 최초 업그레이드 저장 형식.
    public static readonly GameDataDomainDefinition Upgrade = new(
        displayName: "업그레이드",
        cloudCollectionName: "Upgrade",
        currentSchemaVersion: 1,
        localDataDeleter: userId =>
            new PlayerPrefsUpgradeRepository(userId).Delete());

    private static readonly GameDataDomainDefinition[] s_all =
    {
        Currency,
        SlimeStatus,
        Upgrade,
    };

    public static IReadOnlyList<GameDataDomainDefinition> All => s_all;
}
