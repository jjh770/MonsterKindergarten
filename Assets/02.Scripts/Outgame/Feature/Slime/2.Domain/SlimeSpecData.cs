using System;

// 슬라임 스펙 데이터 -> 슬라임 레벨별로 가지고 있는 데이터들의 모음
[Serializable]
public class SlimeSpecData
{
    // 슬라임 이름
    public string Name;
    // 장식장 정보 UI에 표시할 한 문장 소개
    [UnityEngine.TextArea(2, 3)]
    public string Description;
    // 슬라임 등급 (레벨) 
    public ESlimeGrade Grade;
    // 슬라임의 클릭 포인트
    public int Point;
    // 슬라임의 자동 클릭 빈도수
    public float AutoClickInterval;
    // 슬라임의 기본 스프라이트
    public UnityEngine.Sprite Sprite;
}
