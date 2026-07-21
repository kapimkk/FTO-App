using System;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;

namespace FTO_App.Services
{
    /// <summary>
    /// Formatação monetária ao digitar (centavos): 1990 → 19,90.
    /// Evita o bug de virgula virar milhar (19,90 → 199,00).
    /// </summary>
    public static class MoneyInputHelper
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public static void ApplyLiveFormat(TextBox tb, ref bool suppressFlag, Action? afterFormat = null)
        {
            if (suppressFlag || tb is null) return;

            suppressFlag = true;
            try
            {
                int caretFromEnd = tb.Text.Length - tb.CaretIndex;
                string digits = new string(tb.Text.Where(char.IsDigit).ToArray());
                if (digits.Length > 12)
                    digits = digits[^12..];

                decimal value = digits.Length == 0
                    ? 0m
                    : decimal.Parse(digits, CultureInfo.InvariantCulture) / 100m;

                string formatted = value.ToString("N2", PtBr);
                if (tb.Text != formatted)
                {
                    tb.Text = formatted;
                    int newPos = Math.Max(0, formatted.Length - Math.Max(0, caretFromEnd));
                    tb.CaretIndex = Math.Min(newPos, formatted.Length);
                }
            }
            finally
            {
                suppressFlag = false;
            }

            afterFormat?.Invoke();
        }

        public static decimal Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string clean = text.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (clean.Contains('.') && !clean.Contains(','))
            {
                if (decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal inv))
                    return inv;
            }
            if (decimal.TryParse(clean, NumberStyles.Number, PtBr, out decimal br))
                return br;
            // Fallback: só dígitos como centavos
            string digits = new string(clean.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out decimal cents))
                return cents / 100m;
            return 0;
        }

        public static string Format(decimal value) => value.ToString("N2", PtBr);
    }
}
