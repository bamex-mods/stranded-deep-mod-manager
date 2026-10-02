using System;

namespace StrandedDeepModManager.UI
{
    internal static class DisplayNames
    {
        private const string StrandedDeepPrefix =
            "Stranded Deep ";

        public static string Mod(
            string canonicalName)
        {
            if (String.IsNullOrWhiteSpace(
                canonicalName))
            {
                return canonicalName ?? "";
            }

            if (canonicalName.StartsWith(
                StrandedDeepPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                string shortName =
                    canonicalName.Substring(
                        StrandedDeepPrefix.Length).Trim();

                if (!String.IsNullOrWhiteSpace(
                    shortName))
                {
                    return shortName;
                }
            }

            return canonicalName;
        }
    }
}