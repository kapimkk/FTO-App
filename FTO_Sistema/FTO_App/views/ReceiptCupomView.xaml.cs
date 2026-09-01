using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FTO_App.Models;
using FTO_App.Services;

namespace FTO_App.Views
{
    public partial class ReceiptCupomView : UserControl
    {
        /// <summary>Largura nominal do cupom 80mm em pixels (96 DPI).</summary>
        public const double LarguraCupomPx = 302;

        /// <summary>Espessura máxima de um Border para ser considerado linha separadora.</summary>
        private const double AlturaMaximaSeparadorPx = 2;

        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        /// <summary>Padding do XAML — a margem física do driver é somada a ele, nunca acumulada.</summary>
        private readonly Thickness _paddingOriginal;

        public ReceiptCupomView()
        {
            InitializeComponent();
            _paddingOriginal = BorderCupom.Padding;
            AplicarDadosEmpresa();
        }

        public FrameworkElement CupomParaImpressao => BorderCupom;

        private void AplicarDadosEmpresa()
        {
            var empresa = EmpresaConfigStore.Current;

            TxtEmpresaNome.Text = empresa.Nome;
            TxtEmpresaSubtitulo.Text = empresa.Subtitulo;
            TxtEmpresaEndereco.Text = empresa.Endereco;
            TxtEmpresaCidade.Text = empresa.Cidade;
            TxtEmpresaTelefone.Text = empresa.TelefoneExibicao;
            TxtEmpresaCnpj.Text = empresa.CnpjExibicao;
            TxtEmpresaIe.Text = empresa.IeExibicao;
            string titulo = string.IsNullOrWhiteSpace(empresa.CupomTitulo)
                ? "Comprovante de Vendas"
                : empresa.CupomTitulo;
            if (string.Equals(titulo, "CUPOM NAO FISCAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(titulo, "CUPOM NÃO FISCAL", StringComparison.OrdinalIgnoreCase))
                titulo = "Comprovante de Vendas";
            TxtCupomTitulo.Text = titulo;
            TxtRodape.Text = empresa.CupomRodape;

            TxtEmpresaSubtitulo.Visibility = string.IsNullOrWhiteSpace(empresa.Subtitulo) ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpresaEndereco.Visibility = string.IsNullOrWhiteSpace(empresa.Endereco) ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpresaCidade.Visibility = string.IsNullOrWhiteSpace(empresa.Cidade) ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpresaTelefone.Visibility = string.IsNullOrWhiteSpace(empresa.Telefone) ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpresaCnpj.Visibility = string.IsNullOrWhiteSpace(empresa.Cnpj) ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpresaIe.Visibility = string.IsNullOrWhiteSpace(empresa.Ie) ? Visibility.Collapsed : Visibility.Visible;
            TxtRodape.Visibility = string.IsNullOrWhiteSpace(empresa.CupomRodape) ? Visibility.Collapsed : Visibility.Visible;
        }

        public void Inicializar(Venda venda)
        {
            if (venda is null) throw new ArgumentNullException(nameof(venda));

            AplicarDadosEmpresa();

            TxtNumeroVenda.Text = venda.Id.ToString(PtBr);
            TxtData.Text = venda.DataFormatada;
            TxtImpressoEm.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", PtBr);
            TxtCliente.Text = string.IsNullOrWhiteSpace(venda.Cliente) ? "-" : venda.Cliente.Trim();
            TxtServico.Text = string.IsNullOrWhiteSpace(venda.TipoServico) ? "-" : venda.TipoServico.Trim();
            TxtTotal.Text = venda.VendaValor.ToString("C2", PtBr);
            TxtFormaPagamento.Text = string.IsNullOrWhiteSpace(venda.FormaPag) ? "-" : venda.FormaPag.Trim();
        }

        /// <summary>Define largura fixa e mede o layout (pré-visualização na tela).</summary>
        public void PrepararParaImpressao(double larguraPx) =>
            PrepararParaImpressao(larguraPx, 0, 0, 1.0, modoImpressao: false);

        /// <summary>
        /// Prepara o cupom para sair na bobina.
        /// </summary>
        /// <param name="larguraConteudoPx">Largura imprimível real da fila, em DIP.</param>
        /// <param name="margemFisicaX">
        /// Margem que a impressora não consegue imprimir (PageImageableArea.OriginWidth). Entra como
        /// PADDING, não como deslocamento do visual: o PrintVisual serializa o conteúdo do elemento,
        /// mas não garante honrar o offset/transform do visual raiz que recebe.
        /// </param>
        /// <param name="escala">Redução de segurança quando o layout não cabe na área imprimível.</param>
        /// <param name="modoImpressao">
        /// Ajusta texto e linhas para a cabeça térmica (1 bit). Fora daqui o cupom continua com a
        /// aparência de tela.
        /// </param>
        public void PrepararParaImpressao(
            double larguraConteudoPx, double margemFisicaX, double margemFisicaY, double escala, bool modoImpressao)
        {
            if (larguraConteudoPx <= 0) larguraConteudoPx = LarguraCupomPx;
            if (!(escala > 0) || double.IsNaN(escala)) escala = 1.0;

            if (modoImpressao) AplicarAjustesTermicos();

            // O padding é aplicado ANTES da escala, então precisa ser dividido por ela para que a
            // margem física caia no lugar certo depois do LayoutTransform.
            double desvioX = Math.Max(0, margemFisicaX) / escala;
            double desvioY = Math.Max(0, margemFisicaY) / escala;

            BorderCupom.LayoutTransform = Math.Abs(escala - 1.0) < 0.001
                ? Transform.Identity
                : new ScaleTransform(escala, escala);
            BorderCupom.Padding = new Thickness(
                _paddingOriginal.Left + desvioX,
                _paddingOriginal.Top + desvioY,
                _paddingOriginal.Right,
                _paddingOriginal.Bottom);
            BorderCupom.Width = larguraConteudoPx + desvioX;

            // Largura automática: com LayoutTransform o tamanho final é o transformado, e fixar
            // Width aqui recortaria o cupom quando escala < 1.
            Width = double.NaN;
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));
            UpdateLayout();
        }

        /// <summary>
        /// Térmica não tem tom de cinza: cada ponto queima ou não queima.
        ///
        /// * ClearType gera franja colorida e Display arredonda o avanço dos glifos para o pixel de
        ///   96 DPI da tela — impresso a 203 DPI isso vira texto esgarçado, com hastes quebradas.
        ///   Ideal + Grayscale é o par recomendado para impressão.
        /// * Linha de 15% de preto vira retícula rala: some ou sai pontilhada. No cupom ela tem de
        ///   ser preta e cheia.
        /// </summary>
        private void AplicarAjustesTermicos()
        {
            TextOptions.SetTextFormattingMode(BorderCupom, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(BorderCupom, TextRenderingMode.Grayscale);

            foreach (Border separador in Separadores(BorderCupom))
            {
                separador.Opacity = 1;
                separador.Background = Brushes.Black;
            }
        }

        private static IEnumerable<Border> Separadores(DependencyObject raiz)
        {
            int filhos = VisualTreeHelper.GetChildrenCount(raiz);
            for (int i = 0; i < filhos; i++)
            {
                DependencyObject filho = VisualTreeHelper.GetChild(raiz, i);

                if (filho is Border borda &&
                    !double.IsNaN(borda.Height) &&
                    borda.Height > 0 &&
                    borda.Height <= AlturaMaximaSeparadorPx)
                {
                    yield return borda;
                }

                foreach (Border neto in Separadores(filho))
                    yield return neto;
            }
        }
    }
}
