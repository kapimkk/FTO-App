using System;
using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace FTO_App.Services
{
    /// <summary>
    /// Geração de imagens de QR Code (usado na DANFE NFC-e impressa na térmica).
    /// </summary>
    public static class QrCodeImageService
    {
        /// <summary>Gera um BitmapImage a partir de um conteúdo (ex.: URL de consulta da NFC-e). Retorna null se vazio ou inválido.</summary>
        public static BitmapImage? GerarImagem(string? conteudo, int pixelsPorModulo = 4)
        {
            if (string.IsNullOrWhiteSpace(conteudo)) return null;

            try
            {
                using var generator = new QRCodeGenerator();
                using var dados = generator.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.M);
                var renderer = new PngByteQRCode(dados);
                byte[] png = renderer.GetGraphic(pixelsPorModulo);

                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(png);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
