using System;

public enum EPlaygroundShopPurchaseResult
{
    Success,
    InsufficientPoints,
    Unavailable,
}

// 상점 구매에 참여하는 두 도메인의 처리 순서를 소유한다.
// UI는 어떤 상품을 눌렀는지 전달하고 결과를 보여 주기만 한다.
public sealed class PlaygroundShopPurchaseService
{
    private readonly CurrencyManager _currencyManager;
    private readonly SlimeManager _slimeManager;

    public PlaygroundShopPurchaseService(
        CurrencyManager currencyManager,
        SlimeManager slimeManager)
    {
        _currencyManager = currencyManager ??
                           throw new ArgumentNullException(nameof(currencyManager));
        _slimeManager = slimeManager ??
                        throw new ArgumentNullException(nameof(slimeManager));
    }

    public EPlaygroundShopPurchaseResult TryBuyObject(
        EPlaygroundObjectType type,
        Currency price)
    {
        return TryPurchase(
            price,
            () => _slimeManager.TryBuyPlaygroundObject(type));
    }

    public EPlaygroundShopPurchaseResult TryBuyTheme(
        EBackgroundTheme theme,
        Currency price)
    {
        return TryPurchase(
            price,
            () => _slimeManager.TryAddBackgroundTheme(theme));
    }

    private EPlaygroundShopPurchaseResult TryPurchase(
        Currency price,
        Func<bool> grantOwnership)
    {
        EEconomyTransactionResult result =
            EconomyTransactionService.TryPurchase(
                _currencyManager,
                ECurrencyType.Point,
                price,
                grantOwnership);

        if (result == EEconomyTransactionResult.InsufficientCurrency)
        {
            return EPlaygroundShopPurchaseResult.InsufficientPoints;
        }

        return result == EEconomyTransactionResult.Success
            ? EPlaygroundShopPurchaseResult.Success
            : EPlaygroundShopPurchaseResult.Unavailable;
    }
}
