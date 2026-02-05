using TMPro;
using UnityEngine;

public class PointUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _pointText;

    private ESlimeGrade _highestGrade = ESlimeGrade.Grade1;

    private void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnDataChanged += OnPointChanged;
            CurrencyManager.Instance.OnDataInitialized += OnDataInitialized;
        }

        if (SlimeSpawner.Instance != null)
        {
            _highestGrade = SlimeSpawner.Instance.HighestGrade;
        }

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnDataChanged -= OnPointChanged;
            CurrencyManager.Instance.OnDataInitialized -= OnDataInitialized;
        }
    }

    private void OnDataInitialized()
    {
        UpdateUI();
    }

    private void OnPointChanged(ECurrencyType type, Currency point)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_pointText != null && GameManager.Instance != null)
        {
            int spriteIndex = (int)_highestGrade;
            // 최종 사용자 입장에서 double은 그냥 숫자일 뿐이지 '재화'인지 모름
            // 재화는 0미만일 수 없지만, 음수가 가능해질 수 있음.
            // 재화는 표현할 떄 무조건 ToFormattinedString()을 써야함. 하지만 ToString() 과 같이 임의로 수행할 수 있음.
            // -> 그렇기 때문에 Currency라는 "재화"를 만들어 규칙을 만들어야함. -> 02.Domain폴더 - Currency.cs
            _pointText.text = $"<sprite={spriteIndex}>{CurrencyManager.Instance.Point}";
        }
    }
}
