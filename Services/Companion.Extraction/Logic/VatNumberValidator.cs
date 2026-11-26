namespace Companion.Extraction.Logic;

public static class VatNumberValidator
{
    public static bool IsValid(string? value) => value is { Length: 10 } && value[0] == '4'
        && value.All(c => c is >= '0' and <= '9');
}