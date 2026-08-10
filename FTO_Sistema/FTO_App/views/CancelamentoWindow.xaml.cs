using System.Windows;

namespace FTO_App.Views
{
    /// <summary>Diálogo de confirmação + justificativa para POST /nfe/cancelar (NF-e).</summary>
    public partial class CancelamentoWindow : Window
    {
        public string Justificativa { get; private set; } = string.Empty;

        public CancelamentoWindow(string resumoNota)
        {
            InitializeComponent();
            LblResumo.Text = resumoNota;
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            string texto = TxtJustificativa.Text.Trim();
            if (texto.Length < 15)
            {
                MessageBox.Show("A justificativa precisa ter pelo menos 15 caracteres.", "Cancelamento",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Justificativa = texto;
            DialogResult = true;
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
