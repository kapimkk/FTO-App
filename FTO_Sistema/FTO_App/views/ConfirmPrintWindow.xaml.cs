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
                        out string? erro))
                {
                    MessageBox.Show(
                        erro ?? "Não foi possível imprimir o cupom.",
                        "Erro na impressão",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show(
                    "Cupom enviado para a impressora com sucesso!",
                    "Impressão",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

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
