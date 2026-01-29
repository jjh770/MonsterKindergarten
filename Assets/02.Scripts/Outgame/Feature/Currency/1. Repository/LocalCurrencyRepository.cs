// 저장하는 방식
// 1-1. PlayerPrefs + double -> string 을 사용해서 Save Load하는 방식
// 1-2. PlayerPrefs + double -> json   을 사용해서 Save Load하는 방식
// 2. CSV. JSon으로 저장하는 방식
// 3. 서버에 저장하는 방식 / DB에 저장하는 방식
// @. 플랫폼에 따라 저장하는 방식이 달라질 수도, 저장을 호출하는 방식도 모두 다름.

// 데이터의 영속성(저장과 불러오기)에 대한 책임을 가지는 Repository 
// 영속성은 게임이 꺼져도 데이터가 남아있어 다음에 플레이할 때 이어할 수 있게 하는 속성
// 비즈니스 로직과 분리한다.

// 비즈니스 로직은 매니저에게.
// 저장 로직은 레포지토리에게.
// 1. 코드가 깔끔해지고 유지보수가 쉬워진다.
using UnityEngine;

public class LocalCurrencyRepository : ICurrencyRepository
{
    public void Save(CurrencySaveData saveData)
    {
        for (int i = 0; i < (int)ECurrencyType.Count; i++)
        {
            var type = (ECurrencyType)i;

            // 소수점 17자리까지 저장
            PlayerPrefs.SetString(type.ToString(), saveData.Currencies[i].ToString("G17"));
        }
    }

    public CurrencySaveData Load()
    {
        CurrencySaveData data = CurrencySaveData.Default;
        for (int i = 0; i < (int)ECurrencyType.Count; i++)
        {
            var type = (ECurrencyType)i;
            string key = type.ToString();

            if (PlayerPrefs.HasKey(key))
            {
                data.Currencies[i] = double.Parse(PlayerPrefs.GetString(key, "0"));
            }
        }
        return data;
    }
}


