using System;
using System.Collections.Generic;
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
    /// Duas coisas decidem se o cupom sai limpo, e as DUAS mudam quando a mesma impressora é
    /// instalada uma segunda vez por rede (o Windows cria outra fila, com outros padrões):
    ///
    /// 1) TAMANHO DE PÁGINA. Uma bobina de 80 mm tem ~72 mm imprimíveis. Fila em A4 → o driver
    ///    rasteriza 210 mm e a impressora imprime só os ~72 mm da esquerda: cupom cortado.
    ///
    /// 2) COR / QUALIDADE / RESOLUÇÃO. Cabeça térmica é 1 bit: só queima ou não queima o ponto.
    ///    Se a fila estiver em Color, o driver converte cinza/ClearType em retícula (meio-tom) e
    ///    o texto sai esgarçado; se estiver em Draft, imprime com menos pontos e sai apagado. Em
    ///    ambos os casos o cupom sai "falhado" — mesmo com o papel certo e sem nada cortado.
    ///
    /// Por isso o PrintTicket é montado aqui explicitamente, em vez de confiar no padrão da fila:
    /// é o que faz a fila de rede imprimir igual à de USB.
    ///
    /// Regra que vale para o visual: NADA de VisualBrush/bitmap no caminho de impressão. O que vai
    /// para o XPS tem de ser vetor (texto vira &lt;Glyphs&gt;), senão o conteúdo é rasterizado a
    /// 96 DPI e reamostrado para os 203 DPI da térmica — que é outra forma de sair falhado.
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

                // 1) Ticket térmico: bobina que o PRÓPRIO driver declara (mídia inventada é
                //    rejeitada na validação e volta silenciosamente para o default da fila),
                //    monocromático, sem rascunho e na maior resolução declarada.
                PrintTicket ticket = MontarTicketTermico(queue, out bool usouMidiaDeRolo);

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

                // 3) Layout na largura imprimível de verdade, com a margem física do driver
                //    compensada e escala de segurança se ainda assim não couber.
                double larguraLayoutPx = Math.Clamp(larguraUtilPx, LarguraRoloMinimaPx, LarguraRoloMaximaPx);
                double escala = EscalaDeSeguranca(larguraLayoutPx, larguraUtilPx);
                double margemX = area?.OriginWidth is > 0 ? area.OriginWidth : 0;
                double margemY = area?.OriginHeight is > 0 ? area.OriginHeight : 0;

                PrepararVisualParaImpressao(visual, larguraLayoutPx, margemX, margemY, escala);

                var dialog = new PrintDialog { PrintQueue = queue, PrintTicket = ticket };
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
        /// Fator para encolher o cupom quando a largura de layout ainda passa da área imprimível
        /// (acontece em impressora de etiqueta, mais estreita que o mínimo do layout).
        /// </summary>
        public static double EscalaDeSeguranca(double larguraLayoutPx, double larguraUtilPx)
        {
            if (larguraLayoutPx <= 0 || larguraUtilPx <= 0) return 1.0;
            return larguraLayoutPx > larguraUtilPx ? larguraUtilPx / larguraLayoutPx : 1.0;
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
        /// Cabeça térmica é 1 bit. Em Color o driver aplica meio-tom (retícula) no cinza da
        /// antialiasing e do ClearType, e o texto sai pontilhado/esgarçado. Monochrome faz o
        /// driver decidir preto-ou-branco por ponto, que é o comportamento certo aqui.
        /// </summary>
        public static OutputColor? EscolherCor(IEnumerable<OutputColor>? disponiveis)
        {
            var declaradas = disponiveis?.ToList() ?? new List<OutputColor>();
            foreach (var cor in new[] { OutputColor.Monochrome, OutputColor.Grayscale })
                if (declaradas.Contains(cor)) return cor;
            return null;
        }

        /// <summary>
        /// Draft numa térmica = menos energia/pontos por linha: cupom apagado e com falhas.
        /// Normal é o alvo; High só se a fila não declarar Normal.
        /// </summary>
        public static OutputQuality? EscolherQualidade(IEnumerable<OutputQuality>? disponiveis)
        {
            var declaradas = disponiveis?.ToList() ?? new List<OutputQuality>();
            foreach (var q in new[] { OutputQuality.Normal, OutputQuality.High, OutputQuality.Text })
                if (declaradas.Contains(q)) return q;
            return null;
        }

        /// <summary>
        /// Maior resolução horizontal declarada (203 dpi nas térmicas de 80 mm). No empate,
        /// prefere a mais "quadrada": 203×203 em vez de 203×406 — mesma nitidez, metade dos
        /// dados trafegando até a impressora, o que importa quando ela está na rede.
        /// </summary>
        public static PageResolution? EscolherResolucao(IEnumerable<PageResolution>? disponiveis)
        {
            return disponiveis?
                .Where(r => r?.X is > 0)
                .OrderByDescending(r => r.X!.Value)
                .ThenBy(r => Math.Abs((r.Y ?? r.X!.Value) - r.X!.Value))
                .FirstOrDefault();
        }

        /// <summary>
        /// Monta o ticket a partir das capacidades que o driver realmente declara. Usar objetos
        /// vindos das capabilities garante que <see cref="PrintQueue.MergeAndValidatePrintTicket"/>
        /// não descarte a escolha e devolva o default da fila (que é o A4 do caso de rede).
        /// </summary>
        private static PrintTicket MontarTicketTermico(PrintQueue queue, out bool usouMidiaDeRolo)
        {
            usouMidiaDeRolo = false;
            PrintTicket baseTicket = queue.UserPrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket();

            try
            {
                var caps = queue.GetPrintCapabilities(baseTicket);

                var desejado = new PrintTicket { PageOrientation = PageOrientation.Portrait };

                PageMediaSize? rolo = caps.PageMediaSizeCapability
                    .Where(PareceMidiaDeBobina)
                    // mais próxima de 80 mm; empate → a mais alta (rolo longo em vez de etiqueta)
                    .OrderBy(m => Math.Abs(m.Width!.Value - LarguraPapelAlvoPx))
                    .ThenByDescending(m => m.Height!.Value)
                    .FirstOrDefault();

                if (rolo != null) desejado.PageMediaSize = rolo;
                if (EscolherCor(caps.OutputColorCapability) is OutputColor cor) desejado.OutputColor = cor;
                if (EscolherQualidade(caps.OutputQualityCapability) is OutputQuality qualidade) desejado.OutputQuality = qualidade;
                if (EscolherResolucao(caps.PageResolutionCapability) is PageResolution resolucao) desejado.PageResolution = resolucao;

                var validado = queue.MergeAndValidatePrintTicket(baseTicket, desejado).ValidatedPrintTicket;

                // Só considera bobina se a validação MANTEVE a largura de rolo — driver que
                // descarta a mídia devolve o default (A4) sem lançar exceção.
                usouMidiaDeRolo = validado.PageMediaSize?.Width is double w &&
                                  w >= LarguraRoloMinimaPx && w <= LarguraRoloMaximaPx;

                return validado;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MontarTicketTermico: {ex.Message}");
                return baseTicket;
            }
        }

        private static PageImageableArea? ObterAreaImprimivel(PrintQueue queue, PrintTicket ticket)
        {
            try { return queue.GetPrintCapabilities(ticket).PageImageableArea; }
            catch { return null; }
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

        private static void PrepararVisualParaImpressao(
            FrameworkElement visual, double larguraPx, double margemX, double margemY, double escala)
        {
            ReceiptCupomView? cupomView = visual as ReceiptCupomView ?? visual.Parent as ReceiptCupomView;
            if (cupomView != null)
            {
                cupomView.PrepararParaImpressao(larguraPx, margemX, margemY, escala, modoImpressao: true);
                return;
            }

            visual.LayoutTransform = Math.Abs(escala - 1.0) < 0.001
                ? Transform.Identity
                : new ScaleTransform(escala, escala);
            visual.Margin = new Thickness(margemX, margemY, 0, 0);
            visual.Width = larguraPx;
            visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            visual.Arrange(new Rect(0, 0, visual.DesiredSize.Width, visual.DesiredSize.Height));
            visual.UpdateLayout();
        }
    }
}
