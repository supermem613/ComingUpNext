namespace ComingUpNextTray
{
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>
    /// Utility methods for truncating UI text to fit available pixel widths.
    /// </summary>
    internal static class UiTruncation
    {
        /// <summary>
        /// Truncates the provided text to fit within <paramref name="maxPixels"/>, appending an ellipsis if truncated.
        /// Uses TextRenderer.MeasureText for pixel measurement.
        /// </summary>
        /// <param name="text">Input text (may be null).</param>
        /// <param name="font">Font used for measuring.</param>
        /// <param name="maxPixels">Maximum allowed pixel width.</param>
        /// <returns>Original text if it fits, otherwise a truncated string with trailing ellipsis.</returns>
        public static string TruncateToFit(string? text, Font font, int maxPixels)
        {
            if (string.IsNullOrEmpty(text) || font is null || maxPixels <= 0)
            {
                return text ?? string.Empty;
            }

            // If it already fits, return as-is
            Size full = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
            if (full.Width <= maxPixels)
            {
                return text;
            }

            const string ell = "...";

            // Binary search for best truncation length
            int lo = 0, hi = text.Length;
            string candidate = ell;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                string sub = string.Concat(text.AsSpan(0, mid), ell);
                Size s = TextRenderer.MeasureText(sub, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
                if (s.Width <= maxPixels)
                {
                    candidate = sub;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return candidate;
        }

        /// <summary>
        /// Truncates <paramref name="input"/> to fit within <paramref name="maxPixels"/>, but preserves
        /// a trailing parenthetical segment if present (the last " (...)"). The base part is truncated
        /// first so the parenthetical remains intact when possible.
        /// </summary>
        /// <param name="input">Input text to truncate.</param>
        /// <param name="font">Font used for measurement.</param>
        /// <param name="maxPixels">Maximum allowed pixel width.</param>
        /// <returns>Truncated string which preserves trailing parenthetical when possible.</returns>
        public static string TruncatePreservingParen(string input, Font font, int maxPixels)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            int parenIndex = input.LastIndexOf(" (", System.StringComparison.Ordinal);
            if (parenIndex <= 0)
            {
                return TruncateToFit(input, font, maxPixels);
            }

            string basePart = input.Substring(0, parenIndex);
            string parenPart = input.Substring(parenIndex);

            int parenWidth = TextRenderer.MeasureText(parenPart, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine).Width;
            int avail = Math.Max(0, maxPixels - parenWidth);
            string truncatedBase = TruncateToFit(basePart, font, avail);
            return string.Concat(truncatedBase, parenPart);
        }
    }
}
