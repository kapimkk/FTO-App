using FTO_App.Models;
using FTO_App.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FTO_App.Views
{
    /// <summary>
    /// Ações fiscais de uma NFS-e já lançada: emitir no SEFIN, baixar/ver XML, DANFSE e cancelar.
    /// Sem nProt — chave de acesso tem 50 dígitos.
    /// </summary>
    public partial class NotaServicoAcoesWindow : Window
    {
        private readonly NotaServicoModel _nota;

        public bool HouveAlteracao { get; private set; }

        public NotaServicoAcoesWindow(NotaServicoModel nota)
        {
            InitializeComponent();
            _nota = nota ?? throw new ArgumentNullException(nameof(nota));
            SetComboTag(CbAmbiente, string.IsNullOrWhiteSpace(_nota.Ambiente) ? "2" : _nota.Ambiente);
            AtualizarCabecalho();
            AtualizarPainel();
        }

        private void AtualizarCabecalho()
        {
            LblTitulo.Text = $"⚡ Ações NFS-e — {_nota.NumeroExibicao}";
            LblResumo.Text =
                $"{_nota.TomadorNome}\n{_nota.DescricaoServico} — {_nota.ValorFormatado} · Competência {_nota.DataCompetenciaFormatada} · Status: {_nota.Status}";
        }

        private void AtualizarPainel()
        {
            TxtChaveAcesso.Text = _nota.ChaveAcesso;
            TxtIdDps.Text = _nota.IdDps;
            TxtStatusFiscal.Text = string.IsNullOrWhiteSpace(_nota.CStat)
                ? _nota.XMotivo
                : $"{_nota.CStat} - {_nota.XMotivo}";
        }

        private static void SetComboTag(ComboBox cb, string tag)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string GetComboTag(ComboBox? cb, string fallback)
        {
            if (cb?.SelectedItem is ComboBoxItem item && item.Tag != null)
                return item.Tag.ToString() ?? fallback;
            return fallback;
        }

        private string AmbienteAtual() => GetComboTag(CbAmbiente, "2");

        private static string BaseUrlNfse() => EmpresaConfigStore.Current.FiscalApiUrlNfse;

        private bool ValidarDadosMinimos()
        {
            var cfg = EmpresaConfigStore.Current;
            if (string.IsNullOrWhiteSpace(cfg.Cnpj) || string.IsNullOrWhiteSpace(cfg.CodigoIbge))
            {
                MessageBox.Show("Complete CNPJ e código IBGE da empresa em Configurações antes de emitir.",
                    "NFS-e", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(cfg.Endereco) || string.IsNullOrWhiteSpace(cfg.Bairro) ||
                string.IsNullOrWhiteSpace(cfg.Cep) || string.IsNullOrWhiteSpace(cfg.Uf))
            {
                MessageBox.Show("Complete o endereço do prestador (logradouro, bairro, CEP, UF) em Configurações.",
                    "NFS-e", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string doc = Digitos(_nota.TomadorCpfCnpj);
            if (doc.Length is not (11 or 14) || string.IsNullOrWhiteSpace(_nota.TomadorNome))
            {
                MessageBox.Show("Tomador obrigatório: nome e CPF (11) ou CNPJ (14) válidos.\nCNPJ precisa existir na Receita Federal (SEFIN E0188).",
                    "NFS-e", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(_nota.DescricaoServico) || _nota.ValorServico <= 0)
            {
                MessageBox.Show("Informe descrição do serviço e valor maior que zero.", "NFS-e",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            string trib = Digitos(_nota.CodTribNac);
            if (trib.Length != 6)
            {
                MessageBox.Show("cTribNac deve ter 6 dígitos (ex.: 010101).", "NFS-e",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (_nota.IncluirIbsCbs && Digitos(_nota.CodNbs).Length != 9)
            {
                MessageBox.Show("Com IBS/CBS, o NBS (9 dígitos) é obrigatório.", "NFS-e",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private bool ExigirChave()
        {
            if (!string.IsNullOrWhiteSpace(TxtChaveAcesso?.Text)) return true;
            MessageBox.Show("Emita a NFS-e primeiro para obter a chave de acesso (50 dígitos).",
                "NFS-e", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private void SalvarResultado()
        {
            Database.ExecuteNonQuery(@"
                UPDATE notasservico SET
                    chaveacesso=@ca, iddps=@id, dataprocessamento=@dp, cstat=@cs, xmotivo=@xm,
                    xmlenviado=@xe, xmlautorizado=@xa, status=@st, ambiente=@amb
                WHERE id=@nid",
                new Dictionary<string, object>
                {
                    ["@ca"] = _nota.ChaveAcesso,
                    ["@id"] = _nota.IdDps,
                    ["@dp"] = _nota.DataProcessamento,
                    ["@cs"] = _nota.CStat,
                    ["@xm"] = _nota.XMotivo,
                    ["@xe"] = _nota.XmlEnviado,
                    ["@xa"] = _nota.XmlAutorizado,
                    ["@st"] = _nota.Status,
                    ["@amb"] = _nota.Ambiente,
                    ["@nid"] = _nota.Id
                });
            HouveAlteracao = true;
        }

        private async void BtnEmitirApi_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarDadosMinimos()) return;

            var cfg = EmpresaConfigStore.Current;
            string baseUrl = BaseUrlNfse();
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(cfg.FiscalApiKey))
            {
                MessageBox.Show("Configure a URL da API NFS-e e a API Key (escopo NFSe/Full) em Configurações → Fiscal.",
                    "NFS-e", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _nota.Ambiente = FiscalApiClient.NormalizarTpAmb(AmbienteAtual());
            if (string.IsNullOrWhiteSpace(_nota.CodigoIbgeEmissao))
                _nota.CodigoIbgeEmissao = cfg.CodigoIbge;
            if (string.IsNullOrWhiteSpace(_nota.CodIbgePrestacao))
                _nota.CodIbgePrestacao = _nota.CodigoIbgeEmissao;

            BtnEmitirApi.IsEnabled = false;
            TxtStatusFiscal.Text = "⏳ Emitindo no SEFIN Nacional...";
            try
            {
                var resultado = await FiscalApiClient.EmitirNfseAsync(_nota, cfg, baseUrl, cfg.FiscalApiKey);
                if (!resultado.Sucesso)
                {
                    TxtStatusFiscal.Text = "";
                    MessageBox.Show($"Falha ao emitir NFS-e:\n\n{resultado.ResumoErro()}", "Emissão NFS-e",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dados = resultado.Dados!;
                _nota.ChaveAcesso = dados.ChaveAcesso ?? "";
                _nota.IdDps = dados.IdDps ?? "";
                _nota.DataProcessamento = dados.DataProcessamento ?? "";
                _nota.CStat = dados.CStat ?? "";
                _nota.XMotivo = dados.XMotivo ?? dados.Erro ?? "";
                _nota.XmlEnviado = dados.XmlEnviado ?? "";
                _nota.XmlAutorizado = dados.XmlAutorizado ?? "";
                _nota.Status = dados.Aprovado ? "Emitida" : "Rejeitada";

                SalvarResultado();
                if (dados.Aprovado)
                    AtualizarUltimoNumeroLocal(_nota.NumeroDps);

                AtualizarPainel();
                AtualizarCabecalho();

                if (dados.Aprovado)
                {
                    MessageBox.Show(
                        $"✅ NFS-e autorizada!\n\nChave: {_nota.ChaveAcesso}\nId DPS: {_nota.IdDps}\n{_nota.CStat} - {_nota.XMotivo}",
                        "Emissão NFS-e", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"⚠️ NFS-e NÃO autorizada.\n\n{_nota.CStat} - {_nota.XMotivo}\n{dados.Erro}",
                        "Emissão NFS-e", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro inesperado: {ex.Message}", "NFS-e", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnEmitirApi.IsEnabled = true;
            }
        }

        private static void AtualizarUltimoNumeroLocal(long numero)
        {
            var cfg = EmpresaConfigStore.Current;
            if (long.TryParse(cfg.UltimoNumeroNfse, out long atual) && numero <= atual) return;
            cfg.UltimoNumeroNfse = numero.ToString();
            EmpresaConfigStore.Save(cfg);
        }

        private async void BtnBaixarXml_Click(object sender, RoutedEventArgs e)
        {
            if (!ExigirChave()) return;
            var cfg = EmpresaConfigStore.Current;
            var resultado = await FiscalApiClient.ObterXmlAsync(
                BaseUrlNfse(), cfg.FiscalApiKey, TxtChaveAcesso.Text.Trim(), AmbienteAtual());

            if (!resultado.Sucesso)
            {
                // Fallback: XML já gravado na emissão
                if (!string.IsNullOrWhiteSpace(_nota.XmlAutorizado))
                {
                    SalvarXmlEmDisco(_nota.XmlAutorizado, "autorizado");
                    return;
                }
                MessageBox.Show($"Falha ao baixar XML:\n\n{resultado.ResumoErro()}", "XML NFS-e",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SalvarXmlEmDisco(resultado.Dados!, "autorizado");
        }

        private void BtnVerXml_Click(object sender, RoutedEventArgs e)
        {
            string xml = !string.IsNullOrWhiteSpace(_nota.XmlAutorizado)
                ? _nota.XmlAutorizado
                : _nota.XmlEnviado;
            if (string.IsNullOrWhiteSpace(xml))
            {
                MessageBox.Show("Não há XML armazenado nesta NFS-e. Emita ou baixe o XML primeiro.",
                    "NFS-e", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new XmlViewerWindow(
                $"👁 XML NFS-e — {_nota.ChaveAcesso}",
                xml,
                $"NFSe_{_nota.ChaveAcesso}.xml")
            { Owner = this };
            win.ShowDialog();
        }

        private async void BtnDanfse_Click(object sender, RoutedEventArgs e)
        {
            if (!ExigirChave()) return;
            var cfg = EmpresaConfigStore.Current;
            var resultado = await FiscalApiClient.ObterDanfseAsync(
                BaseUrlNfse(), cfg.FiscalApiKey, TxtChaveAcesso.Text.Trim());

            if (!resultado.Sucesso)
            {
                MessageBox.Show(
                    $"DANFSE indisponível:\n\n{resultado.ResumoErro()}\n\nEnquanto o SEFIN não gerar PDF, use o XML da NFS-e.",
                    "DANFSE", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "PDF|*.pdf",
                FileName = $"DANFSE_{TxtChaveAcesso.Text.Trim()}.pdf"
            };
            if (dlg.ShowDialog() != true) return;
            await File.WriteAllBytesAsync(dlg.FileName, resultado.Dados!);
            MessageBox.Show($"DANFSE salva em:\n{dlg.FileName}", "DANFSE", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnCancelarNota_Click(object sender, RoutedEventArgs e)
        {
            if (!ExigirChave()) return;
            if (!string.Equals(_nota.Status, "Emitida", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Só é possível cancelar NFS-e com status Emitida.", "Cancelamento",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new CancelamentoNfseWindow(
                $"NFS-e {_nota.NumeroExibicao} — {_nota.TomadorNome}\nChave: {_nota.ChaveAcesso}")
            { Owner = this };
            if (dlg.ShowDialog() != true) return;

            var cfg = EmpresaConfigStore.Current;
            BtnCancelarNota.IsEnabled = false;
            try
            {
                var resultado = await FiscalApiClient.CancelarNfseAsync(
                    BaseUrlNfse(), cfg.FiscalApiKey, _nota.ChaveAcesso, cfg.Cnpj,
                    dlg.CodigoMotivo, dlg.Justificativa);

                if (!resultado.Sucesso)
                {
                    MessageBox.Show($"Falha ao cancelar:\n\n{resultado.ResumoErro()}", "Cancelamento NFS-e",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dados = resultado.Dados!;
                if (dados.Aprovado)
                {
                    _nota.Status = "Cancelada";
                    _nota.XMotivo = "Cancelamento homologado pelo SEFIN";
                    if (!string.IsNullOrWhiteSpace(dados.XmlEnviado))
                        _nota.XmlEnviado = dados.XmlEnviado;
                    SalvarResultado();
                    AtualizarCabecalho();
                    MessageBox.Show(
                        "✅ Cancelamento aprovado pelo SEFIN.\n\nA consulta de status ainda pode não refletir o cancelamento — confie nesta resposta.",
                        "Cancelamento NFS-e", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Cancelamento não aprovado.\n{dados.Erro}", "Cancelamento NFS-e",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                BtnCancelarNota.IsEnabled = true;
            }
        }

        private void SalvarXmlEmDisco(string xml, string sufixo)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "XML|*.xml",
                FileName = $"NFSe_{_nota.ChaveAcesso}_{sufixo}.xml"
            };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, xml);
            MessageBox.Show($"XML salvo em:\n{dlg.FileName}", "XML NFS-e", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => Close();

        private static string Digitos(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : new string(Array.FindAll(s.ToCharArray(), char.IsDigit));
    }
}
