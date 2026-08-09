using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using FTO_App.Models;
using FTO_App.Services;

namespace FTO_App.Views
{
    /// <summary>
    /// DANFE simplificada da NFC-e (modelo 65) em layout de cupom térmico 80mm —
    /// reaproveita o mesmo pipeline de impressão (PrintVisual) do cupom não fiscal.
    /// </summary>
    public partial class DanfeNfceCupomView : UserControl
    {
        /// <summary>Largura nominal do cupom 80mm em pixels (96 DPI) — igual ao ReceiptCupomView.</summary>
        public const double LarguraCupomPx = ReceiptCupomView.LarguraCupomPx;

        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public DanfeNfceCupomView()
        {
            InitializeComponent();
        }

        public FrameworkElement CupomParaImpressao => BorderCupom;

        /// <summary>Preenche o cupom com os dados da nota (nota já emitida/autorizada na SEFAZ).</summary>
        public void Inicializar(NotaFiscalModel nota, EmpresaConfig empresa)
        {
            if (nota is null) throw new ArgumentNullException(nameof(nota));
            if (empresa is null) throw new ArgumentNullException(nameof(empresa));

            TxtEmpresaNome.Text = string.IsNullOrWhiteSpace(empresa.NomeFantasia) ? empresa.Nome : empresa.NomeFantasia;
            TxtEmpresaEndereco.Text = MontarEndereco(empresa);
            TxtEmpresaCidade.Text = string.IsNullOrWhiteSpace(empresa.Cidade)
                ? "" : $"{empresa.Cidade}{(string.IsNullOrWhiteSpace(empresa.Uf) ? "" : "/" + empresa.Uf)}";
            TxtEmpresaCnpj.Text = empresa.CnpjExibicao;
            TxtEmpresaIe.Text = empresa.IeExibicao;

            TxtEmpresaEndereco.Visibility = string.IsNullOrWhiteSpace(TxtEmpresaEndereco.Text) ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpresaCidade.Visibility = string.IsNullOrWhiteSpace(TxtEmpresaCidade.Text) ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpresaCnpj.Visibility = string.IsNullOrWhiteSpace(empresa.Cnpj) ? Visibility.Collapsed : Visibility.Visible;
            TxtEmpresaIe.Visibility = string.IsNullOrWhiteSpace(empresa.Ie) ? Visibility.Collapsed : Visibility.Visible;

            // Logo do emitente não é usada na NFC-e (somente NF-e / PDF A4).
            if (ImgLogoEmitente != null)
            {
                ImgLogoEmitente.Source = null;
                ImgLogoEmitente.Visibility = Visibility.Collapsed;
            }

            BorderHomolog.Visibility = nota.Ambiente == "1" ? Visibility.Collapsed : Visibility.Visible;

            TxtItemDescricao.Text = string.IsNullOrWhiteSpace(nota.ProdutoDescricao) ? "-" : nota.ProdutoDescricao.Trim();
            TxtItemQtdUnit.Text =
                $"{nota.ProdutoQuantidade.ToString("0.####", PtBr)} {nota.ProdutoUnidade} x {nota.ProdutoValorUnitario.ToString("C2", PtBr)}";
            TxtItemTotal.Text = nota.ProdutoValorTotal.ToString("C2", PtBr);

            TxtQtdTotalItens.Text = "1";
            TxtValorTotal.Text = nota.ValorTotalNota.ToString("N2", PtBr);

            TxtFormaPagamento.Text = DescreverFormaPagamento(nota.FormaPagamento);

            TxtConsumidor.Text = string.IsNullOrWhiteSpace(nota.DestNome)
                ? "CONSUMIDOR NÃO IDENTIFICADO"
                : string.IsNullOrWhiteSpace(nota.DestCpfCnpj) ? nota.DestNome.Trim() : $"{nota.DestNome.Trim()} — {nota.DestCpfCnpj.Trim()}";

            TxtNumeroSerie.Text = $"{nota.Numero} / {nota.Serie}";
            TxtDataEmissao.Text = nota.DataEmissao.ToString("dd/MM/yyyy HH:mm:ss", PtBr);

            TxtChaveAcesso.Text = FormatarChave(nota.ChaveAcesso);

            if (!string.IsNullOrWhiteSpace(nota.NProt))
            {
                string dh = FormatarDataHora(nota.DhRecbto);
                TxtProtocolo.Text = string.IsNullOrWhiteSpace(dh)
                    ? $"Protocolo de autorização: {nota.NProt}"
                    : $"Protocolo de autorização: {nota.NProt} — {dh}";
                BorderProtocolo.Visibility = Visibility.Visible;
            }
            else
            {
                BorderProtocolo.Visibility = Visibility.Collapsed;
            }

            var qrUrl = NfceQrCodeNormalizer.Resolver(nota.QrCodeUrl, nota.XmlAutorizado) ?? nota.QrCodeUrl;
            var qr = QrCodeImageService.GerarImagem(qrUrl);
            if (qr != null)
            {
                ImgQrCode.Source = qr;
                ImgQrCode.Visibility = Visibility.Visible;
                TxtQrIndisponivel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ImgQrCode.Visibility = Visibility.Collapsed;
                TxtQrIndisponivel.Visibility = Visibility.Visible;
            }
        }

        private static string MontarEndereco(EmpresaConfig empresa)
        {
            var partes = new[]
            {
                empresa.Endereco,
                string.IsNullOrWhiteSpace(empresa.Numero) ? null : $"nº {empresa.Numero}",
                empresa.Bairro
            }.Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(", ", partes);
        }

        private static string DescreverFormaPagamento(string? codigo) => (codigo ?? "").Trim() switch
        {
            "01" => "Dinheiro",
            "02" => "Cheque",
            "03" => "Cartão de Crédito",
            "04" => "Cartão de Débito",
            "05" => "Crédito Loja",
            "10" => "Vale Alimentação",
            "11" => "Vale Refeição",
            "15" => "Boleto Bancário",
            "17" => "PIX",
            "99" => "Outros",
            "" => "-",
            var outro => outro
        };

        /// <summary>Agrupa os 44 dígitos da chave de acesso em blocos de 4, como no DANFE oficial.</summary>
        private static string FormatarChave(string? chave)
        {
            if (string.IsNullOrWhiteSpace(chave)) return "-";

            string digitos = new string(chave.Where(char.IsDigit).ToArray());
            if (digitos.Length == 0) return chave.Trim();

            var sb = new StringBuilder();
            for (int i = 0; i < digitos.Length; i += 4)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(digitos.Substring(i, Math.Min(4, digitos.Length - i)));
            }
            return sb.ToString();
        }

        private static string FormatarDataHora(string? dh)
        {
            if (string.IsNullOrWhiteSpace(dh)) return "";
            if (DateTimeOffset.TryParse(dh, PtBr, DateTimeStyles.None, out var dto))
                return dto.LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss", PtBr);
            if (DateTime.TryParse(dh, PtBr, DateTimeStyles.None, out var dt))
                return dt.ToString("dd/MM/yyyy HH:mm:ss", PtBr);
            return dh;
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
