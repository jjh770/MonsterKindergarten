public readonly struct CurrencyChange
{
    public ECurrencyType Type { get; }
    public Currency Amount { get; }
    public bool IsSpend { get; }

    private CurrencyChange(
        ECurrencyType type,
        Currency amount,
        bool isSpend)
    {
        Type = type;
        Amount = amount;
        IsSpend = isSpend;
    }

    public static CurrencyChange Add(ECurrencyType type, Currency amount)
    {
        return new CurrencyChange(type, amount, isSpend: false);
    }

    public static CurrencyChange Spend(ECurrencyType type, Currency amount)
    {
        return new CurrencyChange(type, amount, isSpend: true);
    }
}
