using System.Windows;
using System.Windows.Controls;

namespace FTO_App.Views
{
    /// <summary>Diálogo de cancelamento NFS-e (POST /api/v1/nfse/cancelar) — códigoMotivo 1/2/9 + justificativa.</summary>
    public partial class CancelamentoNfseWindow : Window
    {
        public string CodigoMotivo { get; private set; } = "9";
        public string Justificativa { get; private set; } = string.Empty;

        public CancelamentoNfseWindow(string resumoNota)
        {
            InitializeComponent();
            LblResumo.Text = resumoNota;
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            string texto = TxtJustificativa.Text.Trim();
            if (texto.Length < 15)
            {
                MessageBox.Show("A justificativa precisa ter pelo menos 15 caracteres.", "Cancelamento NFS-e",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (texto.Length > 255)
            {
                MessageBox.Show("A justificativa deve ter no máximo 255 caracteres.", "Cancelamento NFS-e",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CodigoMotivo = (CbMotivo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "9";
            Justificativa = texto;
            DialogResult = true;
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
