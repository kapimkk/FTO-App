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

            string amb = string.IsNullOrWhiteSpace(_nota.Ambiente) ? "2" : FiscalApiClient.NormalizarTpAmb(_nota.Ambiente);
            _nota.Ambiente = amb;
            SetComboTag(CbAmbiente, amb);
            SetComboTag(CbOpSimpNac, string.IsNullOrWhiteSpace(_nota.OpSimpNac) ? "1" : _nota.OpSimpNac);
            SetComboTag(CbRegApTribSN, string.IsNullOrWhiteSpace(_nota.RegApTribSN) ? "1" : _nota.RegApTribSN);
            AtualizarCabecalho();
            AtualizarPainel();
            LblDica.Text = amb == "1"
                ? "Produção nacional (tpAmb=1): a API deve ter AmbienteRestrito=false e URL sefin.nfse.gov.br/SefinNacional (sem /API)."
                : "Homologação / produção restrita (tpAmb=2): host sefin.producaorestrita…/API/SefinNacional.";
        }

        private void AtualizarCabecalho()
        {
            LblTitulo.Text = $"⚡ Ações NFS-e — {_nota.NumeroExibicao}";
            LblResumo.Text =
                $"{_nota.TomadorNome}\n{_nota.DescricaoServico} — {_nota.ValorFormatado} · Competência {_nota.DataCompetenciaFormatada} · Status: {_nota.Status}\n" +
                $"Regime DPS: opSimpNac={_nota.OpSimpNac} · regApTribSN={_nota.RegApTribSN}";
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
            // Endereço do prestador NÃO vai na DPS com tpEmit=1 (E0128) — não bloqueia emissão.

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
            string ibgeTom = Digitos(_nota.TomadorIbge);
            if (ibgeTom.Length == 7 && string.IsNullOrWhiteSpace(_nota.TomadorLgr))
            {
                MessageBox.Show(
                    "Tomador com IBGE preenchido precisa de logradouro — sem isso o endereço não vai na DPS.",
                    "NFS-e", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void PersistRegimeDps()
        {
            Database.ExecuteNonQuery(@"
                UPDATE notasservico SET opsimpnac=@op, regaptribsn=@ras, ambiente=@amb WHERE id=@nid",
                new Dictionary<string, object>
                {
                    ["@op"] = _nota.OpSimpNac,
                    ["@ras"] = _nota.RegApTribSN,
                    ["@amb"] = _nota.Ambiente,
                    ["@nid"] = _nota.Id
                });
            HouveAlteracao = true;
        }

        private void SalvarResultado()
        {
            Database.ExecuteNonQuery(@"
                UPDATE notasservico SET
                    chaveacesso=@ca, iddps=@id, dataprocessamento=@dp, cstat=@cs, xmotivo=@xm,
                    xmlenviado=@xe, xmlautorizado=@xa, status=@st, ambiente=@amb,
                    opsimpnac=@op, regaptribsn=@ras,
                    codigoibgeemissao=@ibgee, codibgeprestacao=@ibgep
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
                    ["@op"] = _nota.OpSimpNac,
                    ["@ras"] = _nota.RegApTribSN,
                    ["@ibgee"] = _nota.CodigoIbgeEmissao ?? "",
                    ["@ibgep"] = _nota.CodIbgePrestacao ?? "",
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
            _nota.OpSimpNac = GetComboTag(CbOpSimpNac, "1");
            _nota.RegApTribSN = GetComboTag(CbRegApTribSN, "1");
            PersistRegimeDps();
            AtualizarCabecalho();

            if (_nota.Ambiente == "1")
            {
                var confirma = MessageBox.Show(
                    "Emitir em Produção nacional (tpAmb=1)?\n\n" +
                    $"opSimpNac={_nota.OpSimpNac} · regApTribSN={_nota.RegApTribSN}\n" +
                    "(E0160: este valor deve bater com o Simples Nacional do CNPJ na competência.)\n\n" +
                    "Confirme que na VPS a Fiscal.NFSe.API está com:\n" +
                    "• AmbienteRestrito=false\n" +
                    "• BaseUrlSefinNacionalProducao=https://sefin.nfse.gov.br/SefinNacional\n" +
                    "  (sem /API — com /API o SEFIN devolve IIS 404)\n\n" +
                    "Continuar a emissão?",
                    "Ambiente NFS-e",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirma != MessageBoxResult.Yes) return;
            }

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
                _nota.XMotivo = string.IsNullOrWhiteSpace(dados.XMotivo) ? (dados.Erro ?? "") : dados.XMotivo;
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
                    MessageBox.Show(MontarMensagemRejeicao(dados), "Emissão NFS-e",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
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
            // Sem ?tpAmb= a API assume homolog (2) e a consulta da nota emitida em produção vira 404.
            var resultado = await FiscalApiClient.ObterDanfseAsync(
                BaseUrlNfse(), cfg.FiscalApiKey, TxtChaveAcesso.Text.Trim(), AmbienteAtual());

            if (!resultado.Sucesso)
            {
                string extra = "";
                if (resultado.HttpStatus == 404)
                {
                    extra = "\n\nDica 404: confira se o Ambiente SEFIN desta janela é o mesmo da emissão " +
                            "(produção = 1). Sem tpAmb correto a API procura a nota no host errado.";
                }
                else if (resultado.HttpStatus == 503 ||
                         string.Equals(resultado.CodigoErro, "DANFE_NOT_AVAILABLE", StringComparison.OrdinalIgnoreCase))
                {
                    extra = "\n\nO SEFIN ainda não gera PDF do DANFSE neste ambiente — use Baixar/Ver XML.";
                }

                MessageBox.Show(
                    $"DANFSE indisponível:\n\n{resultado.ResumoErro()}{extra}\n\nEnquanto o PDF não estiver disponível, use o XML da NFS-e.",
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
                    dlg.CodigoMotivo, dlg.Justificativa, AmbienteAtual());

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

        /// <summary>Monta mensagem de rejeição sem duplicar o texto de Erro/XMotivo e com dica para E0006.</summary>
        private static string MontarMensagemRejeicao(FiscalNfseEmissaoResponse dados)
        {
            string detalhe = !string.IsNullOrWhiteSpace(dados.XMotivo)
                ? dados.XMotivo!
                : (dados.Erro ?? "Sem detalhe retornado pela API.");

            // Se CStat veio vazio e Erro já está em XMotivo, não concatenar de novo
            if (!string.IsNullOrWhiteSpace(dados.Erro) &&
                !string.Equals(detalhe, dados.Erro, StringComparison.Ordinal) &&
                !detalhe.Contains(dados.Erro, StringComparison.Ordinal))
            {
                detalhe = string.IsNullOrWhiteSpace(dados.CStat)
                    ? $"{detalhe}\n{dados.Erro}"
                    : $"{dados.CStat} - {detalhe}\n{dados.Erro}";
            }
            else if (!string.IsNullOrWhiteSpace(dados.CStat))
            {
                detalhe = $"{dados.CStat} - {detalhe}";
            }

            string msg = $"⚠️ NFS-e NÃO autorizada.\n\n{detalhe}";
            if (detalhe.Contains("E0006", StringComparison.OrdinalIgnoreCase) ||
                detalhe.Contains("Ambiente informado diverge", StringComparison.OrdinalIgnoreCase))
            {
                msg += "\n\nDica E0006: o tpAmb da DPS deve bater com o host SEFIN.\n" +
                       "• Homologação → tpAmb=2 + sefin.producaorestrita…/API/SefinNacional\n" +
                       "• Produção → tpAmb=1 + sefin.nfse.gov.br/SefinNacional (sem /API)";
            }
            else if (detalhe.Contains("E0160", StringComparison.OrdinalIgnoreCase) ||
                     detalhe.Contains("situação perante o Simples Nacional", StringComparison.OrdinalIgnoreCase))
            {
                msg += "\n\nDica E0160: o opSimpNac enviado não bate com o Simples Nacional do CNPJ nesta competência.\n" +
                       "Na própria janela de Ações, troque o combo opSimpNac e emita de novo:\n" +
                       "• Se 3 (ME/EPP) falhou → tente 1 (Não optante) ou 2 (MEI)\n" +
                       "• Se 1 falhou → tente 2 (MEI) ou 3 (ME/EPP)\n" +
                       "Confira no portal do Simples Nacional / Receita e alinhe Configurações → Regime.";
            }
            else if (detalhe.Contains("E0128", StringComparison.OrdinalIgnoreCase) ||
                     detalhe.Contains("endereço nacional do prestador", StringComparison.OrdinalIgnoreCase))
            {
                msg += "\n\nDica E0128: com tpEmit=1 (prestador = emitente) não se informa endereço do prestador. " +
                       "Atualize o FTO — o payload já omite esse bloco.";
            }
            else if (detalhe.Contains("404", StringComparison.OrdinalIgnoreCase) &&
                     (detalhe.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                      detalhe.Contains("File or directory not found", StringComparison.OrdinalIgnoreCase) ||
                      detalhe.Contains("URL provavelmente incorreta", StringComparison.OrdinalIgnoreCase)))
            {
                msg += "\n\nDica 404 IIS: URL de produção errada. Use:\n" +
                       "SefinNacionalSettings__BaseUrlSefinNacionalProducao=https://sefin.nfse.gov.br/SefinNacional\n" +
                       "(remova /API) e rebuild do fiscal-nfse-api.";
            }
            return msg;
        }

        private static string Digitos(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : new string(Array.FindAll(s.ToCharArray(), char.IsDigit));
    }
}
