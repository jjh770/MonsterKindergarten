using UnityEngine;

public sealed class TicketUI : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text _ticketText;
    [SerializeField] private CurrencyManager _currencyManager;
    [SerializeField] private GameManager _gameManager;

    private bool _isInitialized;

    private void Start()
    {
        if (_ticketText == null || _currencyManager == null || _gameManager == null)
        {
            Debug.LogError("TicketUI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _gameManager.AllDataInitialized += OnAllDataInitialized;
        _currencyManager.DataChanged += OnCurrencyChanged;

        if (_gameManager.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        _gameManager.AllDataInitialized -= OnAllDataInitialized;
        if (_currencyManager != null)
        {
            _currencyManager.DataChanged -= OnCurrencyChanged;
        }
    }

    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        UpdateUI();
    }

    private void OnCurrencyChanged(ECurrencyType type, Currency amount)
    {
        if (!_isInitialized || type != ECurrencyType.GachaTicket) return;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!_isInitialized) return;

        _ticketText.text = _currencyManager.Get(ECurrencyType.GachaTicket).ToString();
    }
}
