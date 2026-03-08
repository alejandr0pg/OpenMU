// <copyright file="AppleJwtValidator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Validates Apple Sign In identity tokens (JWTs) against Apple's public keys.
/// </summary>
internal static class AppleJwtValidator
{
    private const string AppleJwksUrl = "https://appleid.apple.com/auth/keys";
    private const string AppleIssuer = "https://appleid.apple.com";
    private static readonly HttpClient HttpClient = new();

    private static JsonElement? _cachedKeys;
    private static DateTime _cacheExpiry = DateTime.MinValue;

    /// <summary>
    /// Validates an Apple identity token and returns the email claim.
    /// </summary>
    /// <param name="identityToken">The JWT identity token from Apple Sign In.</param>
    /// <param name="expectedAudience">The expected audience (your app's bundle ID / client ID).</param>
    /// <returns>The email address if valid; otherwise, null.</returns>
    public static async Task<string?> ValidateAndGetEmailAsync(string identityToken, string expectedAudience)
    {
        var parts = identityToken.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var header = ParseBase64Url(parts[0]);
        var payload = ParseBase64Url(parts[1]);
        if (header is null || payload is null)
        {
            return null;
        }

        if (!ValidateClaims(payload, expectedAudience))
        {
            return null;
        }

        if (!header.TryGetProperty("kid", out var kidProp) ||
            !header.TryGetProperty("alg", out var algProp))
        {
            return null;
        }

        var kid = kidProp.GetString();
        var alg = algProp.GetString();
        if (kid is null || alg != "RS256")
        {
            return null;
        }

        var appleKey = await GetApplePublicKeyAsync(kid).ConfigureAwait(false);
        if (appleKey is null)
        {
            return null;
        }

        if (!VerifySignature(parts[0], parts[1], parts[2], appleKey.Value))
        {
            return null;
        }

        return payload.TryGetProperty("email", out var emailProp)
            ? emailProp.GetString()
            : null;
    }

    private static bool ValidateClaims(JsonElement payload, string expectedAudience)
    {
        if (!payload.TryGetProperty("iss", out var iss) || iss.GetString() != AppleIssuer)
        {
            return false;
        }

        if (!payload.TryGetProperty("aud", out var aud) || aud.GetString() != expectedAudience)
        {
            return false;
        }

        if (payload.TryGetProperty("exp", out var exp))
        {
            var expTime = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            if (expTime < DateTimeOffset.UtcNow)
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifySignature(string headerB64, string payloadB64, string signatureB64, JsonElement key)
    {
        if (!key.TryGetProperty("n", out var nProp) || !key.TryGetProperty("e", out var eProp))
        {
            return false;
        }

        var n = DecodeBase64Url(nProp.GetString()!);
        var e = DecodeBase64Url(eProp.GetString()!);
        if (n is null || e is null)
        {
            return false;
        }

        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = n, Exponent = e });

        var data = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        var signature = DecodeBase64Url(signatureB64);
        if (signature is null)
        {
            return false;
        }

        return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static async Task<JsonElement?> GetApplePublicKeyAsync(string kid)
    {
        if (_cachedKeys is null || DateTime.UtcNow > _cacheExpiry)
        {
            var response = await HttpClient.GetAsync(AppleJwksUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            _cachedKeys = doc.RootElement.GetProperty("keys").Clone();
            _cacheExpiry = DateTime.UtcNow.AddHours(24);
        }

        foreach (var key in _cachedKeys.Value.EnumerateArray())
        {
            if (key.TryGetProperty("kid", out var keyId) && keyId.GetString() == kid)
            {
                return key;
            }
        }

        return null;
    }

    private static JsonElement? ParseBase64Url(string base64Url)
    {
        var bytes = DecodeBase64Url(base64Url);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(bytes);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? DecodeBase64Url(string input)
    {
        try
        {
            var padded = input.Replace('-', '+').Replace('_', '/');
            padded += (padded.Length % 4) switch
            {
                2 => "==",
                3 => "=",
                _ => string.Empty,
            };
            return Convert.FromBase64String(padded);
        }
        catch
        {
            return null;
        }
    }
}
