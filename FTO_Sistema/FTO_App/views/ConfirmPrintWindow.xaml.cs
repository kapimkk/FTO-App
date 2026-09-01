using System;
using System.Windows;
using FTO_App.Models;
using FTO_App.Services;
using Microsoft.Win32;

namespace FTO_App.Views
{
    public partial class ConfirmPrintWindow : Window
    {
        private readonly Venda _venda;

        public ConfirmPrintWindow(Venda venda)
        {
            InitializeComponent();
            _venda = venda ?? throw new ArgumentNullException(nameof(venda));
            CupomView.Inicializar(_venda);
            CupomView.PrepararParaImpressao(ReceiptCupomView.LarguraCupomPx);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnConfirmPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? impressora = DeviceSettingsStore.Current.SelectedPrinter;
                if (string.IsNullOrWhiteSpace(impressora))
                {
                    MessageBox.Show(
                        "Selecione uma impressora na tela de módulos (após o login).\n" +
                        "Recomendado: MP-2500 HT.",
                        "Impressora não configurada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!CupomPrintHelper.ImprimirNaImpressoraConfigurada(
                        CupomView.CupomParaImpressao,
                        $"Cupom FTO {_venda.Id}",
                        impressora,
                        out string? erro,
                        out string? aviso))
                {
                    MessageBox.Show(
                        erro ?? "Não foi possível imprimir o cupom.",
                        "Erro na impressão",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Fila configurada fora do rolo térmico: imprime, mas o usuário precisa saber
                // por que pode sair cortado e onde ajustar.
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(aviso)
                        ? "Cupom enviado para a impressora com sucesso!"
                        : $"Cupom enviado para a impressora.\n\n⚠️ {aviso}",
                    "Impressão",
                    MessageBoxButton.OK,
                    string.IsNullOrWhiteSpace(aviso) ? MessageBoxImage.Information : MessageBoxImage.Warning);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Não foi possível imprimir.\n\n{ex.Message}",
                    "Erro na impressão",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnDownloadPdf_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"Cupom_FTO_{_venda.Id}_{DateTime.Now:yyyyMMdd}"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            try
            {
                PdfService.GerarCupomPdf(_venda, saveDialog.FileName);
                MessageBox.Show(
                    $"PDF salvo com sucesso!\n\n{saveDialog.FileName}",
                    "Cupom PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Não foi possível gerar o PDF.\n\n{ex.Message}",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
