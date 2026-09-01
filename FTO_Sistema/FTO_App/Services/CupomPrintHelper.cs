using System;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FTO_App.Views;

namespace FTO_App.Services
{
    /// <summary>
    /// Impressão do cupom em impressora térmica (PrintVisual).
    ///
    /// O ponto crítico aqui é o TAMANHO DE PÁGINA que o driver vai usar. Uma bobina de 80 mm
    /// tem ~72 mm imprimíveis (576 dots a 203 dpi); se a fila estiver com página A4, o driver
    /// rasteriza 210 mm de largura e a impressora imprime só os 72 mm da esquerda — o cupom sai
    /// cortado à direita. É por isso que a MESMA impressora sai perfeita por USB (fila com o
    /// rolo configurado) e falhada em rede (fila nova, default A4/Carta).
    /// </summary>
    public static class CupomPrintHelper
    {
        /// <summary>96 DIP = 1 polegada = 25,4 mm.</summary>
        private const double DipPorMm = 96.0 / 25.4;

        /// <summary>Área imprimível típica de uma térmica de 80 mm (72 mm ≈ 272 DIP).</summary>
        private const double LarguraImprimivelPadraoPx = 72 * DipPorMm;

        /// <summary>Faixa aceitável para considerar que a página é de bobina térmica (40 mm a 105 mm).</summary>
        private const double LarguraRoloMinimaPx = 40 * DipPorMm;
        private const double LarguraRoloMaximaPx = 105 * DipPorMm;

        /// <summary>Largura de papel alvo (80 mm) usada para escolher a mídia mais próxima.</summary>
        private const double LarguraPapelAlvoPx = 80 * DipPorMm;

        public static bool ImprimirNaImpressoraConfigurada(
            FrameworkElement visual,
            string nomeDocumento,
            string? nomeImpressora,
            out string? mensagemErro) =>
            ImprimirNaImpressoraConfigurada(visual, nomeDocumento, nomeImpressora, out mensagemErro, out _);

        /// <param name="aviso">
        /// Preenchido quando a impressão foi feita, mas a fila está configurada de um jeito que
        /// provavelmente vai cortar o cupom (ex.: página A4 numa térmica). A UI deve mostrar.
        /// </param>
        public static bool ImprimirNaImpressoraConfigurada(
            FrameworkElement visual,
            string nomeDocumento,
            string? nomeImpressora,
            out string? mensagemErro,
            out string? aviso)
        {
            mensagemErro = null;
            aviso = null;

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
                using var server = new LocalPrintServer();
                PrintQueue queue;
                try
                {
                    queue = server.GetPrintQueue(nomeImpressora);
                    // Fila de rede pode ter caído desde que o Windows a listou
                    queue.Refresh();
                }
                catch (PrintQueueException ex)
                {
                    mensagemErro = $"Impressora '{nomeImpressora}' não encontrada ou indisponível: {ex.Message}";
                    return false;
                }

                if (!FilaProntaParaImprimir(queue, nomeImpressora, out mensagemErro))
                    return false;

                // 1) Ticket com a mídia de bobina que o PRÓPRIO driver declara (mídia inventada
                //    é rejeitada na validação e volta silenciosamente para o default da fila).
                PrintTicket ticket = MontarTicketBobina(queue, out bool usouMidiaDeRolo);

                // 2) Área imprimível REAL do ticket que vai ser usado (não do ticket default).
                PageImageableArea? area = ObterAreaImprimivel(queue, ticket);
                double larguraUtilPx = area?.ExtentWidth > 10 ? area.ExtentWidth : LarguraImprimivelPadraoPx;

                if (!usouMidiaDeRolo && larguraUtilPx > LarguraRoloMaximaPx)
                {
                    // Fila em A4/Carta numa térmica: o driver vai rasterizar uma página larga e a
                    // impressora só imprime os ~72 mm da esquerda. Layout no tamanho da bobina para
                    // pelo menos não estourar, e avisa o usuário do que precisa ser ajustado.
                    larguraUtilPx = LarguraImprimivelPadraoPx;
                    aviso =
                        $"A fila '{nomeImpressora}' está com papel {DescreverMidia(ticket.PageMediaSize)} " +
                        "— não é o rolo térmico.\n\n" +
                        "Se o cupom sair cortado, ajuste em: Painel de Controle → Dispositivos e Impressoras → " +
                        $"clique com o botão direito em '{nomeImpressora}' → Preferências de Impressão → " +
                        "papel/tamanho = rolo 80 mm.";
                }

                // 3) Layout na largura imprimível de verdade.
                double larguraLayoutPx = Math.Clamp(larguraUtilPx, LarguraRoloMinimaPx, LarguraRoloMaximaPx);
                PrepararVisualParaImpressao(visual, larguraLayoutPx);

                double larguraConteudo = visual.ActualWidth > 0 ? visual.ActualWidth : larguraLayoutPx;
                double alturaConteudo = visual.ActualHeight > 0 ? visual.ActualHeight : visual.DesiredSize.Height;
                if (alturaConteudo <= 0) alturaConteudo = 1;

                // 4) Rede de segurança: se ainda assim o conteúdo for mais largo que a área
                //    imprimível, encolhe proporcionalmente em vez de deixar cortar.
                double escala = larguraConteudo > larguraUtilPx && larguraConteudo > 0
                    ? larguraUtilPx / larguraConteudo
                    : 1.0;

                var dialog = new PrintDialog { PrintQueue = queue, PrintTicket = ticket };
                dialog.PrintVisual(
                    MontarVisualPosicionado(visual, area, escala, larguraConteudo, alturaConteudo),
                    nomeDocumento);
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
        /// Uma bobina térmica NÃO é um tamanho padronizado: drivers a expõem como mídia própria,
        /// e o WPF entrega essas entradas com <see cref="PageMediaSize.PageMediaSizeName"/> nulo.
        /// Filtrar só por largura entre 40 e 105 mm não serve — envelopes (JapanChou4 90×205),
        /// postais e ISO A6 caem nessa faixa e seriam escolhidos por engano, fazendo o app trocar
        /// o papel da fila por um envelope.
        /// </summary>
        public static bool PareceMidiaDeBobina(PageMediaSize media)
        {
            if (media?.Width is not double largura || media.Height is not double altura) return false;
            if (largura < LarguraRoloMinimaPx || largura > LarguraRoloMaximaPx) return false;
            if (altura <= 0) return false;

            // Tamanho com nome padronizado (ISO/NorthAmerica/Japan/PRC...) nunca é bobina.
            return media.PageMediaSizeName is null or PageMediaSizeName.Unknown;
        }

        /// <summary>
        /// Escolhe, entre as mídias que o driver realmente declara, a mais próxima de 80 mm de
        /// largura. Usar um objeto vindo das capabilities garante que a validação não descarte.
        /// </summary>
        private static PrintTicket MontarTicketBobina(PrintQueue queue, out bool usouMidiaDeRolo)
        {
            usouMidiaDeRolo = false;
            PrintTicket baseTicket = queue.UserPrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket();

            try
            {
                var caps = queue.GetPrintCapabilities(baseTicket);

                PageMediaSize? rolo = caps.PageMediaSizeCapability
                    .Where(PareceMidiaDeBobina)
                    // mais próxima de 80 mm; empate → a mais alta (rolo longo em vez de etiqueta)
                    .OrderBy(m => Math.Abs(m.Width!.Value - LarguraPapelAlvoPx))
                    .ThenByDescending(m => m.Height!.Value)
                    .FirstOrDefault();

                if (rolo == null) return baseTicket;

                var desejado = new PrintTicket
                {
                    PageMediaSize = rolo,
                    PageOrientation = PageOrientation.Portrait
                };

                var validado = queue.MergeAndValidatePrintTicket(baseTicket, desejado).ValidatedPrintTicket;

                // Só considera sucesso se a validação MANTEVE a largura de bobina — driver que
                // descarta a mídia devolve o default (A4) sem lançar exceção.
                usouMidiaDeRolo = validado.PageMediaSize?.Width is double w &&
                                  w >= LarguraRoloMinimaPx && w <= LarguraRoloMaximaPx;

                return validado;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MontarTicketBobina: {ex.Message}");
                return baseTicket;
            }
        }

        private static PageImageableArea? ObterAreaImprimivel(PrintQueue queue, PrintTicket ticket)
        {
            try { return queue.GetPrintCapabilities(ticket).PageImageableArea; }
            catch { return null; }
        }

        /// <summary>
        /// Envolve o cupom num visual que respeita a margem não-imprimível do driver (OriginWidth/
        /// OriginHeight) e aplica a escala de segurança. Sem o deslocamento da origem, drivers com
        /// margem física deslocam o conteúdo e cortam a borda direita.
        /// </summary>
        private static Visual MontarVisualPosicionado(
            FrameworkElement visual, PageImageableArea? area, double escala, double largura, double altura)
        {
            double offsetX = area?.OriginWidth is > 0 ? area.OriginWidth : 0;
            double offsetY = area?.OriginHeight is > 0 ? area.OriginHeight : 0;

            if (offsetX == 0 && offsetY == 0 && Math.Abs(escala - 1.0) < 0.001)
                return visual;

            var container = new DrawingVisual();
            using (DrawingContext dc = container.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(offsetX, offsetY));
                dc.PushTransform(new ScaleTransform(escala, escala));
                dc.DrawRectangle(
                    new VisualBrush(visual) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                    null,
                    new Rect(0, 0, largura, altura));
                dc.Pop();
                dc.Pop();
            }
            return container;
        }

        private static string DescreverMidia(PageMediaSize? media)
        {
            if (media?.Width is not double w || media.Height is not double h)
                return "de tamanho não informado";
            string nome = media.PageMediaSizeName?.ToString() ?? "personalizado";
            return $"{nome} ({w / DipPorMm:0} × {h / DipPorMm:0} mm)";
        }

        /// <summary>
        /// Verifica se a fila está em condição de receber o trabalho antes de mandar imprimir —
        /// impressoras em rede caem/ficam sem papel com mais frequência que uma USB ao lado da
        /// máquina, e sem essa checagem o app manda o job e o cupom simplesmente não sai.
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
