namespace UltronV1.Core.Mnq
{
    public readonly struct ContractValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }

        public ContractValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public static ContractValidationResult Ok()
            => new ContractValidationResult(true, "Contract valid: MNQ 09-26");

        public static ContractValidationResult Fail(string message)
            => new ContractValidationResult(false, message);
    }

    /// <summary>
    /// Ensures the host instrument matches MNQ 09-26 before the engine runs.
    /// </summary>
    public static class MnqContractValidator
    {
        public static ContractValidationResult Validate(string? instrumentFullName)
        {
            if (string.IsNullOrWhiteSpace(instrumentFullName))
                return ContractValidationResult.Fail("[UltronV1] Instrument name is empty.");

            // NinjaTrader format: "MNQ 09-26" or "MNQ SEP26" depending on display;
            // accept root MNQ + contract month/year token.
            string normalized = instrumentFullName!.Trim().ToUpperInvariant();

            if (!normalized.StartsWith(MnqContractSpec.SymbolRoot))
                return ContractValidationResult.Fail(
                    $"[UltronV1] Invalid instrument '{instrumentFullName}'. V1 requires {MnqContractSpec.Symbol}.");

            // Strict match for the configured V1 contract when exact string is available.
            if (normalized == MnqContractSpec.Symbol.ToUpperInvariant())
                return ContractValidationResult.Ok();

            // Allow "MNQ 09-26" variants and SEP26-style if root is MNQ and contains 09-26 or SEP26.
            bool hasContractToken = normalized.Contains("09-26")
                || normalized.Contains("SEP26")
                || normalized.Contains("09/26");

            if (!hasContractToken)
                return ContractValidationResult.Fail(
                    $"[UltronV1] Instrument '{instrumentFullName}' is MNQ but not {MnqContractSpec.Symbol}. V1 is contract-specific.");

            return ContractValidationResult.Ok();
        }
    }
}
