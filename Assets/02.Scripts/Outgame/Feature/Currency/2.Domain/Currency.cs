// 재화를 의미하는 도메인 모델


// Currency와 같이 도메인을 만들어야하는 경우
// 1, 재화가 여러 곳에서 사용 된다. (UI, 상점, 업그레이드 등 다양한 콘텐츠)
// 2. 포맷팅이 통일되어야 한다. 무조건
// 3. 재화끼리 연산이 빈번하다.
// 4. 팀 프로젝트에 있어서 도메인 관련 실수를 방지하고 싶다. 

// Currency와 같은 도메인이 필요없는 경우
// 1. 게임을 빠르게 만들고 싶다.
// 2. 재화가 한 종류 뿐이고 사용처도 많지 않다.
// 3. 팀원 없이 혼자 개발에서 도메인에 대한 지식이 바로잡혀있다.

// struct vs class (구조체와 클래스)
// struct는 int, double 처럼 값으로 동작하기에 안성맞춤
// '재화'는 "값"이 중요하다.

using System;
using Utility;

public readonly struct Currency
{
    // 우리의 게임에서의 재화 규칙.
    // 1. 음수면 안된다.
    // 2. 정해진 표기법이 있다. (ToFormattedString())
    // 3. 재화간에는 +- 가 되어야한다.
    public readonly double Value;

    public Currency(double value)
    {
        // 1. 음수 유효성 검사 
        if (value < 0)
        {
            // 이런 잘못된 데이터가 들어왔다는 것은 여러가지 부작용이 생길 수 있음.
            // 게임 플레이 도중에 그 부작용을 느끼는 것보다
            // 시작단계에서 에러를 뱉어버리는 것이 유지보수 면에서 편함.
            throw new Exception("Currency 값은 0보다 작을 수 없습니다.");
        }
        Value = value;
    }

    // 2. 정해진 표기법 지키기
    // ToString이란 객체를 문자열로 변환될 떄 암시적으로 호출되는 메서드
    // 이걸 메서드 오버라이딩하여 특정 포맷으로 문자가 변환되게끔 강제한다.
    public override string ToString()
    {
        return Value.ToForamttedString();
    }

    // +@ 기본 연산을 하고 싶다!
    // 연산자 오버라이딩
    // 객체간의 연산자 (+, -, >, <)할 때 암시적으로 호출되는 메서드
    // +@-1 더하기
    public static Currency operator +(Currency currency1, Currency currency2)
    {
        return new Currency(currency1.Value + currency2.Value);
    }
    // +@-2 뺴기
    public static Currency operator -(Currency currency1, Currency currency2)
    {
        return new Currency(currency1.Value - currency2.Value);
    }
    // +@-3 비교 연산자
    public static bool operator >=(Currency currency1, Currency currency2)
    {
        return currency1.Value >= currency2.Value;
    }
    public static bool operator <=(Currency currency1, Currency currency2)
    {
        return currency1.Value <= currency2.Value;
    }
    public static bool operator >(Currency currency1, Currency currency2)
    {
        return currency1.Value > currency2.Value;
    }
    public static bool operator <(Currency currency1, Currency currency2)
    {
        return currency1.Value < currency2.Value;
    }
    // +@-4 double → Currency 암시적 변환    
    public static implicit operator Currency(double value)
    {
        return new Currency(value);
    }

    // +@-5 Currency -> double 암시적 변환
    public static explicit operator double(Currency currency)
    {
        return currency.Value;
    }
}
