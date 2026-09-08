using UnityEngine;

// 기획서 §14 - 일반 가챠 결과의 후보와 가중치.
//
// 최고 해금 등급 자신은 후보에 없다. 합성으로 새 등급을 처음 만나는 경험을 가챠가
// 앞질러 가져가지 않게 하려는 규칙이다. 그래서 후보는 최고 -1부터 -4까지다.
//
// 값은 기획서에서 확정된 것이라 에셋으로 빼지 않았다. 조정이 필요해지면 그때
// SpawnWeightTable처럼 ScriptableObject로 옮긴다.
public static class NormalGachaPool
{
    // 최고 해금 등급에서 몇 단계 아래인지와 그 가중치. 합계 110.
    private static readonly int[] Offsets = { 1, 2, 3, 4 };
    private static readonly int[] Weights = { 10, 20, 30, 50 };

    // 후보가 Grade1 아래로 내려가면 그 자리는 빼고 남은 것끼리 다시 정규화한다.
    // 가챠는 Lv.7에서 열리므로 정상 경로에서는 넷이 모두 있지만, 해금 등급을
    // 낮추는 밸런스 변경이 곧바로 예외가 되지 않도록 열어 둔다.
    public static bool TryPick(ESlimeGrade highestGrade, out ESlimeGrade result)
    {
        result = ESlimeGrade.None;

        int totalWeight = 0;
        for (int i = 0; i < Offsets.Length; i++)
        {
            if (GetCandidate(highestGrade, i) == ESlimeGrade.None) continue;

            totalWeight += Weights[i];
        }

        if (totalWeight <= 0) return false;

        int roll = Random.Range(0, totalWeight);
        for (int i = 0; i < Offsets.Length; i++)
        {
            ESlimeGrade candidate = GetCandidate(highestGrade, i);
            if (candidate == ESlimeGrade.None) continue;

            roll -= Weights[i];
            if (roll >= 0) continue;

            result = candidate;
            return true;
        }

        return false;
    }

    private static ESlimeGrade GetCandidate(ESlimeGrade highestGrade, int index)
    {
        int grade = (int)highestGrade - Offsets[index];
        return grade >= (int)ESlimeGrade.Grade1
            ? (ESlimeGrade)grade
            : ESlimeGrade.None;
    }
}
