using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FTO_App.Services
{
    /// <summary>
    /// Situação de pagamento de um lançamento de venda (coluna <c>vendas.pago</c>).
    /// Centraliza a comparação para não depender de acento, caixa ou espaço.
    /// </summary>
    public static class StatusVenda
    {
        public const string Pago = "Pago";
        public const string EmAberto = "Em Aberto";
        public const string EmExecucao = "Em execução";
        public const string NaoAprovado = "Não aprovado";

        private const string ChavePago = "pago";
        private const string ChaveEmAberto = "emaberto";
        private const string ChaveNaoAprovado = "naoaprovado";

        /// <summary>Reduz o status a letras/dígitos minúsculos sem acento ("Não aprovado" → "naoaprovado").</summary>
        public static string Normalizar(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "";
            string semAcento = new string(valor
                .Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
            return new string(semAcento.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        public static bool EhPago(string? status) => Normalizar(status) == ChavePago;

        public static bool EhEmAberto(string? status) => Normalizar(status) == ChaveEmAberto;

        public static bool EhNaoAprovado(string? status) => Normalizar(status) == ChaveNaoAprovado;

        /// <summary>
        /// Orçamento recusado não é receita: "Não aprovado" fica fora de faturamento, lucro,
        /// ticket médio, gráficos e ranking de clientes. Continua aparecendo nas listagens.
        /// </summary>
        public static bool ContaNoFaturamento(string? status) => !EhNaoAprovado(status);

        /// <summary>Rótulo para exibição — status vazio vira "(sem status)".</summary>
        public static string Rotulo(string? status) =>
            string.IsNullOrWhiteSpace(status) ? "(sem status)" : status.Trim();
    }
}
