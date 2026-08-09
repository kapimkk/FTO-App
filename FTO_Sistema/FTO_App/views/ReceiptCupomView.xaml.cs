using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FTO_App.Models;
using FTO_App.Services;

namespace FTO_App.Views
{
    public partial class ReceiptCupomView : UserControl
    {
        /// <summary>Largura nominal do cupom 80mm em pixels (96 DPI).</summary>
        public const double LarguraCupomPx = 302;

        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public ReceiptCupomView()
        {
            InitializeComponent();
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

        /// <summary>Define largura fixa e mede o layout para impressão sem área vazia lateral.</summary>
        public void PrepararParaImpressao(double larguraPx)
        {
            if (larguraPx <= 0) larguraPx = LarguraCupomPx;

            Width = larguraPx;
            BorderCupom.Width = larguraPx;

            Measure(new Size(larguraPx, double.PositiveInfinity));
            Arrange(new Rect(0, 0, larguraPx, DesiredSize.Height));
            UpdateLayout();
        }
    }
}
