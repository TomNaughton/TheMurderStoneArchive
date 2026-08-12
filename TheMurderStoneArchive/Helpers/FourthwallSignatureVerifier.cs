using System.Security.Cryptography;
using System.Text;

namespace TheMurderStoneArchive.Helpers
{
    /// <summary>
    /// Shared HMAC-SHA256 signature verification for Fourthwall webhooks.
    /// Used by both the donation webhook service and the API subscription webhook controller.
    /// </summary>
    public static class FourthwallSignatureVerifier
    {
        /// <summary>
        /// Verifies a Fourthwall webhook signature header against the raw request payload.
        /// Supports hex, base64, and timestamp-prefixed variants as sent by Fourthwall.
        /// </summary>
        public static bool VerifySignature(string payload, string signatureHeader, string webhookSecret)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader))
                return false;

            var signatures = new List<string>();
            string? timestamp = null;

            foreach (var token in signatureHeader
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .SelectMany(part => part.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            {
                var part = token.Trim();
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                var index = part.IndexOf('=');
                if (index < 0)
                {
                    signatures.Add(part.Trim('"'));
                    continue;
                }

                var key = part[..index].Trim();
                var value = part[(index + 1)..].Trim().Trim('"');

                if (string.Equals(key, "t", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "timestamp", StringComparison.OrdinalIgnoreCase))
                {
                    timestamp = value;
                    continue;
                }

                if (string.Equals(key, "v1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "v2", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "signature", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "sha256", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "hmac", StringComparison.OrdinalIgnoreCase))
                {
                    signatures.Add(value);
                    continue;
                }

                // Unknown key — treat the whole token as the signature (handles raw base64 with '=' padding)
                signatures.Add(part.Trim('"'));
            }

            if (signatures.Count == 0)
                return false;

            var secretCandidates = GetSecretCandidates(webhookSecret);
            var expectedCandidates = new List<string>();

            foreach (var secretKey in secretCandidates)
            {
                expectedCandidates.Add(ComputeHmacHex(payload, secretKey));
                expectedCandidates.Add(ComputeHmacBase64(payload, secretKey));

                if (!string.IsNullOrWhiteSpace(timestamp))
                {
                    var timestampPayload = $"{timestamp}.{payload}";
                    expectedCandidates.Add(ComputeHmacHex(timestampPayload, secretKey));
                    expectedCandidates.Add(ComputeHmacBase64(timestampPayload, secretKey));
                }
            }

            foreach (var provided in signatures)
            {
                foreach (var expected in expectedCandidates)
                {
                    if (FixedTimeEqualsFlexible(provided, expected))
                        return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<byte[]> GetSecretCandidates(string webhookSecret)
        {
            var results = new List<byte[]> { Encoding.UTF8.GetBytes(webhookSecret) };

            if (TryDecodeBase64OrBase64Url(webhookSecret, out var base64Bytes))
                results.Add(base64Bytes);

            if (TryDecodeHex(webhookSecret, out var hexBytes))
                results.Add(hexBytes);

            return results;
        }

        private static string ComputeHmacHex(string payload, byte[] secret)
        {
            using var hmac = new HMACSHA256(secret);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }

        private static string ComputeHmacBase64(string payload, byte[] secret)
        {
            using var hmac = new HMACSHA256(secret);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        private static bool FixedTimeEqualsFlexible(string provided, string expected)
        {
            var normProvided = provided.Trim().Trim('"');
            var normExpected = expected.Trim().Trim('"');

            if (TryDecodeSignature(normProvided, out var providedBytes) &&
                TryDecodeSignature(normExpected, out var expectedBytes))
            {
                return providedBytes.Length == expectedBytes.Length &&
                       CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
            }

            var a = Encoding.UTF8.GetBytes(normProvided);
            var b = Encoding.UTF8.GetBytes(normExpected);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }

        private static bool TryDecodeSignature(string value, out byte[] bytes)
        {
            var normalized = value.Trim();
            if (normalized.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[7..];

            if (TryDecodeHex(normalized, out bytes)) return true;
            return TryDecodeBase64OrBase64Url(normalized, out bytes);
        }

        private static bool TryDecodeHex(string value, out byte[] bytes)
        {
            bytes = [];
            if (string.IsNullOrWhiteSpace(value) || value.Length % 2 != 0) return false;
            foreach (var ch in value)
                if (!Uri.IsHexDigit(ch)) return false;
            try { bytes = Convert.FromHexString(value); return true; }
            catch { return false; }
        }

        private static bool TryDecodeBase64OrBase64Url(string value, out byte[] bytes)
        {
            bytes = [];
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Replace('-', '+').Replace('_', '/');
            var padding = normalized.Length % 4;
            if (padding > 0) normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
            try { bytes = Convert.FromBase64String(normalized); return true; }
            catch { return false; }
        }
    }
}
