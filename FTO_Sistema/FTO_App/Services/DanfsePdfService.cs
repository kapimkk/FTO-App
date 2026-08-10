using FTO_App.Services.Danfse;

namespace FTO_App.Services
{
    /// <summary>
    /// Geração local do DANFSe (PDF) conforme NT 008/2026.
    /// A API oficial ADN/SEFIN de PDF foi sobrestada em 03/08/2026 — o ERP gera a partir do XML autorizado.
    /// </summary>
    public static class DanfsePdfService
    {
        /// <summary>Gera o PDF do DANFSe somente com dados presentes no XML autorizado.</summary>
        public static byte[] GerarDeXml(string xmlAutorizado)
        {
            var model = NfseXmlDanfseParser.Parse(xmlAutorizado);
            return DanfseNt008Renderer.Render(model);
        }

        /// <summary>Parse sem renderizar — útil para testes e validação prévia.</summary>
        public static DanfseDocumentModel ParseXml(string xmlAutorizado) =>
            NfseXmlDanfseParser.Parse(xmlAutorizado);
    }
}
