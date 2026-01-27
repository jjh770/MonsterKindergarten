using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public double AutoPoint;
    public double ManualPoint;

    // int(21억) long(경) bigInteger(숫자를 쪼개서 계산하기 때문에 매우 느림)
    // float(10^38), double(10^900), decimal (부동소수점) 같은 자료형 크기 대비 범위가 엄청나게 큼
    // float의 정밀도 7자리
    // double의 정밀도 15~16자리
    // decimal의 정밀도 28~29자리
    // 숫자의 레벨이 10조단위~ 경단위로 넘어간다면 1, 10과 같은 엄청 작은 숫자는 신경도 쓰지 않음.
    // double로 게임 포인트를 작성하면 1단위의 값은 정확할지 모르지만 계산이 빠름
    // 만약 1단위의 계산도 정확하게 하고 싶다면 bigInteger를 사용
    private double _point;
    public double Point
    {
        get => _point;
        set
        {
            _point = value;
            OnPointChanged?.Invoke(_point);
        }
    }

    public event Action<double> OnPointChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddPoint(double amount)
    {
        Point += amount;
    }

    public void SubtractPoint(double amount)
    {
        Point -= amount;
    }
}
