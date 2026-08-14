using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FTO_App.Services
{
    /// <summary>
    /// Nome sugerido no download de documentos fiscais:
    /// <c>Prefixo-NomeDaPessoa-ddMMyyyy.ext</c> (ex.: NotaFiscalServico-JoaoDaSilva-14082026.pdf).
    /// </summary>
    public static class DocumentoArquivoNome
    {
        public const string PrefixoNfse = "NotaFiscalServico";
        public const string PrefixoNfe = "NotaFiscal";

        private const int MaxCaracteresNome = 40;

        /// <summary>Monta o nome do arquivo já sanitizado para o sistema de arquivos.</summary>
        /// <param name="prefixo">Tipo do documento (ex.: NotaFiscalServico).</param>
        /// <param name="nome">Tomador (NFS-e) ou destinatário (NF-e).</param>
        /// <param name="data">Data do documento (competência na NFS-e, emissão na NF-e).</param>
        /// <param name="extensao">Extensão com ponto (ex.: ".pdf").</param>
        /// <param name="sufixo">Complemento opcional antes da extensão (ex.: "logo").</param>
        public static string Montar(string prefixo, string? nome, DateTime data, string extensao, string? sufixo = null)
        {
            string pessoa = Compactar(nome);
            if (string.IsNullOrEmpty(pessoa)) pessoa = "SemNome";

            var partes = new StringBuilder(Sanitizar(prefixo));
            partes.Append('-').Append(pessoa).Append('-').Append(data.ToString("ddMMyyyy", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(sufixo))
                partes.Append('-').Append(Sanitizar(sufixo));

            return partes.Append(extensao).ToString();
        }

        /// <summary>Nome sem espaços/acentos, em PascalCase: "José da Silva &amp; Cia" → "JoseDaSilvaCia".</summary>
        private static string Compactar(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "";

            string limpo = RemoverAcentos(nome);
            var palavras = limpo.Split(new[] { ' ', '\t', '-', '_', '.', '/' }, StringSplitOptions.RemoveEmptyEntries);

            var sb = new StringBuilder();
            foreach (string palavra in palavras)
            {
                string apenasLetras = new string(palavra.Where(char.IsLetterOrDigit).ToArray());
                if (apenasLetras.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(apenasLetras[0]));
                if (apenasLetras.Length > 1) sb.Append(apenasLetras[1..]);
                if (sb.Length >= MaxCaracteresNome) break;
            }

            string resultado = sb.ToString();
            return resultado.Length > MaxCaracteresNome ? resultado[..MaxCaracteresNome] : resultado;
        }

        private static string RemoverAcentos(string texto) =>
            new string(texto
                .Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());

        private static string Sanitizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            var invalidos = Path.GetInvalidFileNameChars();
            return new string(RemoverAcentos(texto).Where(c => !invalidos.Contains(c) && c != ' ').ToArray());
        }
    }
}
