using FTO_App.Models;
using FTO_App.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FTO_App.Views
{
    public partial class NotaFiscalView : UserControl
    {
        private const int PageSize = 50;
        private const int NatOpMaxLength = 60;
        private long? _editingId;
        private readonly List<ClienteModel> _clientes = new();
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private string _filtro = "";
        private int _page = 1;
        private int _totalPages = 1;
        private bool _buscandoCep;

        public NotaFiscalView()
        {
            InitializeComponent();
            DpEmissao.SelectedDate = DateTime.Today;
            Loaded += (_, _) =>
            {
                LoadClientes();
                AtualizarPainelIcmsPorCrt();
                AtualizarHintHomolog();
                LoadGrid();
            };
        }

        private void AtualizarPainelIcmsPorCrt()
        {
            bool regimeNormal = EmpresaConfigStore.Current.RegimeTributario == "3";
            if (PanelCst != null) PanelCst.Visibility = regimeNormal ? Visibility.Visible : Visibility.Collapsed;
            if (PanelCsosn != null) PanelCsosn.Visibility = regimeNormal ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AtualizarHintHomolog()
        {
            if (LblHomologHint == null || CbAmbiente == null) return;
            string? amb = (CbAmbiente.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            LblHomologHint.Visibility = amb == "2" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CbAmbiente_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            AtualizarHintHomolog();
        }

        private void CbIndIEDest_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || TxtDestIe == null) return;
            string? ind = (CbIndIEDest.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (ind == "2" && string.IsNullOrWhiteSpace(TxtDestIe.Text))
                TxtDestIe.Text = "ISENTO";
            else if (ind == "9")
                TxtDestIe.Text = "";
        }

        private void TxtDestIe_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded || CbIndIEDest == null) return;
            string ie = (TxtDestIe.Text ?? "").Trim();
            if (string.Equals(ie, "ISENTO", StringComparison.OrdinalIgnoreCase))
            {
                SetComboTag(CbIndIEDest, "2");
                return;
            }
            // IE numérica → contribuinte (evita salvar ind=9 com IE preenchida)
            if (ie.Any(char.IsDigit))
                SetComboTag(CbIndIEDest, "1");
        }

        private void TxtProdCfop_TextChanged(object sender, TextChangedEventArgs e) => SugerirIdDest();
        private void TxtDestUf_TextChanged(object sender, TextChangedEventArgs e) => SugerirIdDest();

        private void SugerirIdDest()
        {
            if (CbIdDest == null || !IsLoaded) return;
            string id = NfeXmlService.InferirIdDest(
                TxtProdCfop?.Text,
                EmpresaConfigStore.Current.Uf,
                TxtDestUf?.Text);
            SetComboTag(CbIdDest, id);
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

        /// <summary>Abre o cadastro em branco para nova NF-e (modelo 55).</summary>
        private void AbrirNovo()
        {
            BtnLimpar_Click(this, new RoutedEventArgs());
            AtualizarTituloForm();
            if (BtnExcluirForm != null) BtnExcluirForm.Visibility = Visibility.Collapsed;
            FormOverlay.Visibility = Visibility.Visible;
        }

        private void BtnNovoNfe_Click(object sender, RoutedEventArgs e) => AbrirNovo();

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (GridNotas.SelectedItem is not NotaFiscalModel n)
            {
                MessageBox.Show("Selecione uma nota na lista.", "NF-e", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            CarregarNotaNoForm(n);
            AtualizarTituloForm();
            if (BtnExcluirForm != null) BtnExcluirForm.Visibility = Visibility.Visible;
            FormOverlay.Visibility = Visibility.Visible;
        }

        private void Grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GridNotas.SelectedItem is NotaFiscalModel n)
            {
                CarregarNotaNoForm(n);
                AtualizarTituloForm();
                if (BtnExcluirForm != null) BtnExcluirForm.Visibility = Visibility.Visible;
                FormOverlay.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Abre a janela de emissão/consulta/cancelamento/CC-e/DANFE para a
        /// nota selecionada — o cadastro (esta tela) fica só com o lançamento (Salvar/Excluir).</summary>
        private void BtnAcoesFiscais_Click(object sender, RoutedEventArgs e)
        {
            if (GridNotas.SelectedItem is not NotaFiscalModel sel)
            {
                MessageBox.Show("Selecione uma nota na lista.", "Nota Fiscal", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var nota = CarregarNotaPorId(sel.Id);
            if (nota == null)
            {
                MessageBox.Show("Não foi possível carregar os dados da nota selecionada.", "Nota Fiscal",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var win = new NotaFiscalAcoesWindow(nota) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
            if (win.HouveAlteracao) LoadGrid();
        }

        private void BtnExcluirLista_Click(object sender, RoutedEventArgs e)
        {
            if (GridNotas.SelectedItem is not NotaFiscalModel n)
            {
                MessageBox.Show("Selecione uma nota na lista.", "NF-e", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExcluirNota(n.Id, n.NumeroExibicao, n.DestNome);
        }

        private void BtnExcluirForm_Click(object sender, RoutedEventArgs e)
        {
            if (!_editingId.HasValue) return;
            string num = $"{TxtSerie?.Text}/{TxtNumero?.Text}";
            ExcluirNota(_editingId.Value, num, TxtDestNome?.Text);
        }

        private void ExcluirNota(long id, string numero, string? dest)
        {
            string label = string.IsNullOrWhiteSpace(dest) ? numero : $"{numero} — {dest}";
            if (MessageBox.Show($"Excluir a nota fiscal \"{label}\"?\n\nEsta ação não pode ser desfeita.",
                    "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                Database.ExecuteNonQuery("DELETE FROM NotasFiscais WHERE Id=@id",
                    new Dictionary<string, object> { ["@id"] = id });
                if (_editingId == id)
                {
                    FormOverlay.Visibility = Visibility.Collapsed;
                    BtnLimpar_Click(this, new RoutedEventArgs());
                }
                LoadGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao excluir: {ex.Message}", "NF-e", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFecharForm_Click(object sender, RoutedEventArgs e)
        {
            FormOverlay.Visibility = Visibility.Collapsed;
            BtnLimpar_Click(sender, e);
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e)
        {
            _filtro = TxtBusca.Text.Trim();
            _page = 1;
            LoadGrid();
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filtro = TxtBusca.Text.Trim();
            _page = 1;
            LoadGrid();
        }

        private void Filtro_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            _page = 1;
            LoadGrid();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_page > 1) { _page--; LoadGrid(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_page < _totalPages) { _page++; LoadGrid(); }
        }

        private void LoadClientes()
        {
            _clientes.Clear();
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = Database.Cmd(conn, "SELECT * FROM Clientes ORDER BY Nome");
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    _clientes.Add(new ClienteModel
                    {
                        Id = Convert.ToInt64(Database.FieldOrDbNull(r, "Id")),
                        Nome = Database.FieldOrDbNull(r, "Nome")?.ToString() ?? "",
                        CpfCnpj = Col(r, "Cpf_Cnpj"),
                        Ie = Col(r, "Ie"),
                        Email = Col(r, "Email"),
                        Logradouro = Col(r, "Logradouro"),
                        Numero = Col(r, "Numero"),
                        Complemento = Col(r, "Complemento"),
                        Bairro = Col(r, "Bairro"),
                        Municipio = Col(r, "Municipio"),
                        Uf = Col(r, "Uf"),
                        Cep = Col(r, "Cep"),
                        CodigoIbge = Col(r, "CodigoIbge")
                    });
                }
            }
            catch { }

            CbCliente.ItemsSource = null;
            CbCliente.ItemsSource = _clientes;
        }

        private void SugerirProximoNumero()
        {
            try
            {
                long ultimo = 0;
                if (long.TryParse(EmpresaConfigStore.Current.UltimoNumeroNfe, out long cfg))
                    ultimo = cfg;

                using var conn = Database.GetConnection();
                using var cmd = Database.Cmd(conn, "SELECT MAX(Numero) FROM NotasFiscais");
                var scalar = cmd.ExecuteScalar();
                if (scalar != null && scalar != DBNull.Value)
                    ultimo = Math.Max(ultimo, Convert.ToInt64(scalar));

                TxtNumero.Text = (ultimo + 1).ToString();
                TxtSerie.Text = string.IsNullOrWhiteSpace(EmpresaConfigStore.Current.SerieNfe)
                    ? "1" : EmpresaConfigStore.Current.SerieNfe;
                CbAmbiente.SelectedIndex = EmpresaConfigStore.Current.AmbienteNfe == "1" ? 0 : 1;
            }
            catch
            {
                TxtNumero.Text = "1";
            }
        }

        private void CbCliente_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbCliente.SelectedItem is not ClienteModel c) return;
            TxtDestNome.Text = c.Nome;
            TxtDestDoc.Text = c.CpfCnpj;
            TxtDestIe.Text = c.Ie;
            TxtDestEmail.Text = c.Email;
            TxtDestLgr.Text = c.Logradouro;
            TxtDestNro.Text = c.Numero;
            TxtDestBairro.Text = c.Bairro;
            TxtDestMun.Text = c.Municipio;
            TxtDestUf.Text = c.Uf;
            TxtDestCep.Text = c.Cep;
            TxtDestIbge.Text = c.CodigoIbge;
            if (!string.IsNullOrWhiteSpace(c.Ie))
            {
                if (string.Equals(c.Ie.Trim(), "ISENTO", StringComparison.OrdinalIgnoreCase))
                    SetComboTag(CbIndIEDest, "2");
                else
                    SetComboTag(CbIndIEDest, "1");
            }
            else
            {
                SetComboTag(CbIndIEDest, "9");
            }
            SugerirIdDest();
        }

        private async void TxtDestCep_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || _buscandoCep) return;
            e.Handled = true;
            _buscandoCep = true;
            try
            {
                var result = await CepService.BuscarAsync(TxtDestCep.Text);
                if (!result.Success)
                {
                    MessageBox.Show(result.ErrorMessage ?? "CEP não encontrado.", "CEP",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                TxtDestCep.Text = result.Cep;
                if (!string.IsNullOrWhiteSpace(result.Logradouro)) TxtDestLgr.Text = result.Logradouro;
                if (!string.IsNullOrWhiteSpace(result.Bairro)) TxtDestBairro.Text = result.Bairro;
                if (!string.IsNullOrWhiteSpace(result.Municipio)) TxtDestMun.Text = result.Municipio;
                if (!string.IsNullOrWhiteSpace(result.Uf)) TxtDestUf.Text = result.Uf;
                if (!string.IsNullOrWhiteSpace(result.CodigoIbge)) TxtDestIbge.Text = result.CodigoIbge;
                TxtDestNro.Focus();
            }
            finally
            {
                _buscandoCep = false;
            }
        }

        // -----------------------------------------------------------------
        // Autocomplete de NCM (BrasilAPI) — debounce de ~350ms, mínimo 3
        // caracteres; falha de rede não bloqueia (usuário digita manualmente).
        // -----------------------------------------------------------------

        private CancellationTokenSource? _ncmCts;
        private bool _suprimirBuscaNcm;

        private async void TxtProdNcm_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suprimirBuscaNcm || !IsLoaded) return;

            _ncmCts?.Cancel();
            var cts = new CancellationTokenSource();
            _ncmCts = cts;
            string termo = TxtProdNcm.Text;

            try
            {
                await Task.Delay(350, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            if (cts.IsCancellationRequested) return;

            var sugestoes = await NcmService.BuscarAsync(termo);
            if (cts.IsCancellationRequested) return;

            if (sugestoes.Count == 0)
            {
                PopupNcm.IsOpen = false;
                return;
            }
            ListNcmSugestoes.ItemsSource = sugestoes;
            PopupNcm.IsOpen = true;
        }

        private void ListNcmSugestoes_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ListNcmSugestoes.SelectedItem is NcmResult ncm)
            {
                _suprimirBuscaNcm = true;
                // Guarda só os dígitos (BrasilAPI devolve com pontos: "2203.00.00")
                TxtProdNcm.Text = ReformaTributariaService.NormalizarNcm(ncm.Codigo);
                TxtProdNcm.CaretIndex = TxtProdNcm.Text.Length;
                _suprimirBuscaNcm = false;
            }
            PopupNcm.IsOpen = false;
        }

        private void CalcTotais(object sender, TextChangedEventArgs e) => RecalcTotais();
        private void ChkAutoIbsCbs_Click(object sender, RoutedEventArgs e) => RecalcTotais();

        private void RecalcTotais()
        {
            decimal qtd = ParseDec(TxtProdQtd?.Text);
            decimal unit = ParseDec(TxtProdVUnit?.Text);
            decimal total = qtd * unit;
            if (TxtProdVTot != null) TxtProdVTot.Text = total.ToString("N2", PtBr);
            if (TxtTotalNf != null) TxtTotalNf.Text = total.ToString("C2", PtBr);

            if (TxtCbsValor == null) return;

            var cfg = EmpresaConfigStore.Current;
            if (LblNfIbsCbsInfo != null)
                LblNfIbsCbsInfo.Text = ReformaTributariaService.DescricaoPreset(cfg.IbsCbsPreset);

            bool auto = ChkAutoIbsCbs?.IsChecked != false && cfg.IbsCbsCalculoAutomatico;
            if (auto)
            {
                var r = ReformaTributariaService.Calcular(total, cfg);
                TxtCbsAliqNf.Text = r.AliquotaCbs.ToString("0.####", PtBr);
                TxtIbsAliqNf.Text = r.AliquotaIbs.ToString("0.####", PtBr);
                TxtCbsValor.Text = r.ValorCbs.ToString("N2", PtBr);
                TxtIbsValor.Text = r.ValorIbs.ToString("N2", PtBr);
                TxtIbsUfValor.Text = r.ValorIbsUf.ToString("N2", PtBr);
                TxtIbsMunValor.Text = r.ValorIbsMun.ToString("N2", PtBr);
                if (string.IsNullOrWhiteSpace(TxtCstIbsCbs.Text)) TxtCstIbsCbs.Text = r.Cst;
                if (string.IsNullOrWhiteSpace(TxtClassTrib.Text)) TxtClassTrib.Text = r.ClassTrib;
            }
            else
            {
                decimal cbsA = ParseDec(TxtCbsAliqNf?.Text);
                decimal ibsA = ParseDec(TxtIbsAliqNf?.Text);
                var (_, _, ufA, munA) = ReformaTributariaService.AliquotasDoPreset(cfg.IbsCbsPreset, cfg);
                // Mantém rateio UF/Mun do preset quando o usuário só altera o total IBS
                decimal fatorUf = ibsA > 0 && (ufA + munA) > 0 ? ufA / (ufA + munA) : 0.5m;
                TxtCbsValor.Text = Math.Round(total * cbsA / 100m, 2).ToString("N2", PtBr);
                decimal ibsV = Math.Round(total * ibsA / 100m, 2);
                TxtIbsValor.Text = ibsV.ToString("N2", PtBr);
                decimal ufV = Math.Round(ibsV * fatorUf, 2);
                TxtIbsUfValor.Text = ufV.ToString("N2", PtBr);
                TxtIbsMunValor.Text = (ibsV - ufV).ToString("N2", PtBr);
            }
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var nota = MontarNota();
                SalvarNoBanco(nota);
                MessageBox.Show("Nota salva!", "NF-e", MessageBoxButton.OK, MessageBoxImage.Information);
                FormOverlay.Visibility = Visibility.Collapsed;
                BtnLimpar_Click(sender, e);
                LoadGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}", "NF-e", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Insere/atualiza a nota no banco (sem fechar o formulário) — reaproveitado antes de emitir na API.</summary>
        private void SalvarNoBanco(NotaFiscalModel nota)
        {
            var p = Parametros(nota);

            if (_editingId.HasValue)
            {
                p["@id"] = _editingId.Value;
                Database.ExecuteNonQuery(@"UPDATE NotasFiscais SET NaturezaOperacao=@nat, Modelo=@mod, Serie=@ser, Numero=@num,
                    DataEmissao=@dem, TipoOperacao=@top, Finalidade=@fin, ConsumidorFinal=@cf, PresencaComprador=@pres, Ambiente=@amb,
                    ClienteId=@cid, DestNome=@dn, DestCpfCnpj=@dd, DestIe=@die, DestEmail=@demail, DestLogradouro=@dl,
                    DestNumero=@dnr, DestComplemento=@dcm, DestBairro=@dba, DestMunicipio=@dmu, DestUf=@duf, DestCep=@dce,
                    DestCodigoIbge=@dib, ProdutoCodigo=@pcod, ProdutoDescricao=@pd, ProdutoNcm=@pn, ProdutoCfop=@pf,
                    ProdutoUnidade=@pu, ProdutoQuantidade=@pq, ProdutoValorUnitario=@pvu, ProdutoValorTotal=@pvt,
                    IcmsOrigem=@io, IcmsCst=@ic, IcmsAliquota=@ia, IcmsValor=@iv, PisCst=@psc, PisAliquota=@psa, PisValor=@psv,
                    CofinsCst=@csc, CofinsAliquota=@csa, CofinsValor=@csv, ValorProdutos=@vp, ValorFrete=@vf, ValorDesconto=@vd,
                    ValorTotalNota=@vn, FormaPagamento=@fp, InformacoesComplementares=@inf, Status=@st, CaminhoXml=@xml
                    WHERE Id=@id", p);
            }
            else
            {
                _editingId = Database.ExecuteInsertReturnId(@"INSERT INTO NotasFiscais
                    (NaturezaOperacao,Modelo,Serie,Numero,DataEmissao,TipoOperacao,Finalidade,ConsumidorFinal,PresencaComprador,Ambiente,
                     ClienteId,DestNome,DestCpfCnpj,DestIe,DestEmail,DestLogradouro,DestNumero,DestComplemento,DestBairro,DestMunicipio,DestUf,DestCep,DestCodigoIbge,
                     ProdutoCodigo,ProdutoDescricao,ProdutoNcm,ProdutoCfop,ProdutoUnidade,ProdutoQuantidade,ProdutoValorUnitario,ProdutoValorTotal,
                     IcmsOrigem,IcmsCst,IcmsAliquota,IcmsValor,PisCst,PisAliquota,PisValor,CofinsCst,CofinsAliquota,CofinsValor,
                     ValorProdutos,ValorFrete,ValorDesconto,ValorTotalNota,FormaPagamento,InformacoesComplementares,Status,CaminhoXml)
                    VALUES (@nat,@mod,@ser,@num,@dem,@top,@fin,@cf,@pres,@amb,@cid,@dn,@dd,@die,@demail,@dl,@dnr,@dcm,@dba,@dmu,@duf,@dce,@dib,
                     @pcod,@pd,@pn,@pf,@pu,@pq,@pvu,@pvt,@io,@ic,@ia,@iv,@psc,@psa,@psv,@csc,@csa,@csv,@vp,@vf,@vd,@vn,@fp,@inf,@st,@xml)", p);
            }

            AtualizarUltimoNumero(nota.Numero);
            if (_editingId.HasValue)
            {
                nota.Id = _editingId.Value;
                SalvarCamposExtras(_editingId.Value, nota);
            }
        }

        // -----------------------------------------------------------------
        // Inutilização de numeração — única ação da API Fiscal que continua
        // aqui (não é sobre uma nota específica, e sim sobre uma faixa de
        // números nunca emitidos; todas as demais ações vivem em
        // NotaFiscalAcoesWindow, aberta pelo botão "⚡ Ações fiscais").
        // -----------------------------------------------------------------

        /// <summary>Título do modal reflete se é lançamento novo ou edição.</summary>
        private void AtualizarTituloForm()
        {
            if (LblFormTitulo == null) return;
            LblFormTitulo.Text = _editingId.HasValue ? "Editar NF-e" : "Cadastrar NF-e";
        }

        private string NaturezaOperacaoAtual()
        {
            string nat = (CbNatOp?.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nat))
                nat = "Venda de mercadoria";
            if (nat.Length > NatOpMaxLength)
                nat = nat[..NatOpMaxLength];
            return nat;
        }

        private async void BtnInutilizar_Click(object sender, RoutedEventArgs e)
        {
            var cfg = EmpresaConfigStore.Current;
            if (string.IsNullOrWhiteSpace(cfg.Cnpj) || string.IsNullOrWhiteSpace(cfg.Uf))
            {
                MessageBox.Show("Complete CNPJ e UF da empresa em Configurações antes de inutilizar numeração.",
                    "Inutilização", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var win = new InutilizacaoWindow(cfg.SerieNfe) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            string tpAmb = FiscalApiClient.NormalizarTpAmb(cfg.AmbienteNfe);
            var resultado = await FiscalApiClient.InutilizarAsync(
                cfg.FiscalApiUrlNfe, cfg.FiscalApiKey, cfg.Cnpj, FiscalPayloadBuilder.UfToCodigo(cfg.Uf),
                win.Ano, win.Serie, win.NumeroInicial, win.NumeroFinal, win.Justificativa, tpAmb);

            if (!resultado.Sucesso)
            {
                MessageBox.Show($"Falha ao inutilizar a faixa:\n\n{resultado.ResumoErro()}", "Inutilização",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dados = resultado.Dados!;
            MessageBox.Show(dados.Aprovado
                    ? $"✅ Faixa {win.NumeroInicial}-{win.NumeroFinal} (série {win.Serie}) inutilizada com sucesso!\n\nProtocolo: {dados.NProt}\n{dados.CStat} - {dados.XMotivo}"
                    : $"⚠️ Inutilização não foi aceita pela SEFAZ.\n\n{dados.CStat} - {(dados.MensagemTraduzida ?? dados.XMotivo)}",
                "Inutilização", MessageBoxButton.OK, dados.Aprovado ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void BtnLimpar_Click(object sender, RoutedEventArgs e)
        {
            _editingId = null;
            CbCliente.SelectedItem = null;
            TxtDestNome.Text = TxtDestDoc.Text = TxtDestIe.Text = TxtDestEmail.Text = "";
            TxtDestLgr.Text = TxtDestNro.Text = TxtDestBairro.Text = TxtDestMun.Text = "";
            TxtDestUf.Text = TxtDestCep.Text = TxtDestIbge.Text = "";
            _suprimirBuscaNcm = true;
            TxtProdDesc.Text = TxtProdNcm.Text = TxtProdCest.Text = "";
            _suprimirBuscaNcm = false;
            TxtProdGtin.Text = "SEM GTIN";
            TxtProdCod.Text = "001";
            TxtProdCfop.Text = "5102";
            TxtProdUn.Text = "UN";
            TxtProdQtd.Text = "1";
            TxtProdVUnit.Text = "";
            TxtIcmsCst.Text = "00";
            CbCsosn.Text = "102";
            SetComboTag(CbIcmsOrigem, "0");
            SetComboTag(CbIndIEDest, "9");
            SetComboTag(CbIdDest, "1");
            SetComboTag(CbIndFinal, "1");
            SetComboTag(CbIndPres, "1");
            TxtIcmsAliq.Text = TxtPisAliq.Text = TxtCofinsAliq.Text = "0";
            TxtPisCst.Text = TxtCofinsCst.Text = "01";
            TxtInfCpl.Text = "";
            CbNatOp.Text = "Venda de mercadoria";
            TxtCstIbsCbs.Text = ReformaTributariaService.CstPadrao;
            TxtClassTrib.Text = ReformaTributariaService.ClassTribPadrao;
            ChkAutoIbsCbs.IsChecked = EmpresaConfigStore.Current.IbsCbsCalculoAutomatico;
            if (BtnExcluirForm != null) BtnExcluirForm.Visibility = Visibility.Collapsed;
            AtualizarPainelIcmsPorCrt();
            AtualizarHintHomolog();
            AtualizarTituloForm();
            SugerirProximoNumero();
            RecalcTotais();
        }

        private void BtnProdutoEstoque_Click(object sender, RoutedEventArgs e)
        {
            var win = new ProdutoEstoquePickerWindow { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true || win.ProdutoSelecionado is null) return;
            AplicarProdutoDoEstoque(win.ProdutoSelecionado);
        }

        /// <summary>
        /// Preenche os campos do item com o cadastro do estoque, após validar dados fiscais mínimos.
        /// </summary>
        private void AplicarProdutoDoEstoque(ProdutoModel p)
        {
            var faltando = new List<string>();
            if (string.IsNullOrWhiteSpace(p.Nome) && string.IsNullOrWhiteSpace(p.Descricao))
                faltando.Add("nome/descrição");
            if (string.IsNullOrWhiteSpace(p.Ncm) || DocumentValidator.OnlyDigits(p.Ncm).Length != 8)
                faltando.Add("NCM (8 dígitos)");
            if (string.IsNullOrWhiteSpace(p.Cfop) || DocumentValidator.OnlyDigits(p.Cfop).Length != 4)
                faltando.Add("CFOP (4 dígitos)");
            if (p.PrecoVenda <= 0)
                faltando.Add("preço de venda");
            if (p.Quantidade <= 0)
                faltando.Add("quantidade em estoque");

            bool regimeNormal = EmpresaConfigStore.Current.RegimeTributario == "3";
            if (regimeNormal)
            {
                if (string.IsNullOrWhiteSpace(p.CstIcms))
                    faltando.Add("CST ICMS");
            }
            else if (string.IsNullOrWhiteSpace(p.Csosn))
            {
                faltando.Add("CSOSN");
            }

            if (faltando.Count > 0)
            {
                MessageBox.Show(
                    "O produto do estoque está incompleto para emitir nota. Complete no módulo Estoque:\n\n• " +
                    string.Join("\n• ", faltando),
                    "Produto incompleto", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.CodigoBarras))
                TxtProdCod.Text = p.CodigoBarras.Trim();
            else if (p.Id > 0)
                TxtProdCod.Text = p.Id.ToString(PtBr);

            string desc = !string.IsNullOrWhiteSpace(p.Descricao) ? p.Descricao.Trim() : p.Nome.Trim();
            _suprimirBuscaNcm = true;
            TxtProdDesc.Text = desc;
            TxtProdNcm.Text = DocumentValidator.OnlyDigits(p.Ncm);
            _suprimirBuscaNcm = false;

            TxtProdCest.Text = (p.Cest ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(p.CodigoBarras) && p.CodigoBarras.Trim().Length is >= 8 and <= 14)
                TxtProdGtin.Text = p.CodigoBarras.Trim();
            else
                TxtProdGtin.Text = "SEM GTIN";

            TxtProdCfop.Text = DocumentValidator.OnlyDigits(p.Cfop);
            TxtProdUn.Text = string.IsNullOrWhiteSpace(p.Unidade) ? "UN" : p.Unidade.Trim();
            TxtProdQtd.Text = "1";
            TxtProdVUnit.Text = p.PrecoVenda.ToString("N4", PtBr);

            SetComboTag(CbIcmsOrigem, string.IsNullOrWhiteSpace(p.Origem) ? "0" : p.Origem.Trim());
            if (!string.IsNullOrWhiteSpace(p.CstIcms)) TxtIcmsCst.Text = p.CstIcms.Trim();
            if (!string.IsNullOrWhiteSpace(p.Csosn)) CbCsosn.Text = p.Csosn.Trim();
            TxtIcmsAliq.Text = p.IcmsAliquota.ToString("N2", PtBr);

            if (!string.IsNullOrWhiteSpace(p.PisCst)) TxtPisCst.Text = p.PisCst.Trim();
            TxtPisAliq.Text = p.PisAliquota.ToString("N2", PtBr);
            if (!string.IsNullOrWhiteSpace(p.CofinsCst)) TxtCofinsCst.Text = p.CofinsCst.Trim();
            TxtCofinsAliq.Text = p.CofinsAliquota.ToString("N2", PtBr);

            if (!string.IsNullOrWhiteSpace(p.CstIbsCbs)) TxtCstIbsCbs.Text = p.CstIbsCbs.Trim();
            if (!string.IsNullOrWhiteSpace(p.ClassTrib)) TxtClassTrib.Text = p.ClassTrib.Trim();
            if (p.CbsAliquota > 0) TxtCbsAliqNf.Text = p.CbsAliquota.ToString("N4", PtBr);
            if (p.IbsAliquota > 0) TxtIbsAliqNf.Text = p.IbsAliquota.ToString("N4", PtBr);

            if (!string.IsNullOrWhiteSpace(p.InfAdicionais) && string.IsNullOrWhiteSpace(TxtInfCpl.Text))
                TxtInfCpl.Text = p.InfAdicionais.Trim();

            SugerirIdDest();
            RecalcTotais();
            MessageBox.Show($"Produto \"{p.Nome}\" aplicado aos campos da nota.", "Estoque",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SalvarCamposExtras(long id, NotaFiscalModel n)
        {
            Database.ExecuteNonQuery(@"UPDATE NotasFiscais SET CstIbsCbs=@cst, ClassTrib=@ct,
                CbsAliquota=@ca, CbsValor=@cv, IbsAliquota=@ia, IbsValor=@iv,
                IbsAliquotaUf=@iau, IbsValorUf=@ivu, IbsAliquotaMun=@iam, IbsValorMun=@ivm,
                IdDest=@idd, IndIEDest=@iie, Csosn=@csosn, ProdutoCest=@cest, ProdutoGtin=@gtin,
                IcmsOrigem=@io, IcmsCst=@icst, PisCst=@psc, CofinsCst=@csc
                WHERE Id=@id",
                new Dictionary<string, object>
                {
                    ["@cst"] = n.CstIbsCbs, ["@ct"] = n.ClassTrib,
                    ["@ca"] = n.CbsAliquota, ["@cv"] = n.CbsValor,
                    ["@ia"] = n.IbsAliquota, ["@iv"] = n.IbsValor,
                    ["@iau"] = n.IbsAliquotaUf, ["@ivu"] = n.IbsValorUf,
                    ["@iam"] = n.IbsAliquotaMun, ["@ivm"] = n.IbsValorMun,
                    ["@idd"] = n.IdDest, ["@iie"] = n.IndIEDest,
                    ["@csosn"] = n.Csosn, ["@cest"] = n.ProdutoCest, ["@gtin"] = n.ProdutoGtin,
                    ["@io"] = n.IcmsOrigem, ["@icst"] = n.IcmsCst,
                    ["@psc"] = n.PisCst, ["@csc"] = n.CofinsCst,
                    ["@id"] = id
                });
        }

        private void CarregarNotaNoForm(NotaFiscalModel n)
        {
            _editingId = n.Id;
            CbNatOp.Text = string.IsNullOrWhiteSpace(n.NaturezaOperacao) ? "Venda de mercadoria" : n.NaturezaOperacao;
            TxtSerie.Text = n.Serie;
            TxtNumero.Text = n.Numero.ToString();
            DpEmissao.SelectedDate = n.DataEmissao;
            SetComboTag(CbAmbiente, n.Ambiente);
            SetComboTag(CbTipoOp, n.TipoOperacao);
            SetComboTag(CbFinalidade, n.Finalidade);
            SetComboTag(CbIdDest, string.IsNullOrWhiteSpace(n.IdDest) ? "1" : n.IdDest);
            SetComboTag(CbIndFinal, n.ConsumidorFinal);
            SetComboTag(CbIndPres, n.PresencaComprador);
            SetComboTag(CbIndIEDest, string.IsNullOrWhiteSpace(n.IndIEDest) ? "9" : n.IndIEDest);
            TxtDestNome.Text = n.DestNome;
            TxtDestDoc.Text = n.DestCpfCnpj;
            TxtDestIe.Text = n.DestIe;
            TxtDestEmail.Text = n.DestEmail;
            TxtDestLgr.Text = n.DestLogradouro;
            TxtDestNro.Text = n.DestNumero;
            TxtDestBairro.Text = n.DestBairro;
            TxtDestMun.Text = n.DestMunicipio;
            TxtDestUf.Text = n.DestUf;
            TxtDestCep.Text = n.DestCep;
            TxtDestIbge.Text = n.DestCodigoIbge;
            TxtProdCod.Text = n.ProdutoCodigo;
            TxtProdDesc.Text = n.ProdutoDescricao;
            _suprimirBuscaNcm = true;
            TxtProdNcm.Text = n.ProdutoNcm;
            _suprimirBuscaNcm = false;
            TxtProdCest.Text = n.ProdutoCest;
            TxtProdGtin.Text = string.IsNullOrWhiteSpace(n.ProdutoGtin) ? "SEM GTIN" : n.ProdutoGtin;
            TxtProdCfop.Text = n.ProdutoCfop;
            TxtProdUn.Text = n.ProdutoUnidade;
            TxtProdQtd.Text = n.ProdutoQuantidade.ToString(PtBr);
            TxtProdVUnit.Text = n.ProdutoValorUnitario.ToString("N2", PtBr);
            SetComboTag(CbIcmsOrigem, n.IcmsOrigem);
            TxtIcmsCst.Text = string.IsNullOrWhiteSpace(n.IcmsCst) ? "00" : n.IcmsCst;
            CbCsosn.Text = string.IsNullOrWhiteSpace(n.Csosn) ? "102" : n.Csosn;
            TxtIcmsAliq.Text = n.IcmsAliquota.ToString(PtBr);
            TxtPisCst.Text = string.IsNullOrWhiteSpace(n.PisCst) ? "01" : n.PisCst;
            TxtPisAliq.Text = n.PisAliquota.ToString(PtBr);
            TxtCofinsCst.Text = string.IsNullOrWhiteSpace(n.CofinsCst) ? "01" : n.CofinsCst;
            TxtCofinsAliq.Text = n.CofinsAliquota.ToString(PtBr);
            TxtInfCpl.Text = n.InformacoesComplementares;
            TxtCstIbsCbs.Text = string.IsNullOrWhiteSpace(n.CstIbsCbs) ? "000" : n.CstIbsCbs;
            TxtClassTrib.Text = string.IsNullOrWhiteSpace(n.ClassTrib) ? "000001" : n.ClassTrib;
            TxtCbsAliqNf.Text = n.CbsAliquota.ToString("0.####", PtBr);
            TxtIbsAliqNf.Text = n.IbsAliquota.ToString("0.####", PtBr);
            ChkAutoIbsCbs.IsChecked = false;
            TxtCbsValor.Text = n.CbsValor.ToString("N2", PtBr);
            TxtIbsValor.Text = n.IbsValor.ToString("N2", PtBr);
            TxtIbsUfValor.Text = n.IbsValorUf.ToString("N2", PtBr);
            TxtIbsMunValor.Text = n.IbsValorMun.ToString("N2", PtBr);
            AtualizarPainelIcmsPorCrt();
            AtualizarHintHomolog();
            RecalcTotais();
        }

        private NotaFiscalModel MontarNota()
        {
            decimal qtd = ParseDec(TxtProdQtd.Text);
            decimal unit = ParseDec(TxtProdVUnit.Text);
            decimal total = qtd * unit;
            decimal icmsA = ParseDec(TxtIcmsAliq.Text);
            decimal pisA = ParseDec(TxtPisAliq.Text);
            decimal cofA = ParseDec(TxtCofinsAliq.Text);

            long.TryParse(TxtNumero.Text, out long numero);
            var cliente = CbCliente.SelectedItem as ClienteModel;
            var cfg = EmpresaConfigStore.Current;

            RecalcTotais();
            decimal cbsA = ParseDec(TxtCbsAliqNf.Text);
            decimal ibsA = ParseDec(TxtIbsAliqNf.Text);
            decimal ibsUfV = ParseDec(TxtIbsUfValor.Text);
            decimal ibsMunV = ParseDec(TxtIbsMunValor.Text);
            decimal ibsUfA = total > 0 ? Math.Round(ibsUfV * 100m / total, 4) : 0;
            decimal ibsMunA = total > 0 ? Math.Round(ibsMunV * 100m / total, 4) : 0;
            if (ChkAutoIbsCbs?.IsChecked != false && cfg.IbsCbsCalculoAutomatico)
            {
                var r = ReformaTributariaService.Calcular(total, cfg);
                ibsUfA = r.AliquotaIbsUf;
                ibsMunA = r.AliquotaIbsMun;
            }

            string idDest = GetComboTag(CbIdDest, NfeXmlService.InferirIdDest(
                TxtProdCfop.Text, cfg.Uf, TxtDestUf.Text));

            var (indIe, ieDest) = NfeXmlService.ConciliarIndIeDest(
                GetComboTag(CbIndIEDest, "9"), TxtDestIe.Text);

            return new NotaFiscalModel
            {
                NaturezaOperacao = NaturezaOperacaoAtual(),
                Modelo = "55",
                Serie = TxtSerie.Text.Trim(),
                Numero = numero,
                DataEmissao = DpEmissao.SelectedDate ?? DateTime.Now,
                TipoOperacao = GetComboTag(CbTipoOp, "1"),
                Finalidade = GetComboTag(CbFinalidade, "1"),
                ConsumidorFinal = GetComboTag(CbIndFinal, "1"),
                PresencaComprador = GetComboTag(CbIndPres, "1"),
                Ambiente = GetComboTag(CbAmbiente, "2"),
                IdDest = idDest,
                ClienteId = cliente?.Id,
                DestNome = TxtDestNome.Text.Trim(),
                DestCpfCnpj = TxtDestDoc.Text.Trim(),
                DestIe = ieDest,
                IndIEDest = indIe,
                DestEmail = TxtDestEmail.Text.Trim(),
                DestLogradouro = TxtDestLgr.Text.Trim(),
                DestNumero = TxtDestNro.Text.Trim(),
                DestBairro = TxtDestBairro.Text.Trim(),
                DestMunicipio = TxtDestMun.Text.Trim(),
                DestUf = TxtDestUf.Text.Trim().ToUpperInvariant(),
                DestCep = TxtDestCep.Text.Trim(),
                DestCodigoIbge = TxtDestIbge.Text.Trim(),
                ProdutoCodigo = TxtProdCod.Text.Trim(),
                ProdutoDescricao = TxtProdDesc.Text.Trim(),
                ProdutoNcm = ReformaTributariaService.NormalizarNcm(TxtProdNcm.Text),
                ProdutoCest = TxtProdCest.Text.Trim(),
                ProdutoGtin = string.IsNullOrWhiteSpace(TxtProdGtin.Text) ? "SEM GTIN" : TxtProdGtin.Text.Trim(),
                ProdutoCfop = TxtProdCfop.Text.Trim(),
                ProdutoUnidade = TxtProdUn.Text.Trim(),
                ProdutoQuantidade = qtd,
                ProdutoValorUnitario = unit,
                ProdutoValorTotal = total,
                IcmsOrigem = GetComboTag(CbIcmsOrigem, "0"),
                IcmsCst = string.IsNullOrWhiteSpace(TxtIcmsCst.Text) ? "00" : TxtIcmsCst.Text.Trim(),
                Csosn = string.IsNullOrWhiteSpace(CbCsosn.Text) ? "102" : CbCsosn.Text.Trim(),
                IcmsAliquota = icmsA,
                IcmsValor = Math.Round(total * icmsA / 100m, 2),
                PisCst = string.IsNullOrWhiteSpace(TxtPisCst.Text) ? "01" : TxtPisCst.Text.Trim(),
                PisAliquota = pisA,
                PisValor = Math.Round(total * pisA / 100m, 2),
                CofinsCst = string.IsNullOrWhiteSpace(TxtCofinsCst.Text) ? "01" : TxtCofinsCst.Text.Trim(),
                CofinsAliquota = cofA,
                CofinsValor = Math.Round(total * cofA / 100m, 2),
                CstIbsCbs = ReformaTributariaService.NormalizarCst(TxtCstIbsCbs.Text),
                ClassTrib = ReformaTributariaService.NormalizarClassTrib(TxtClassTrib.Text),
                CbsAliquota = cbsA,
                CbsValor = ParseDec(TxtCbsValor.Text),
                IbsAliquota = ibsA,
                IbsValor = ParseDec(TxtIbsValor.Text),
                IbsAliquotaUf = ibsUfA,
                IbsValorUf = ibsUfV,
                IbsAliquotaMun = ibsMunA,
                IbsValorMun = ibsMunV,
                ValorProdutos = total,
                ValorTotalNota = total,
                FormaPagamento = GetComboTag(CbFormaPag, "01"),
                InformacoesComplementares = TxtInfCpl.Text.Trim(),
                Status = "Rascunho"
            };
        }

        private Dictionary<string, object> Parametros(NotaFiscalModel n) => new()
        {
            ["@nat"] = n.NaturezaOperacao, ["@mod"] = n.Modelo, ["@ser"] = n.Serie, ["@num"] = n.Numero,
            ["@dem"] = n.DataEmissao.ToString("yyyy-MM-dd HH:mm:ss"),
            ["@top"] = n.TipoOperacao, ["@fin"] = n.Finalidade, ["@cf"] = n.ConsumidorFinal,
            ["@pres"] = n.PresencaComprador, ["@amb"] = n.Ambiente,
            ["@cid"] = n.ClienteId.HasValue ? n.ClienteId.Value : DBNull.Value,
            ["@dn"] = n.DestNome, ["@dd"] = n.DestCpfCnpj, ["@die"] = n.DestIe, ["@demail"] = n.DestEmail,
            ["@dl"] = n.DestLogradouro, ["@dnr"] = n.DestNumero, ["@dcm"] = n.DestComplemento,
            ["@dba"] = n.DestBairro, ["@dmu"] = n.DestMunicipio, ["@duf"] = n.DestUf,
            ["@dce"] = n.DestCep, ["@dib"] = n.DestCodigoIbge,
            ["@pcod"] = n.ProdutoCodigo, ["@pd"] = n.ProdutoDescricao, ["@pn"] = n.ProdutoNcm,
            ["@pf"] = n.ProdutoCfop, ["@pu"] = n.ProdutoUnidade, ["@pq"] = n.ProdutoQuantidade,
            ["@pvu"] = n.ProdutoValorUnitario, ["@pvt"] = n.ProdutoValorTotal,
            ["@io"] = n.IcmsOrigem, ["@ic"] = n.IcmsCst, ["@ia"] = n.IcmsAliquota, ["@iv"] = n.IcmsValor,
            ["@psc"] = n.PisCst, ["@psa"] = n.PisAliquota, ["@psv"] = n.PisValor,
            ["@csc"] = n.CofinsCst, ["@csa"] = n.CofinsAliquota, ["@csv"] = n.CofinsValor,
            ["@vp"] = n.ValorProdutos, ["@vf"] = n.ValorFrete, ["@vd"] = n.ValorDesconto, ["@vn"] = n.ValorTotalNota,
            ["@fp"] = n.FormaPagamento, ["@inf"] = n.InformacoesComplementares,
            ["@st"] = n.Status, ["@xml"] = (object?)n.CaminhoXml ?? DBNull.Value
        };

        private void LoadGrid()
        {
            var list = new List<NotaFiscalModel>();
            string where = "WHERE 1=1";
            if (!string.IsNullOrEmpty(_filtro))
                where += " AND (DestNome LIKE @q OR CAST(Numero AS TEXT) LIKE @q OR Status LIKE @q)";

            string? statusTag = (CbFiltroStatus?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(statusTag))
                where += " AND Status = @st";

            try
            {
                using var conn = Database.GetConnection();

                using (var cmdCount = Database.Cmd(conn, $"SELECT COUNT(*) FROM NotasFiscais {where}"))
                {
                    if (!string.IsNullOrEmpty(_filtro)) cmdCount.Parameters.AddWithValue("@q", $"%{_filtro}%");
                    if (!string.IsNullOrEmpty(statusTag)) cmdCount.Parameters.AddWithValue("@st", statusTag);
                    int total = Convert.ToInt32(cmdCount.ExecuteScalar() ?? 0);
                    _totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
                    if (_page > _totalPages) _page = _totalPages;
                }

                int offset = (_page - 1) * PageSize;
                using var cmd = Database.Cmd(conn, 
                    $"SELECT * FROM NotasFiscais {where} ORDER BY Id DESC LIMIT {PageSize} OFFSET {offset}");
                if (!string.IsNullOrEmpty(_filtro)) cmd.Parameters.AddWithValue("@q", $"%{_filtro}%");
                if (!string.IsNullOrEmpty(statusTag)) cmd.Parameters.AddWithValue("@st", statusTag);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(MapRow(r));

                GridNotas.ItemsSource = list;
                if (LblPageInfo != null)
                    LblPageInfo.Text = $"Pág {_page}/{_totalPages}";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        /// <summary>Carrega uma única nota completa do banco (todos os campos, incluindo os de emissão
        /// já confirmada — ChaveAcesso, QrCodeUrl etc.) para abrir a janela de ações fiscais.</summary>
        private static NotaFiscalModel? CarregarNotaPorId(long id)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = Database.Cmd(conn, "SELECT * FROM NotasFiscais WHERE Id=@id");
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                return r.Read() ? MapRow(r) : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar a nota: {ex.Message}", "Nota Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>Mapeamento único linha→modelo, reaproveitado pela grade (<see cref="LoadGrid"/>) e
        /// pelo carregamento individual (<see cref="CarregarNotaPorId"/>) — evita duplicar ~70 linhas.</summary>
        private static NotaFiscalModel MapRow(NpgsqlDataReader r) => new()
        {
            Id = Convert.ToInt64(Database.FieldOrDbNull(r, "Id")),
            Serie = Col(r, "Serie"),
            Numero = Database.FieldOrDbNull(r, "Numero") != DBNull.Value ? Convert.ToInt64(Database.FieldOrDbNull(r, "Numero")) : 0,
            DataEmissao = DateTime.TryParse(Col(r, "DataEmissao"), out var dt) ? dt : DateTime.Now,
            DestNome = Col(r, "DestNome"),
            DestCpfCnpj = Col(r, "DestCpfCnpj"),
            DestIe = Col(r, "DestIe"),
            DestEmail = Col(r, "DestEmail"),
            DestLogradouro = Col(r, "DestLogradouro"),
            DestNumero = Col(r, "DestNumero"),
            DestBairro = Col(r, "DestBairro"),
            DestMunicipio = Col(r, "DestMunicipio"),
            DestUf = Col(r, "DestUf"),
            DestCep = Col(r, "DestCep"),
            DestCodigoIbge = Col(r, "DestCodigoIbge"),
            NaturezaOperacao = Col(r, "NaturezaOperacao"),
            ProdutoCodigo = Col(r, "ProdutoCodigo"),
            ProdutoDescricao = Col(r, "ProdutoDescricao"),
            ProdutoNcm = Col(r, "ProdutoNcm"),
            ProdutoCfop = Col(r, "ProdutoCfop"),
            ProdutoUnidade = Col(r, "ProdutoUnidade"),
            ProdutoQuantidade = DbDec(Database.FieldOrDbNull(r, "ProdutoQuantidade")),
            ProdutoValorUnitario = DbDec(Database.FieldOrDbNull(r, "ProdutoValorUnitario")),
            ProdutoValorTotal = DbDec(Database.FieldOrDbNull(r, "ProdutoValorTotal")),
            IcmsAliquota = DbDec(Database.FieldOrDbNull(r, "IcmsAliquota")),
            IcmsValor = DbDec(Database.FieldOrDbNull(r, "IcmsValor")),
            PisAliquota = DbDec(Database.FieldOrDbNull(r, "PisAliquota")),
            PisValor = DbDec(Database.FieldOrDbNull(r, "PisValor")),
            CofinsAliquota = DbDec(Database.FieldOrDbNull(r, "CofinsAliquota")),
            CofinsValor = DbDec(Database.FieldOrDbNull(r, "CofinsValor")),
            ValorProdutos = DbDec(Database.FieldOrDbNull(r, "ValorProdutos")),
            ValorFrete = DbDec(Database.FieldOrDbNull(r, "ValorFrete")),
            ValorDesconto = DbDec(Database.FieldOrDbNull(r, "ValorDesconto")),
            ValorTotalNota = DbDec(Database.FieldOrDbNull(r, "ValorTotalNota")),
            FormaPagamento = Col(r, "FormaPagamento", "01"),
            InformacoesComplementares = Col(r, "InformacoesComplementares"),
            Status = Col(r, "Status", "Rascunho"),
            CaminhoXml = Col(r, "CaminhoXml"),
            CstIbsCbs = Col(r, "CstIbsCbs", "000"),
            ClassTrib = Col(r, "ClassTrib", "000001"),
            CbsAliquota = DbDec(SafeCol(r, "CbsAliquota")),
            CbsValor = DbDec(SafeCol(r, "CbsValor")),
            IbsAliquota = DbDec(SafeCol(r, "IbsAliquota")),
            IbsValor = DbDec(SafeCol(r, "IbsValor")),
            IbsAliquotaUf = DbDec(SafeCol(r, "IbsAliquotaUf")),
            IbsValorUf = DbDec(SafeCol(r, "IbsValorUf")),
            IbsAliquotaMun = DbDec(SafeCol(r, "IbsAliquotaMun")),
            IbsValorMun = DbDec(SafeCol(r, "IbsValorMun")),
            IdDest = Col(r, "IdDest", "1"),
            IndIEDest = Col(r, "IndIEDest", "9"),
            Csosn = Col(r, "Csosn", "102"),
            ProdutoCest = Col(r, "ProdutoCest"),
            ProdutoGtin = Col(r, "ProdutoGtin", "SEM GTIN"),
            IcmsOrigem = Col(r, "IcmsOrigem", "0"),
            IcmsCst = Col(r, "IcmsCst", "00"),
            PisCst = Col(r, "PisCst", "01"),
            CofinsCst = Col(r, "CofinsCst", "01"),
            TipoOperacao = Col(r, "TipoOperacao", "1"),
            Finalidade = Col(r, "Finalidade", "1"),
            ConsumidorFinal = Col(r, "ConsumidorFinal", "1"),
            PresencaComprador = Col(r, "PresencaComprador", "1"),
            Ambiente = Col(r, "Ambiente", "2"),
            Modelo = Col(r, "Modelo", "55"),
            ChaveAcesso = Col(r, "ChaveAcesso"),
            NProt = Col(r, "NProt"),
            DhRecbto = Col(r, "DhRecbto"),
            CStat = Col(r, "CStat"),
            XMotivo = Col(r, "XMotivo"),
            MensagemTraduzida = Col(r, "MensagemTraduzida"),
            QrCodeUrl = Col(r, "QrCodeUrl"),
            XmlAutorizado = Col(r, "XmlAutorizado")
        };

        private static object? SafeCol(NpgsqlDataReader r, string name)
        {
            try { return Database.FieldOrDbNull(r, name); }
            catch { return DBNull.Value; }
        }

        private static void AtualizarUltimoNumero(long numero) =>
            EmpresaConfigStore.AtualizarUltimoNumeroNfeSeMaior(numero);

        private static decimal ParseDec(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Replace("R$", "").Trim();
            if (decimal.TryParse(s, NumberStyles.Number, PtBr, out var v)) return v;
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v2)) return v2;
            return 0;
        }

        private static decimal DbDec(object? o)
        {
            if (o == null || o == DBNull.Value) return 0;
            if (o is decimal d) return d;
            if (o is double dbl) return Convert.ToDecimal(dbl);
            return ParseDec(o.ToString());
        }

        private static string Col(NpgsqlDataReader r, string c, string def = "")
        {
            try { var v = r[c]; return v == DBNull.Value || v == null ? def : v.ToString() ?? def; }
            catch { return def; }
        }
    }
}
