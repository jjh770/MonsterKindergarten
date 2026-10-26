using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

// 이전 앱이 쓴 세이브가 지금 앱에서 그대로 읽히고, 다시 저장하면 현재 형식이 되는지 확인한다.
// 0.1.18은 재화 v2(종류별 키)와 슬라임 v16을 쓴다. 승격이 틀리면 출시 후 기존 플레이어가
// 진행도를 잃으므로, 저장 형식을 바꿀 때마다 여기에 그 버전의 문서를 추가한다.
//
// 로컬 저장소를 그대로 쓰므로 PlayerPrefs에 시험용 사용자 키를 쓰고, 끝나면 지운다.
public class SaveMigrationTests
{
    private const string TestUserId = "SaveMigrationTest";
    private static readonly DateTime RestoredAtUtc = new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    [SetUp]
    [TearDown]
    public void DeleteTestSaves()
    {
        new LocalCurrencyRepository(TestUserId).Delete();
        new PlayerPrefsSlimeStatusRepository(TestUserId).Delete();
        PlayerPrefs.Save();
    }

    // --- 재화 ---

    [Test]
    public void 재화_v2_종류별_키는_잔액과_저장_시각을_그대로_읽는다()
    {
        WriteLegacyCurrency(80943735d, 12d);

        SaveLoadResult<CurrencySaveData> result = LoadCurrency();

        Assert.IsTrue(result.IsLoaded, result.FailureMessage);
        Assert.AreEqual(2, result.Data.SchemaVersion);
        CollectionAssert.AreEqual(new[] { 80943735d, 12d }, result.Data.Currencies);
        Assert.AreEqual("2026-10-20T02:10:59.0225750Z", result.Data.LastSaveTime);
        // v2에는 탐험 지급 기록이 없다. 기본 문서에서 시작하므로 빈 기록이 된다.
        Assert.AreEqual(0, result.Data.ExpeditionSettlement.ToDomain().Sequence);
    }

    [Test]
    public void 재화_v2를_다시_저장하면_v4_문서가_되고_같은_잔액으로_읽힌다()
    {
        WriteLegacyCurrency(80943735d, 12d);
        CurrencySaveData legacy = LoadCurrency().Data;

        var repository = new LocalCurrencyRepository(TestUserId);
        repository.Save(new CurrencySaveData
        {
            SchemaVersion = GameDataDomains.Currency.CurrentSchemaVersion,
            Currencies = legacy.Currencies,
            ExpeditionSettlement = ExpeditionSettlementSaveData.FromDomain(ExpeditionSettlement.Empty),
            LastSaveTime = legacy.LastSaveTime,
        });
        SaveLoadResult<CurrencySaveData> result = LoadCurrency();

        Assert.IsTrue(result.IsLoaded, result.FailureMessage);
        Assert.AreEqual(4, result.Data.SchemaVersion);
        Assert.AreEqual(4, PlayerPrefs.GetInt($"{TestUserId}_Currency_SchemaVersion"));
        CollectionAssert.AreEqual(new[] { 80943735d, 12d }, result.Data.Currencies);
        Assert.AreEqual(0, result.Data.ExpeditionSettlement.ToDomain().Sequence);
    }

    [Test]
    public void 재화_표식이_v3_이상인데_문서가_없으면_빈_재화로_시작하지_않는다()
    {
        // 새 앱이 저장한 뒤 이전 앱이 종류별 키만 다시 쓴 상태다. 키를 읽으면 탐험 지급 기록이 사라진다.
        WriteLegacyCurrency(100d, 1d);
        PlayerPrefs.SetInt($"{TestUserId}_Currency_SchemaVersion", 3);

        SaveLoadResult<CurrencySaveData> result = LoadCurrency();

        Assert.IsTrue(result.IsFailed);
        Assert.AreEqual(ESaveLoadFailure.Unreadable, result.Failure);
    }

    [Test]
    public void 재화_표식이_앱보다_높으면_지원하지_않는_버전으로_거부한다()
    {
        WriteLegacyCurrency(100d, 1d);
        PlayerPrefs.SetInt($"{TestUserId}_Currency_SchemaVersion", GameDataDomains.Currency.CurrentSchemaVersion + 1);

        SaveLoadResult<CurrencySaveData> result = LoadCurrency();

        Assert.IsTrue(result.IsFailed);
        Assert.AreEqual(ESaveLoadFailure.UnsupportedVersion, result.Failure);
    }

    // --- 슬라임 ---

    [Test]
    public void 슬라임_v16_문서는_빈_탐험과_빈_지급_기록으로_승격되고_복원된다()
    {
        WriteSlimeStatus(CreateV16Document());

        SaveLoadResult<SlimeStatusSaveData> result = LoadSlimeStatus();

        Assert.IsTrue(result.IsLoaded, result.FailureMessage);
        SlimeStatusSaveData saveData = result.Data;
        Assert.AreEqual(GameDataDomains.SlimeStatus.CurrentSchemaVersion, saveData.SchemaVersion);
        Assert.IsTrue(saveData.WasMigrated);
        Assert.IsNotNull(saveData.ActiveExpeditions);
        Assert.IsEmpty(saveData.ActiveExpeditions);

        SlimeStatus status = Restore(saveData, out bool needsMigrationSave);
        Assert.IsTrue(needsMigrationSave);
        Assert.AreEqual(4, status.ActiveSlimes.Count);
        Assert.AreEqual(2, status.NormalCollectionCount);
        Assert.AreEqual(ESlimeGrade.Grade9, status.HighestGrade);
        Assert.IsEmpty(status.ActiveExpeditions);
        Assert.AreEqual(0, status.ExpeditionSettlement.Sequence);
        Assert.AreEqual(2, status.PlacedObjects.Count);
        Assert.AreEqual(1, status.GetOwnedPlaygroundObjectCount(EPlaygroundObjectType.Bumper));
        Assert.AreEqual(1, status.GetOwnedPlaygroundObjectCount(EPlaygroundObjectType.Cannon));
        // v16에 없던 장식 종류는 짧은 보유 목록에서 미보유로 읽힌다.
        for (int type = 2; type < (int)EPlaygroundObjectType.Count; type++)
            Assert.AreEqual(0, status.GetOwnedPlaygroundObjectCount((EPlaygroundObjectType)type));
    }

    [Test]
    public void 슬라임_v18의_단일_탐험은_목록으로_옮겨지고_지급_순번이_이어진다()
    {
        JObject document = CreateV16Document();
        document["SchemaVersion"] = 18;
        // 탐험으로 받은 뽑기권은 전체 획득 수에 들어 있어야 한다.
        document["GachaTicketsObtainedTotal"] = 10;
        document["ExpeditionSettlement"] = JObject.FromObject(new
        {
            Sequence = 3L, Points = 300000d, Tickets = 3L, DecorationMask = 0,
        });
        document["ActiveExpedition"] = JObject.FromObject(new ExpeditionRunSaveData
        {
            RunId = "run-4",
            DestinationId = "expedition-short",
            Sequence = 4,
            Members = new List<ExpeditionMemberSaveData>
            {
                new() { InstanceId = RoomSlimeId, Grade = (int)ESlimeGrade.Grade3, IsSpecial = false },
            },
            StartedAtUtc = RestoredAtUtc.AddHours(-1).ToString("O"),
            ReturnsAtUtc = RestoredAtUtc.AddHours(1).ToString("O"),
            Points = 1000d,
            Tickets = 1,
            Decoration = -1,
        });
        WriteSlimeStatus(document);

        SaveLoadResult<SlimeStatusSaveData> result = LoadSlimeStatus();

        Assert.IsTrue(result.IsLoaded, result.FailureMessage);
        Assert.IsNull(result.Data.ActiveExpedition);
        Assert.AreEqual(1, result.Data.ActiveExpeditions.Count);
        Assert.AreEqual(3, result.Data.ExpeditionSettlement.LegacyCompletedSequence);

        SlimeStatus status = Restore(result.Data, out _);
        Assert.AreEqual(1, status.ActiveExpeditions.Count);
        Assert.AreEqual("run-4", status.ActiveExpeditions[0].RunId);
        Assert.AreEqual(3, status.ExpeditionSettlement.Sequence);
        Assert.AreEqual(300000d, status.ExpeditionSettlement.Points);
    }

    [Test]
    public void 승격한_슬라임_상태를_다시_저장하면_현재_버전으로_같은_상태가_읽힌다()
    {
        WriteSlimeStatus(CreateV16Document());
        SlimeStatus status = Restore(LoadSlimeStatus().Data, out _, out NormalSlimeCollectionStats stats);

        new PlayerPrefsSlimeStatusRepository(TestUserId).Save(SlimeStatusSaveMapper.Build(status, stats));
        SaveLoadResult<SlimeStatusSaveData> result = LoadSlimeStatus();

        Assert.IsTrue(result.IsLoaded, result.FailureMessage);
        Assert.IsFalse(result.Data.WasMigrated);
        SlimeStatus reloaded = Restore(result.Data, out bool needsMigrationSave);
        Assert.IsFalse(needsMigrationSave);
        Assert.AreEqual(status.ActiveSlimes.Count, reloaded.ActiveSlimes.Count);
        Assert.AreEqual(status.NormalCollectionCount, reloaded.NormalCollectionCount);
        Assert.AreEqual(status.PlacedObjects.Count, reloaded.PlacedObjects.Count);
    }

    // --- 0.1.18 문서 ---

    private const string RoomSlimeId = "928cc4c88c9d457c9de918f9ae47770a";

    private static void WriteLegacyCurrency(double points, double tickets)
    {
        PlayerPrefs.SetString($"{TestUserId}_{ECurrencyType.Point}", points.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.SetString($"{TestUserId}_{ECurrencyType.GachaTicket}", tickets.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.SetString($"{TestUserId}_Currency_LastSaveTime", "2026-10-20T02:10:59.0225750Z");
        PlayerPrefs.SetInt($"{TestUserId}_Currency_SchemaVersion", 2);
    }

    // 0.1.18이 쓴 모양이다. 탐험 필드가 없고, 소품 보유 목록은 범퍼와 대포 두 칸이다.
    private static JObject CreateV16Document()
    {
        SlimeStatusSaveData data = SlimeStatusSaveData.Default;
        data.HighestGrade = (int)ESlimeGrade.Grade9;
        data.ActiveSlimes = new List<SlimeInstanceSaveData>
        {
            new(RoomSlimeId, ESlimeGrade.Grade3, false, ESlimeLocation.DisplayRoom),
            new("631388f1545f40059fc85ebcaa2b65dd", ESlimeGrade.Grade1, false, ESlimeLocation.DisplayRoom),
            new("0c2e1d7a9b8f4e6a8d3c5b7a9e1f2d4c", ESlimeGrade.Grade9, false, ESlimeLocation.MainField),
            new("5b7d9f1e3a2c4b6d8e0f1a3c5e7b9d2f", ESlimeGrade.Grade1, false, ESlimeLocation.MainField),
        };
        data.NormalCollectionRegistered[0] = true;
        data.NormalCollectionRegistered[2] = true;
        data.CompletedTutorials = new List<string> { "Tutorial", "DisplayRoomTutorial" };
        data.GameStartedAtUtc = "2026-10-19T05:36:20.4178039Z";
        data.PlacedObjects = new List<PlacedObjectSaveData>
        {
            new() { Type = (int)EPlaygroundObjectType.Cannon, X = 1f, Y = -1.5f },
            new() { Type = (int)EPlaygroundObjectType.Bumper, X = -2.4f, Y = -1f },
        };
        data.OwnedPlaygroundObjects = new List<int> { 1, 1 };

        JObject document = JObject.Parse(JsonConvert.SerializeObject(data));
        document.Remove("ActiveExpeditions");
        document.Remove("ActiveExpedition");
        document.Remove("ExpeditionSettlement");
        document["SchemaVersion"] = 16;
        return document;
    }

    private static void WriteSlimeStatus(JObject document)
    {
        PlayerPrefs.SetString($"{TestUserId}_SlimeStatus", document.ToString(Formatting.None));
    }

    private static SaveLoadResult<CurrencySaveData> LoadCurrency() =>
        new LocalCurrencyRepository(TestUserId).Load().GetAwaiter().GetResult();

    private static SaveLoadResult<SlimeStatusSaveData> LoadSlimeStatus() =>
        new PlayerPrefsSlimeStatusRepository(TestUserId).Load().GetAwaiter().GetResult();

    private static SlimeStatus Restore(SlimeStatusSaveData saveData, out bool needsMigrationSave) =>
        Restore(saveData, out needsMigrationSave, out _);

    private static SlimeStatus Restore(
        SlimeStatusSaveData saveData, out bool needsMigrationSave, out NormalSlimeCollectionStats stats)
    {
        bool restored = SlimeStatusSaveMapper.TryRestore(
            saveData, RestoredAtUtc, out SlimeStatus status, out stats, out needsMigrationSave, out string failure);
        Assert.IsTrue(restored, failure);
        return status;
    }
}
