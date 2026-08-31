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
        private const double AlturaMinimaTicketPx = 600;

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

            if (string.IsNullOrWhiteSpace(nomeImpressora))
            {
                mensagemErro = "Nenhuma impressora configurada. Selecione na tela de módulos.";
                return false;
            }

            try
            {
                var server = new LocalPrintServer();
                PrintQueue queue;
                try
                {
                    queue = server.GetPrintQueue(nomeImpressora);
                    // Impressora em rede: o status ficava em cache até o momento em que o
                    // Windows a listou; sem atualizar aqui o app pode tentar imprimir numa
                    // fila que já caiu (offline/sem papel) e a impressão sai cortada/vazia
                    // em vez de dar um erro claro.
                    queue.Refresh();
                }
                catch (PrintQueueException ex)
                {
                    mensagemErro = $"Impressora '{nomeImpressora}' não encontrada ou indisponível: {ex.Message}";
                    return false;
                }

                if (!FilaProntaParaImprimir(queue, nomeImpressora, out mensagemErro))
                    return false;

                var dialog = new PrintDialog { PrintQueue = queue };

                double larguraPx = ObterLarguraImpressao(dialog);
                PrepararVisualParaImpressao(visual, larguraPx);

                // Impressora térmica instalada por USB guarda (no driver) o papel em rolo de
                // 80mm como padrão da fila; a MESMA impressora instalada em rede costuma criar
                // uma fila nova cujo padrão é A4/Carta — o cupom (302px de largura) então sai
                // espremido/cortado num canto da página "grande", ou o driver ESC/POS rejeita o
                // tamanho e o texto sai corrompido. Em vez de confiar no ticket padrão de cada
                // fila, fixamos aqui o tamanho de página com a mesma largura do nosso layout,
                // igual para USB e para rede.
                dialog.PrintTicket = ConstruirTicketRoloTermico(queue, larguraPx, visual);

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

        /// <summary>
        /// Verifica se a fila está em condição de receber o trabalho antes de mandar imprimir —
        /// impressoras em rede caem/ficam sem papel com mais frequência que uma USB ao lado da
        /// máquina, e sem essa checagem o app manda o job e o cupom simplesmente não sai (ou sai
        /// pela metade), sem nenhuma mensagem.
        /// </summary>
        private static bool FilaProntaParaImprimir(PrintQueue queue, string nomeImpressora, out string? mensagemErro)
        {
            mensagemErro = null;
            var status = queue.QueueStatus;

            string? problema = status switch
            {
                _ when status.HasFlag(PrintQueueStatus.Offline) =>
                    "está offline (verifique se está ligada e conectada na rede)",
                _ when status.HasFlag(PrintQueueStatus.NotAvailable) || status.HasFlag(PrintQueueStatus.ServerUnknown) =>
                    "não está respondendo na rede (verifique o IP/porta configurados nela)",
                _ when status.HasFlag(PrintQueueStatus.PaperOut) =>
                    "está sem papel",
                _ when status.HasFlag(PrintQueueStatus.PaperJam) || status.HasFlag(PrintQueueStatus.PaperProblem) =>
                    "está com problema no papel (atolamento?)",
                _ when status.HasFlag(PrintQueueStatus.DoorOpen) =>
                    "está com a tampa aberta",
                _ when status.HasFlag(PrintQueueStatus.Error) =>
                    "reportou um erro",
                _ => null
            };

            if (problema == null) return true;

            mensagemErro = $"Impressora '{nomeImpressora}' {problema}. Reconecte/verifique-a e tente novamente.";
            return false;
        }

        /// <summary>
        /// Monta um PrintTicket com o tamanho de página igual à largura do nosso próprio layout
        /// (rolo térmico), em vez de herdar o papel padrão da fila. A altura é generosa — o
        /// papel é contínuo e o driver corta ao final do conteúdo, não usa a altura à risca.
        /// Se a impressora rejeitar o tamanho customizado (driver sem suporte), volta ao ticket
        /// padrão da fila em vez de travar a impressão.
        /// </summary>
        private static PrintTicket ConstruirTicketRoloTermico(PrintQueue queue, double larguraPx, FrameworkElement visual)
        {
            double alturaConteudo = visual.ActualHeight > 0 ? visual.ActualHeight : visual.DesiredSize.Height;
            double alturaTicket = Math.Max(alturaConteudo + 60, AlturaMinimaTicketPx);

            var desejado = new PrintTicket
            {
                PageMediaSize = new PageMediaSize(larguraPx, alturaTicket),
                PageOrientation = PageOrientation.Portrait
            };

            try
            {
                var validado = queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket, desejado);
                return validado.ValidatedPrintTicket;
            }
            catch
            {
                // Driver não aceitou o tamanho customizado — segue com o padrão da fila
                // (comportamento antigo) em vez de impedir a impressão.
                return queue.DefaultPrintTicket;
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
            if (visual.Parent is ReceiptCupomView cupomView)
            {
                cupomView.PrepararParaImpressao(larguraPx);
                return;
            }

            if (visual is ReceiptCupomView r)
            {
                r.PrepararParaImpressao(larguraPx);
                return;
            }

            visual.Width = larguraPx;
            visual.Measure(new Size(larguraPx, double.PositiveInfinity));
            visual.Arrange(new Rect(0, 0, larguraPx, visual.DesiredSize.Height));
            visual.UpdateLayout();
        }
    }
}
