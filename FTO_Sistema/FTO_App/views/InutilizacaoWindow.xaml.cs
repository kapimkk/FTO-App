using System;
using System.Windows;

namespace FTO_App.Views
{
    /// <summary>Diálogo para POST /api/v1/nfe/inutilizar (faixa de numeração nunca emitida).</summary>
    public partial class InutilizacaoWindow : Window
    {
        public string Ano { get; private set; } = string.Empty;
        public string Serie { get; private set; } = string.Empty;
        public string NumeroInicial { get; private set; } = string.Empty;
        public string NumeroFinal { get; private set; } = string.Empty;
        public string Justificativa { get; private set; } = string.Empty;

        public InutilizacaoWindow(string serieAtual)
        {
            InitializeComponent();
            TxtAno.Text = DateTime.Now.Year.ToString();
            TxtSerie.Text = string.IsNullOrWhiteSpace(serieAtual) ? "1" : serieAtual;
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            string ano = TxtAno.Text.Trim();
            string serie = TxtSerie.Text.Trim();
            string ini = TxtNumeroInicial.Text.Trim();
            string fim = TxtNumeroFinal.Text.Trim();
            string just = TxtJustificativa.Text.Trim();

            if (ano.Length != 4 || !long.TryParse(ano, out _))
            {
                MessageBox.Show("Informe o ano com 4 dígitos (ex.: 2026).", "Inutilização", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(serie))
            {
                MessageBox.Show("Informe a série.", "Inutilização", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!long.TryParse(ini, out long nIni) || !long.TryParse(fim, out long nFim) || nFim < nIni)
            {
                MessageBox.Show("Informe uma faixa numérica válida (número final ≥ número inicial).", "Inutilização", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (just.Length < 15)
            {
                MessageBox.Show("A justificativa precisa ter pelo menos 15 caracteres.", "Inutilização", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Ano = ano;
            Serie = serie;
            NumeroInicial = ini;
            NumeroFinal = fim;
            Justificativa = just;
            DialogResult = true;
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
