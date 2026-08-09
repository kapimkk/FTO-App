using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using FTO_App.Views;

namespace FTO_App.Services
{
    /// <summary>
    /// Impressão WPF (PrintVisual) — mesmo padrão Imperial Colors.
    /// </summary>
    public static class CupomPrintHelper
    {
        private const double LarguraMaximaTermicaPx = 360;
        private const double LarguraPadraoTermicaPx = ReceiptCupomView.LarguraCupomPx;

        public static bool ImprimirNaImpressoraConfigurada(
            FrameworkElement visual,
            string nomeDocumento,
            string? nomeImpressora,
            out string? mensagemErro)
        {
            mensagemErro = null;

            if (visual is null)
            {
                mensagemErro = "Nenhum conteúdo para imprimir.";
                return false;
            }

            try
            {
                var dialog = new PrintDialog();

                if (string.IsNullOrWhiteSpace(nomeImpressora))
                {
                    mensagemErro = "Nenhuma impressora configurada. Selecione na tela de módulos.";
                    return false;
                }

                var server = new LocalPrintServer();
                dialog.PrintQueue = server.GetPrintQueue(nomeImpressora);

                double largura = ObterLarguraImpressao(dialog);
                PrepararVisualParaImpressao(visual, largura);

                dialog.PrintVisual(visual, nomeDocumento);
                return true;
            }
            catch (PrintQueueException ex)
            {
                mensagemErro = $"Impressora '{nomeImpressora}' indisponível: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                mensagemErro = $"Erro ao imprimir: {ex.Message}";
                return false;
            }
        }

        private static double ObterLarguraImpressao(PrintDialog dialog)
        {
            try
            {
                var area = dialog.PrintQueue.GetPrintCapabilities(dialog.PrintTicket).PageImageableArea;
                if (area is null || area.ExtentWidth <= 50)
                    return LarguraPadraoTermicaPx;

                double larguraPagina = area.ExtentWidth;

                // Drivers que reportam página larga (ex.: A4): usa largura padrão 80mm.
                if (larguraPagina > LarguraMaximaTermicaPx)
                    return LarguraPadraoTermicaPx;

                return larguraPagina;
            }
            catch
            {
                return LarguraPadraoTermicaPx;
            }
        }

        private static void PrepararVisualParaImpressao(FrameworkElement visual, double larguraPx)
        {
            // Border do cupom pode estar dentro de ReceiptCupomView ou DanfeNfceCupomView
            if (visual.Parent is ReceiptCupomView cupomView)
            {
                cupomView.PrepararParaImpressao(larguraPx);
                return;
            }

            if (visual.Parent is DanfeNfceCupomView danfeView)
            {
                danfeView.PrepararParaImpressao(larguraPx);
                return;
            }

            // Fallback: o UserControl pode ser o próprio visual (caso raro)
            if (visual is ReceiptCupomView r)
            {
                r.PrepararParaImpressao(larguraPx);
                return;
            }
            if (visual is DanfeNfceCupomView d)
            {
                d.PrepararParaImpressao(larguraPx);
                return;
            }

            visual.Width = larguraPx;
            visual.Measure(new Size(larguraPx, double.PositiveInfinity));
            visual.Arrange(new Rect(0, 0, larguraPx, visual.DesiredSize.Height));
            visual.UpdateLayout();
        }
    }
}
