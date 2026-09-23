// 세이브는 각 항목의 정수 값을 저장한다. 이름은 바꾸어도 되지만, 순서를 바꾸거나
// 중간에 끼워 넣으면 기존 세이브가 다른 업그레이드를 가리킨다.
public enum EUpgradeType
{
    ManualPointPlusAdd,
    AutoPointPlusAdd,
    ManualPointPercentAdd,
    AutoPointPercentAdd,
    SpawnTimeSub,
    MaxCountAdd,
    HigherGradeSpawnWeightAdd,
    AutoMergePairAdd,

    Count,
}
