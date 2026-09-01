using System;
using System.Linq;
using System.Printing;
using System.Text;

namespace FTO_App.Services
{
    /// <summary>
    /// Relatório do que o driver realmente declara para uma fila de impressão.
    ///
    /// Existe porque "o cupom sai cortado só em rede" é invisível pelo código: a diferença está
    /// no tamanho de página e na área imprimível que CADA fila do Windows expõe. Rodar isso na
    /// fila USB e na fila de rede mostra a divergência em vez de deixar no palpite.
    /// </summary>
    public static class ImpressoraDiagnostico
    {
        private const double DipPorMm = 96.0 / 25.4;

        public static string Gerar(string? nomeImpressora)
        {
            if (string.IsNullOrWhiteSpace(nomeImpressora))
                return "Nenhuma impressora selecionada.";

            var sb = new StringBuilder();
            try
            {
                using var server = new LocalPrintServer();
                using PrintQueue queue = server.GetPrintQueue(nomeImpressora);
                queue.Refresh();

                sb.AppendLine($"Impressora: {queue.Name}");
                sb.AppendLine($"Driver:     {SafeGet(() => queue.QueueDriver?.Name)}");
                sb.AppendLine($"Porta:      {SafeGet(() => queue.QueuePort?.Name)}");
                sb.AppendLine($"Compartilhada: {SafeGet(() => queue.IsShared.ToString())}   " +
                              $"XPS nativo: {SafeGet(() => queue.IsXpsDevice.ToString())}");
                sb.AppendLine($"Status:     {queue.QueueStatus}");
                sb.AppendLine($"Trabalhos na fila: {SafeGet(() => queue.NumberOfJobs.ToString())}");
                sb.AppendLine();

                PrintTicket ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket();
                sb.AppendLine($"Papel atual da fila: {DescreverMidia(ticket.PageMediaSize)}");
                sb.AppendLine($"Orientação: {ticket.PageOrientation}");
                sb.AppendLine();

                var caps = queue.GetPrintCapabilities(ticket);
                var area = caps.PageImageableArea;
                if (area != null)
                {
                    sb.AppendLine("Área imprimível com esse papel:");
                    sb.AppendLine($"  largura: {area.ExtentWidth:0.#} DIP  ({area.ExtentWidth / DipPorMm:0.#} mm)");
                    sb.AppendLine($"  altura:  {area.ExtentHeight:0.#} DIP  ({area.ExtentHeight / DipPorMm:0.#} mm)");
                    sb.AppendLine($"  origem:  x={area.OriginWidth:0.#}  y={area.OriginHeight:0.#} DIP");
                    sb.AppendLine();
                    sb.AppendLine(area.ExtentWidth / DipPorMm > 105
                        ? "  ⚠️ ATENÇÃO: largura acima de 105 mm — a fila NÃO está no rolo térmico.\n" +
                          "     O driver vai rasterizar uma página larga e a impressora só imprime\n" +
                          "     os ~72 mm da esquerda: é isso que corta o cupom."
                        : "  ✅ Largura compatível com bobina térmica.");
                }
                else
                {
                    sb.AppendLine("Área imprimível: não informada pelo driver.");
                }
                sb.AppendLine();

                sb.AppendLine("Tamanhos de papel que este driver oferece:");
                var midias = caps.PageMediaSizeCapability
                    .Where(m => m.Width is > 0 && m.Height is > 0)
                    .OrderBy(m => m.Width!.Value)
                    .ToList();

                if (midias.Count == 0)
                {
                    sb.AppendLine("  (nenhum) — driver não expõe tamanhos; provavelmente genérico.");
                }
                else
                {
                    foreach (var m in midias.Take(40))
                        sb.AppendLine("  - " + DescreverMidia(m));

                    // Mesmo critério usado na impressão — tamanho padronizado (envelope, A6,
                    // postal) cai na faixa de largura mas não é bobina.
                    var rolos = midias.Where(CupomPrintHelper.PareceMidiaDeBobina).ToList();
                    sb.AppendLine();
                    sb.AppendLine(rolos.Count > 0
                        ? "  ✅ Bobina térmica disponível: " +
                          string.Join(", ", rolos.Select(DescreverMidia)) +
                          "\n     O app seleciona automaticamente ao imprimir."
                        : "  ⚠️ NENHUMA mídia de bobina neste driver (só tamanhos padronizados).\n" +
                          "     Esta fila não conhece o rolo térmico — provavelmente foi instalado\n" +
                          "     um driver genérico ao adicionar a impressora por rede. Reinstale-a\n" +
                          "     escolhendo o MESMO driver do fabricante usado na instalação USB.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Falha ao consultar a impressora: {ex.Message}");
            }

            return sb.ToString();
        }

        private static string DescreverMidia(PageMediaSize? media)
        {
            if (media?.Width is not double w || media.Height is not double h)
                return "(tamanho não informado)";
            string nome = media.PageMediaSizeName?.ToString() ?? "Personalizado";
            return $"{nome}: {w / DipPorMm:0.#} × {h / DipPorMm:0.#} mm";
        }

        private static string SafeGet(Func<string?> f)
        {
            try { return f() ?? "—"; }
            catch { return "—"; }
        }
    }
}
