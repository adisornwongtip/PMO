namespace PayFlow.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Success = 2,
    Failed = 3,
    Refunded = 4,
    PartiallyRefunded = 5
}

public enum PaymentMethod
{
    CreditCard = 1,
    PromptPayQr = 2,
    TrueMoney = 3,
    LinePay = 4,
    ShopeePay = 5,
    BankTransfer = 6
}

public enum ReconcileStatus
{
    Pending = 0,
    Matched = 1,
    Unmatched = 2,
    MissingInStatement = 3,
    AmountMismatch = 4,
    FeeDiscrepancy = 5
}
