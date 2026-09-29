// 기획서 §15 - 특별한 슬라임 결과의 종류를 고른다. 특별 여부의 판정은 여기서 하지
// 않는다. 확률은 피버 상태에 따라 달라지므로 SlimeManager가 들고 있다.
//
// 후보는 Lv.1부터 최고 해금 등급까지다. 일반 가챠와 달리 최고 등급 자신도 나올 수
// 있고, 그 위는 절대 나오지 않는다. 가챠가 아직 만나지 못한 등급의 슬라임을
// 만들어 주면 안 되기 때문이다.
//
// 아직 특별 도감에 등록하지 못한 종류는 진행도에 따라 무게를 올려 뽑히기 쉽게 한다.
// 마지막 몇 종을 채우는 데 걸리는 시간이 끝없이 늘어지지 않게 하는 보정이다.
// 표는 기획서에서 확정된 값이라 에셋으로 빼지 않았다.
public static class SpecialGachaPool
{
    private const int RegisteredWeight = 1;

    // 등록 수가 이 값 이하이면 해당 무게. 표의 마지막 줄은 19종 이상을 받는다.
    private static readonly int[] UnregisteredWeightUpperBounds = { 10, 15, 18 };
    private static readonly int[] UnregisteredWeights = { 2, 3, 5 };
    private const int LastUnregisteredWeight = 8;

    public static bool TryPick(
        ESlimeGrade highestGrade,
        int registeredCount,
        System.Func<ESlimeGrade, bool> isRegistered,
        out ESlimeGrade grade)
    {
        grade = ESlimeGrade.None;
        if (highestGrade < ESlimeGrade.Grade1 || isRegistered == null) return false;

        int unregisteredWeight = GetUnregisteredWeight(registeredCount);
        int totalWeight = 0;
        for (int value = (int)ESlimeGrade.Grade1; value <= (int)highestGrade; value++)
        {
            totalWeight += GetWeight((ESlimeGrade)value, unregisteredWeight, isRegistered);
        }

        if (totalWeight <= 0) return false;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        for (int value = (int)ESlimeGrade.Grade1; value <= (int)highestGrade; value++)
        {
            roll -= GetWeight((ESlimeGrade)value, unregisteredWeight, isRegistered);
            if (roll >= 0) continue;

            grade = (ESlimeGrade)value;
            return true;
        }

        return false;
    }

    public static int GetUnregisteredWeight(int registeredCount)
    {
        for (int i = 0; i < UnregisteredWeightUpperBounds.Length; i++)
        {
            if (registeredCount <= UnregisteredWeightUpperBounds[i])
            {
                return UnregisteredWeights[i];
            }
        }

        return LastUnregisteredWeight;
    }

    private static int GetWeight(
        ESlimeGrade grade,
        int unregisteredWeight,
        System.Func<ESlimeGrade, bool> isRegistered)
    {
        return isRegistered(grade) ? RegisteredWeight : unregisteredWeight;
    }
}
