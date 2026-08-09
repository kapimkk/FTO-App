using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FTO_App.Services
{
    /// <summary>
    /// Normaliza a URL do QR Code da NFC-e (API Fiscal e/ou XML autorizado).
    /// A SEFAZ rejeita QR "mal formado" com espaços, quebras, entidades HTML,
    /// <c>|</c> percent-encoded incorreto ou URL diferente da tag <c>qrCode</c> do XML.
    /// </summary>
    public static class NfceQrCodeNormalizer
    {
        /// <summary>
        /// Prefere a URL do XML autorizado (é a que a SEFAZ validou); senão usa a da API.
        /// </summary>
        public static string? Resolver(string? qrDaApi, string? xmlAutorizado)
        {
            string? doXml = ExtrairDoXml(xmlAutorizado);
            string? preferida = !string.IsNullOrWhiteSpace(doXml) ? doXml : qrDaApi;
            return Normalizar(preferida);
        }

        /// <summary>Extrai o conteúdo da tag qrCode (infNFeSupl) do XML autorizado.</summary>
        public static string? ExtrairDoXml(string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            try
            {
                var doc = XDocument.Parse(xml.Trim(), LoadOptions.None);
                XNamespace ns = "http://www.portalfiscal.inf.br/nfe";
                var el = doc.Descendants(ns + "qrCode").FirstOrDefault()
                         ?? doc.Descendants().FirstOrDefault(e =>
                             string.Equals(e.Name.LocalName, "qrCode", StringComparison.OrdinalIgnoreCase));
                string? raw = el?.Value?.Trim();
                return string.IsNullOrWhiteSpace(raw) ? null : raw;
            }
            catch
            {
                // Fallback regex se o XML vier fragmentado / com namespace inconsistente
                var m = Regex.Match(xml, @"<qrCode[^>]*>\s*<!\[CDATA\[(.*?)\]\]>\s*</qrCode>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success)
                    m = Regex.Match(xml, @"<qrCode[^>]*>(.*?)</qrCode>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }
        }

        /// <summary>Retorna a URL limpa pronta para gravar/exibir/gerar o QR; null se vazia após limpeza.</summary>
        public static string? Normalizar(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            string s = url.Trim();
            s = WebUtility.HtmlDecode(s);
            s = s.Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);
            // Remove quebras e espaços invisíveis (comum em JSON/XML mal serializados)
            s = Regex.Replace(s, @"[\u0000-\u001F\u007F\u00A0]", "");
            s = s.Replace("\r", "").Replace("\n", "").Replace("\t", "");

            // Espaços literais quebram o QR na consulta SEFAZ
            if (s.Contains(' '))
                s = s.Replace(" ", "");

            s = s.Trim('"', '\'', '`');

            if (s.Length < 20) return null;

            if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (s.StartsWith("//"))
                    s = "http:" + s;
                else if (s.Contains("fazenda", StringComparison.OrdinalIgnoreCase) ||
                         s.Contains("nfce", StringComparison.OrdinalIgnoreCase) ||
                         s.Contains("sefaz", StringComparison.OrdinalIgnoreCase))
                    s = "http://" + s.TrimStart('/');
            }

            // Parâmetro p= deve usar pipes literais (NT NFC-e), não %7C
            int idxP = s.IndexOf("?p=", StringComparison.OrdinalIgnoreCase);
            if (idxP < 0) idxP = s.IndexOf("&p=", StringComparison.OrdinalIgnoreCase);
            if (idxP >= 0)
            {
                int start = idxP + 3;
                string prefix = s[..start];
                string pVal = s[start..];
                // Corta outros query params acidentais após p (mantém só o bloco p)
                int amp = pVal.IndexOf('&');
                if (amp >= 0) pVal = pVal[..amp];

                if (pVal.Contains('%'))
                {
                    try { pVal = Uri.UnescapeDataString(pVal); }
                    catch { /* mantém pVal */ }
                }

                pVal = pVal.Replace(" ", "");
                s = prefix + pVal;
            }

            return s;
        }
    }
}
