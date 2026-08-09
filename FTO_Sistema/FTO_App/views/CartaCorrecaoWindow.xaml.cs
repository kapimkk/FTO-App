using System.Windows;

namespace FTO_App.Views
{
    /// <summary>Diálogo para POST /api/v1/nfe/carta-correcao.</summary>
    public partial class CartaCorrecaoWindow : Window
    {
        private static readonly string[] PalavrasProibidas = { "VALOR", "DESTINATARIO", "IMPOSTO", "PRECO" };

        public string Correcao { get; private set; } = string.Empty;
        public int Sequencial { get; private set; } = 1;

        public CartaCorrecaoWindow()
        {
            InitializeComponent();
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            string texto = TxtCorrecao.Text.Trim();
            if (texto.Length < 15)
            {
                MessageBox.Show("O texto da correção precisa ter pelo menos 15 caracteres.", "Carta de Correção",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string textoUpper = texto.ToUpperInvariant();
            foreach (string palavra in PalavrasProibidas)
            {
                if (textoUpper.Contains(palavra))
                {
                    MessageBox.Show($"O texto contém a palavra \"{palavra}\" — não é permitido corrigir valor, destinatário, imposto ou preço por CC-e.",
                        "Carta de Correção", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (!int.TryParse(TxtSequencial.Text.Trim(), out int seq) || seq < 1)
            {
                MessageBox.Show("Informe um sequencial válido (número inteiro ≥ 1).", "Carta de Correção",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Correcao = texto;
            Sequencial = seq;
            DialogResult = true;
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
