using System.Text.RegularExpressions;

public static class KeyVaultReferenceParser
{
    private const string Prefix = "@Microsoft.KeyVault(";
    private const string VaultNameKey = "VaultName=";
    private const string SecretNameKey = "SecretName=";

    public static bool TryParseUriFormat(
        string value,
        out Uri? vaultUri,
        out string secretName)
    {
        var secretUri = ExtractSecretUri(value);
        var uri = new Uri(secretUri);

        string[] segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2 ||
            !segments[0].Equals("secrets",
                StringComparison.OrdinalIgnoreCase))
        {
            vaultUri = null;
            secretName = string.Empty;
            return false;
        }

        secretName = Uri.UnescapeDataString(segments[1]);
        vaultUri = new Uri(uri.GetLeftPart(UriPartial.Authority));
        return true;
    }

    public static bool TryParseKeyValueFormat(
        string? value,
        out string vaultName,
        out string secretName)
    {
        vaultName = string.Empty;
        secretName = string.Empty;

        if (string.IsNullOrEmpty(value))
            return false;

        ReadOnlySpan<char> input = value.AsSpan();

        input = input[Prefix.Length..];

        // Validate closing parenthesis.
        if (input.Length == 0 || input[^1] != ')')
            return false;

        input = input[..^1];

        // Extract VaultName.
        if (!input.StartsWith(VaultNameKey.AsSpan(), StringComparison.Ordinal))
            return false;

        input = input[VaultNameKey.Length..];

        int separatorIndex = input.IndexOf(';');

        if (separatorIndex <= 0)
            return false;

        ReadOnlySpan<char> vaultSpan = input[..separatorIndex];
        input = input[(separatorIndex + 1)..];

        // Extract SecretName.
        if (!input.StartsWith(SecretNameKey.AsSpan(), StringComparison.Ordinal))
            return false;

        ReadOnlySpan<char> secretSpan = input[SecretNameKey.Length..];

        if (vaultSpan.IsEmpty || secretSpan.IsEmpty)
            return false;

        // Reject unexpected additional separators or parentheses.
        if (secretSpan.IndexOfAny(';', ')', '(') >= 0)
            return false;

        if (vaultSpan.IndexOfAny(';', ')', '(') >= 0)
            return false;

        vaultName = vaultSpan.ToString();
        secretName = secretSpan.ToString();

        return true;
    }

    private static string ExtractSecretUri(string reference)
    {
        const string pattern =
            @"^\s*@Microsoft\.KeyVault\s*\(\s*SecretUri\s*=\s*(https://[^)\s]+)\s*\)\s*$";

        Match match = Regex.Match(
            reference,
            pattern,
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new FormatException(
                "Expected @Microsoft.KeyVault(SecretUri=https://...).");
        }

        return match.Groups[1].Value;
    }
}
