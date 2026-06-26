using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UltronV1.Core.Engine;

namespace UltronV1.Core.Scoring
{
    /// <summary>
    /// Deterministic fingerprint of the operational scoring configuration (weights + thresholds).
    /// Computed from the effective UltronConfig at runtime — no manual version bumps required.
    /// </summary>
    public static class ScoreProfileHash
    {
        public static string Compute(UltronConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            string canonical = ScoreProfileV1.BuildCanonical(config);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(canonical);
                byte[] hash = sha.ComputeHash(bytes);
                return ToHexLower(hash);
            }
        }

        static string ToHexLower(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
