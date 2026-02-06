using UnityEngine;

// 슬라임 스펙 데이터 들의 집합 -> 슬라임 스펙 테이블
[CreateAssetMenu(fileName = "SlimeSpecTable", menuName = "DataTable/Slime Spec Table")]
public class SlimeSpecTable : ScriptableObject
{
    [SerializeField] public SlimeSpecData[] slimeSpecs = new SlimeSpecData[10];
}
