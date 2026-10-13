using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// 게임의 실제 도메인 공식을 사용해 분석용 표를 만든다. 플레이어 데이터는 읽지 않는다.
public static class BalanceSnapshotExporter
{
    private const string UpgradeTablePath = "Assets/02.Scripts/Outgame/Feature/Upgrade/UpgradeSpecTableSO.asset";
    private const string SlimeTablePath = "Assets/02.Scripts/Outgame/Feature/Slime/2.Domain/SlimeSpecTable.asset";
    private const string UpgradeCodePath = "Assets/02.Scripts/Outgame/Feature/Upgrade/2.Domain/Upgrade.cs";
    private const string UpgradeSpecPath = "Assets/02.Scripts/Outgame/Feature/Upgrade/2.Domain/UpgradeSpecData.cs";
    private const string OutputDirectory = "Builds/BalanceReports";

    [MenuItem("Tools/Balance/Export Balance Snapshot", false, 100)]
    public static void Export()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("밸런스 추출은 Play Mode를 종료한 뒤 실행해주세요.");
            return;
        }

        var upgradeTable = AssetDatabase.LoadAssetAtPath<UpgradeSpecTableSO>(UpgradeTablePath);
        var slimeTable = AssetDatabase.LoadAssetAtPath<SlimeSpecTable>(SlimeTablePath);
        if (upgradeTable == null || slimeTable == null)
        {
            throw new InvalidOperationException("밸런스 테이블 경로를 확인해주세요.");
        }

        // 메모리 값과 파일 해시가 다른 보고서를 만들지 않는다. 저장 여부는 사용자가 결정한다.
        if (EditorUtility.IsDirty(upgradeTable) || EditorUtility.IsDirty(slimeTable))
        {
            Debug.LogWarning("변경한 밸런스 에셋을 저장한 뒤 다시 추출해주세요.");
            return;
        }

        var snapshot = new Snapshot
        {
            GeneratedUtc = DateTime.UtcNow.ToString("O"),
            UnityVersion = Application.unityVersion,
        };
        AddSource(snapshot, UpgradeTablePath);
        AddSource(snapshot, SlimeTablePath);
        AddSource(snapshot, UpgradeCodePath);
        AddSource(snapshot, UpgradeSpecPath);
        AppendUpgrades(snapshot, upgradeTable);
        AppendSlimes(snapshot, slimeTable);

        // 모든 계산과 직렬화가 성공한 뒤 새 파일을 만든다. 기존 결과를 덮어쓰지 않는다.
        string json = JsonUtility.ToJson(snapshot, true);
        Directory.CreateDirectory(OutputDirectory);
        string filename = $"balance-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
        string outputPath = Path.Combine(OutputDirectory, filename);
        using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(json);
        }

        Debug.Log($"밸런스 추출 완료: 구매 단계 {snapshot.UpgradeLevels.Count}개, 슬라임 {snapshot.Slimes.Count}종. {outputPath}");
        EditorUtility.RevealInFinder(Path.GetFullPath(outputPath));
    }

    private static void AddSource(Snapshot snapshot, string path)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
        snapshot.Sources.Add(new SourceRecord
        {
            Path = path,
            Sha256 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(),
        });
    }

    private static void AppendUpgrades(Snapshot snapshot, UpgradeSpecTableSO table)
    {
        if (table.Datas == null || table.Datas.Length == 0)
        {
            throw new InvalidOperationException("업그레이드 테이블이 비어 있습니다.");
        }

        var keys = new HashSet<string>();
        foreach (UpgradeSpecData spec in table.Datas)
        {
            if (spec == null || spec.Type == EUpgradeType.Count ||
                !Enum.IsDefined(typeof(EUpgradeType), spec.Type) ||
                !Enum.IsDefined(typeof(ESlimeGrade), spec.SlimeGrade))
            {
                throw new InvalidOperationException("업그레이드 스펙 또는 enum 값이 올바르지 않습니다.");
            }

            string key = $"{spec.Type}/{spec.SlimeGrade}";
            if (!keys.Add(key))
            {
                throw new InvalidOperationException($"중복 업그레이드입니다: {key}");
            }

            // 새 도메인 객체만 읽으므로 실제 레벨이나 에셋은 바뀌지 않는다.
            var upgrade = new Upgrade(spec, 0);
            double cumulativeCost = 0;
            double previousCost = 0;
            for (int level = 0; level < spec.MaxLevel; level++)
            {
                double cost = upgrade.Cost.Value;
                double point = upgrade.Point;
                double nextPoint = upgrade.NextPoint;
                cumulativeCost += cost;
                RequireFinite(cost, key);
                RequireFinite(point, key);
                RequireFinite(nextPoint, key);
                RequireFinite(cumulativeCost, key);
                if (cost <= 0)
                {
                    throw new InvalidOperationException($"구매 비용이 0 이하입니다: {key}, {level}");
                }

                double pointGain = nextPoint - point;
                double costRatio = level > 0 ? cost / previousCost : 0;
                RequireFinite(pointGain, key);
                RequireFinite(costRatio, key);

                snapshot.UpgradeLevels.Add(new UpgradeLevelRecord
                {
                    Type = spec.Type.ToString(),
                    SlimeGrade = (int)spec.SlimeGrade,
                    FromLevel = level,
                    ToLevel = level + 1,
                    Cost = cost,
                    CumulativeCost = cumulativeCost,
                    PointBefore = point,
                    PointAfter = nextPoint,
                    PointGain = pointGain,
                    HasPreviousCost = level > 0,
                    CostRatioToPrevious = costRatio,
                });
                previousCost = cost;
                upgrade.TryLevelUp();
            }
        }
    }

    private static void AppendSlimes(Snapshot snapshot, SlimeSpecTable table)
    {
        if (table.slimeSpecs == null || table.slimeSpecs.Length == 0)
        {
            throw new InvalidOperationException("슬라임 테이블이 비어 있습니다.");
        }

        var grades = new HashSet<ESlimeGrade>();
        foreach (SlimeSpecData spec in table.slimeSpecs)
        {
            if (spec == null || spec.Grade == ESlimeGrade.None ||
                !Enum.IsDefined(typeof(ESlimeGrade), spec.Grade) || !grades.Add(spec.Grade))
            {
                throw new InvalidOperationException("슬라임 스펙 또는 등급이 비어 있거나 중복되었습니다.");
            }

            RequireFinite(spec.AutoClickInterval, spec.Name);
            if (spec.Point < 0 || spec.AutoClickInterval <= 0)
            {
                throw new InvalidOperationException($"슬라임 생산 설정이 올바르지 않습니다: {spec.Name}");
            }

            snapshot.Slimes.Add(new SlimeRecord
            {
                Grade = (int)spec.Grade,
                Name = spec.Name,
                BasePoint = spec.Point,
                AutoClickInterval = spec.AutoClickInterval,
                BasePointsPerSecond = spec.Point / (double)spec.AutoClickInterval,
            });
        }
    }

    private static void RequireFinite(double value, string label)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException($"분석 값이 유한하지 않습니다: {label}");
        }
    }

    [Serializable]
    private sealed class Snapshot
    {
        public int SchemaVersion = 1;
        public string GeneratedUtc;
        public string UnityVersion;
        public string Scope = "Authored tables only; scene bindings are not verified. Not a gameplay simulation or player telemetry.";
        public string PointUnits = "Upgrade Point values use each upgrade's native units. Slime rates exclude upgrades, collection, special and ad bonuses, pauses and field occupancy.";
        public List<SourceRecord> Sources = new List<SourceRecord>();
        public List<UpgradeLevelRecord> UpgradeLevels = new List<UpgradeLevelRecord>();
        public List<SlimeRecord> Slimes = new List<SlimeRecord>();
    }

    [Serializable]
    private sealed class SourceRecord
    {
        public string Path;
        public string Sha256;
    }

    [Serializable]
    private sealed class UpgradeLevelRecord
    {
        public string Type;
        public int SlimeGrade;
        public int FromLevel;
        public int ToLevel;
        public double Cost;
        public double CumulativeCost;
        public double PointBefore;
        public double PointAfter;
        public double PointGain;
        public bool HasPreviousCost;
        public double CostRatioToPrevious;
    }

    [Serializable]
    private sealed class SlimeRecord
    {
        public int Grade;
        public string Name;
        public int BasePoint;
        public double AutoClickInterval;
        public double BasePointsPerSecond;
    }
}
