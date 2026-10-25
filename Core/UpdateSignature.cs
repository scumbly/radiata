namespace ControllerWheel;

/// <summary>
/// The accept/reject decision for a downloaded installer's Authenticode signature, separated from the
/// WinVerifyTrust call that produces its inputs (<c>Setup/UpdateSignatureVerifier</c>) so it runs headlessly.
/// Release certificates come from Azure Artifact Signing and are re-issued every few days, so nothing here
/// pins a thumbprint: the signer is identified by subject (CN + O, parsed as RDNs, never substring-matched)
/// and the chain must end at Microsoft's identity-verification root, by subject name.
/// </summary>
public static class UpdateSignature
{
    public const string RequiredSignerName = "Jesse Tarter-Holden";

    /// <summary>Root of the Public Trust Artifact Signing chain (leaf → "Microsoft ID Verified CS … CA" →
    /// "Microsoft ID Verified Code Signing PCA 2021" → this).</summary>
    public const string RequiredRootName = "Microsoft Identity Verification Root Certificate Authority 2020";

    /// <summary>Accept only when Windows reports the signature trusted AND the signer subject carries exactly
    /// one CN and one O equal to <see cref="RequiredSignerName"/> AND the chain root's CN equals
    /// <see cref="RequiredRootName"/>. <paramref name="reason"/> is a trace string for every outcome.</summary>
    public static bool Accept(bool windowsTrustOk, string? signerSubject, string? chainRootSubject, out string reason)
    {
        if (!windowsTrustOk) { reason = "WinVerifyTrust did not report the signature as trusted"; return false; }

        var signer = ParseRdns(signerSubject);
        if (signer is null) { reason = "signer subject missing or unparsable"; return false; }
        if (!Single(signer, "CN", RequiredSignerName))
        { reason = $"signer CN is not exactly \"{RequiredSignerName}\" (subject: {signerSubject})"; return false; }
        if (!Single(signer, "O", RequiredSignerName))
        { reason = $"signer O is not exactly \"{RequiredSignerName}\" (subject: {signerSubject})"; return false; }

        var root = ParseRdns(chainRootSubject);
        if (root is null || !Single(root, "CN", RequiredRootName))
        { reason = $"chain does not end at \"{RequiredRootName}\" (root: {chainRootSubject ?? "none"})"; return false; }

        reason = "trusted signature from the required signer and root";
        return true;
    }

    private static bool Single(List<(string type, string value)> rdns, string type, string value)
    {
        int n = 0; bool match = false;
        foreach (var (t, v) in rdns)
        {
            if (!string.Equals(t, type, StringComparison.OrdinalIgnoreCase)) continue;
            n++;
            match = string.Equals(v, value, StringComparison.Ordinal);
        }
        return n == 1 && match;
    }

    /// <summary>Split an X.500 name ("CN=a, O=\"b, c\", C=US") into type/value pairs. Honours double quotes and
    /// backslash escapes; separators are ',' and ';'. Null on malformed input.</summary>
    public static List<(string type, string value)>? ParseRdns(string? dn)
    {
        if (string.IsNullOrWhiteSpace(dn) || dn.Length > 1024) return null;
        var result = new List<(string, string)>();
        var cur = new System.Text.StringBuilder();
        bool quoted = false;

        bool Flush()
        {
            var part = cur.ToString();
            cur.Clear();
            int eq = IndexOfUnescapedEquals(part);
            if (eq <= 0) return false;
            result.Add((part[..eq].Trim(), Unquote(part[(eq + 1)..].Trim())));
            return true;
        }

        for (int i = 0; i < dn.Length; i++)
        {
            char c = dn[i];
            if (c == '\\' && i + 1 < dn.Length) { cur.Append(c).Append(dn[++i]); continue; }
            if (c == '"') quoted = !quoted;
            if ((c == ',' || c == ';') && !quoted) { if (!Flush()) return null; continue; }
            cur.Append(c);
        }
        if (quoted || !Flush()) return null;
        return result;
    }

    private static int IndexOfUnescapedEquals(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; }
            if (s[i] == '=') return i;
        }
        return -1;
    }

    private static string Unquote(string v)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < v.Length; i++)
        {
            char c = v[i];
            if (c == '\\' && i + 1 < v.Length) { sb.Append(v[++i]); continue; }
            if (c == '"') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
