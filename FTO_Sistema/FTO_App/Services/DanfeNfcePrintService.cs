using System;
using FTO_App.Models;
using FTO_App.Views;

namespace FTO_App.Services
{
    /// <summary>
    /// Impressão da DANFE simplificada da NFC-e (modelo 65) na impressora térmica configurada —
    /// reaproveita o mesmo pipeline WPF (PrintVisual) já usado para o cupom não fiscal.
    /// </summary>
    public static class DanfeNfcePrintService
    {
        public static void Imprimir(NotaFiscalModel nota, EmpresaConfig empresa)
        {
            if (nota is null) throw new ArgumentNullException(nameof(nota));
            if (empresa is null) throw new ArgumentNullException(nameof(empresa));

            if (!string.Equals((nota.Modelo ?? "").Trim(), "65", StringComparison.Ordinal))
                throw new InvalidOperationException("Impressão térmica de DANFE só é aplicável à NFC-e (modelo 65).");

            if (string.IsNullOrWhiteSpace(nota.ChaveAcesso))
                throw new InvalidOperationException("Esta nota ainda não foi emitida/autorizada na SEFAZ — não há chave de acesso para imprimir.");

            string printer = DeviceSettingsStore.Current.SelectedPrinter;
            if (string.IsNullOrWhiteSpace(printer))
                throw new InvalidOperationException(
                    "Nenhuma impressora selecionada. Configure na tela de módulos (após o login).");

            var danfe = new DanfeNfceCupomView();
            danfe.Inicializar(nota, empresa);
            danfe.PrepararParaImpressao(DanfeNfceCupomView.LarguraCupomPx);

            if (!CupomPrintHelper.ImprimirNaImpressoraConfigurada(
                    danfe.CupomParaImpressao,
                    $"DANFE NFC-e {nota.NumeroExibicao}",
                    printer,
                    out string? erro))
                throw new InvalidOperationException(erro ?? "Falha ao imprimir a DANFE NFC-e.");
        }
    }
}
