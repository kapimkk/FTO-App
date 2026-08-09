using ClosedXML.Excel;
using FTO_App.Models;
using FTO_App.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using Npgsql;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace FTO_App.Views
{
    public partial class DashboardView : UserControl
    {
        public event EventHandler OnLogoutRequest;

        public void SetEmbeddedMode(bool embedded)
        {
            if (BtnVoltarHeader != null)
                BtnVoltarHeader.Visibility = embedded ? Visibility.Collapsed : Visibility.Visible;
        }

        private long? _editingId = null;
        private long? _editingClientId = null;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private const int ITEMS_PER_PAGE = 50;
        private string _currentFilter = "";
        private bool _suppressClienteAutoFill = false;
        private bool _suppressProdutoFill = false;
        private string _produtoFiltroTexto = "";
        private readonly List<string> _todosClientesNomes = new();
        private readonly HashSet<string> _clientesPorNome = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ProdutoOpcao> _produtosEstoque = new();

        private sealed class ProdutoOpcao
        {
            public long Id { get; set; }
            public string Nome { get; set; } = "";
            public decimal PrecoVenda { get; set; }
            public decimal CustoProduto { get; set; }
            public int Quantidade { get; set; }
            public decimal CbsAliquota { get; set; }
            public decimal IbsAliquota { get; set; }
            public decimal IbsCbsReducao { get; set; }
            public string CstIbsCbs { get; set; } = "000";
            public string ClassTrib { get; set; } = "000001";
            public string Display => $"{Nome} (Est: {Quantidade})";
        }

        public DashboardView()
        {
            InitializeComponent();
            CbCliente.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(CbCliente_TextChanged), true);
            if (DpData != null) DpData.SelectedDate = DateTime.Today;
            PopulateDateFilters();
            LoadClients();
            LoadProdutosEstoque();
            CbProduto.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(CbProduto_TextChanged), true);
            AtualizarPainelTipoLancamento();
            LoadData();
        }

        private void BtnBackToSelect_Click(object sender, RoutedEventArgs e)
        {
            OnLogoutRequest?.Invoke(this, EventArgs.Empty);
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            string? appPath = Environment.ProcessPath;
            if (appPath != null)
            {
                Process.Start(appPath);
                Application.Current.Shutdown();
            }
        }

        private void BtnBackupClients_Click(object sender, RoutedEventArgs e)
        {
            var list = GridClientes.ItemsSource as List<ClienteModel>;

            if (list == null || list.Count == 0)
            {
                MessageBox.Show("Não há clientes cadastrados para fazer backup.");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Excel|*.xlsx",
                FileName = $"Backup_Clientes_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("Clientes");

                        ws.Cell(1, 1).InsertTable(list);

                        ws.Columns().AdjustToContents();

                        wb.SaveAs(sfd.FileName);
                        MessageBox.Show("Backup de clientes realizado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao gerar o backup: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDownloadClientsPdf_Click(object sender, RoutedEventArgs e)
        {
            var list = GridClientes.ItemsSource as List<ClienteModel>;

            if (list == null || list.Count == 0)
            {
                MessageBox.Show("Não há clientes cadastrados para exportar em PDF.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"Clientes_FTO_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            try
            {
                PdfService.GerarListaClientesPdf(list, saveDialog.FileName);
                MessageBox.Show(
                    $"PDF gerado com sucesso!\n\n{saveDialog.FileName}",
                    "Clientes PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erro ao gerar PDF: {ex.Message}",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void PopulateDateFilters()
        {
            if (CbFiltroMes == null || CbFiltroAno == null) return;
            CbFiltroMes.Items.Clear();
            CbFiltroMes.Items.Add("Mês (Todos)");
            string[] meses = { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };
            foreach (var m in meses) CbFiltroMes.Items.Add(m);
            CbFiltroMes.SelectedIndex = 0;

            CbFiltroAno.Items.Clear();
            CbFiltroAno.Items.Add("Ano (Todos)");
            for (int y = 2025; y <= 2060; y++) CbFiltroAno.Items.Add(y.ToString());
            CbFiltroAno.SelectedIndex = 0;

            if (CbFiltroTipo != null)
            {
                CbFiltroTipo.Items.Clear();
                CbFiltroTipo.Items.Add("Tipo (Todos)");
                CbFiltroTipo.Items.Add("Serviço");
                CbFiltroTipo.Items.Add("Venda de produto");
                CbFiltroTipo.SelectedIndex = 0;
                CbFiltroTipo.SelectionChanged += (s, e) => { _currentPage = 1; LoadData(); };
            }

            CbFiltroMes.SelectionChanged += (s, e) => { _currentPage = 1; LoadData(); };
            CbFiltroAno.SelectionChanged += (s, e) => { _currentPage = 1; LoadData(); };
        }


        private object GetDbValue(string? text) => string.IsNullOrWhiteSpace(text) ? DBNull.Value : text.Trim();

        private decimal ParseMoney(object? value) => MoneyInputHelper.ParseDb(value);

        private decimal ParseUiMoney(string? text) => MoneyInputHelper.Parse(text);

        private DateTime ParseDbDate(object value)
        {
            if (value == null || value == DBNull.Value) return DateTime.Today;
            string dateStr = value.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(dateStr)) return DateTime.Today;

            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dtIso))
                return dtIso;

            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dtIsoFull))
                return dtIsoFull;

            if (DateTime.TryParse(dateStr, new CultureInfo("pt-BR"), DateTimeStyles.None, out DateTime dtBr))
                return dtBr;

            return DateTime.Today;
        }

        private void LoadData()
        {
            var list = new List<Venda>();
            int offset = (_currentPage - 1) * ITEMS_PER_PAGE;
            int totalRegistros = 0;
            decimal totalVendas = 0;
            decimal totalGastos = 0;

            // Placeholder __ALIAS__ (não usar {alias} em $"..." — C# interpreta {{ }} e o Replace quebra)
            const string alias = "__ALIAS__";
            string whereDate = "";
            if (CbFiltroMes != null && CbFiltroMes.SelectedIndex > 0)
            {
                int mes = CbFiltroMes.SelectedIndex;
                whereDate += $" AND EXTRACT(MONTH FROM {alias}Data) = {mes}";
            }
            if (CbFiltroAno != null && CbFiltroAno.SelectedIndex > 0)
            {
                string yearStr = CbFiltroAno.SelectedItem.ToString() ?? "";
                if (int.TryParse(yearStr, out int ano))
                    whereDate += $" AND EXTRACT(YEAR FROM {alias}Data) = {ano}";
            }
            if (CbFiltroTipo != null && CbFiltroTipo.SelectedIndex > 0)
            {
                if (CbFiltroTipo.SelectedIndex == 1)
                    whereDate += $" AND ({alias}TipoLancamento = 'Serviço' OR ({alias}TipoLancamento IS NULL AND ({alias}ProdutoId IS NULL OR {alias}ProdutoId = 0)))";
                else if (CbFiltroTipo.SelectedIndex == 2)
                    whereDate += $" AND ({alias}TipoLancamento = 'Venda de produto' OR ({alias}TipoLancamento IS NULL AND {alias}ProdutoId IS NOT NULL AND {alias}ProdutoId > 0))";
            }

            string whereCore = " WHERE 1=1 " + whereDate;
            if (!string.IsNullOrEmpty(_currentFilter))
                whereCore += $" AND ({alias}Cliente LIKE @q OR {alias}Contato LIKE @q OR {alias}CPF_CNPJ LIKE @q OR {alias}TipoServico LIKE @q OR {alias}TipoLancamento LIKE @q)";

            string wherePlain = whereCore.Replace(alias, "");
            string whereJoin = whereCore.Replace(alias, "v.");

            try
            {
                using (var conn = Database.GetConnection())
                {
                    var cmdCount = Database.Cmd(conn, $"SELECT COUNT(*) FROM Vendas {wherePlain}");
                    if (!string.IsNullOrEmpty(_currentFilter)) cmdCount.Parameters.AddWithValue("@q", $"%{_currentFilter}%");
                    object? scalar = cmdCount.ExecuteScalar();
                    totalRegistros = scalar != null ? Convert.ToInt32(scalar) : 0;

                    var cmd = Database.Cmd(conn, $@"
                        SELECT v.*,
                               COALESCE(NULLIF(TRIM(v.CPF_CNPJ), ''), c.Cpf_Cnpj) AS CpfResolvido
                        FROM Vendas v
                        LEFT JOIN Clientes c ON LOWER(TRIM(c.Nome)) = LOWER(TRIM(v.Cliente))
                        {whereJoin}
                        ORDER BY v.Data DESC, v.Id DESC
                        LIMIT {ITEMS_PER_PAGE} OFFSET {offset}");
                    if (!string.IsNullOrEmpty(_currentFilter)) cmd.Parameters.AddWithValue("@q", $"%{_currentFilter}%");

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            long? prodId = Database.FieldOrDbNull(r, "ProdutoId") != DBNull.Value && Database.FieldOrDbNull(r, "ProdutoId") != null
                                ? Convert.ToInt64(Database.FieldOrDbNull(r, "ProdutoId")) : null;
                            string rawTipoLanc = Database.FieldOrDbNull(r, "TipoLancamento") != DBNull.Value ? Database.FieldOrDbNull(r, "TipoLancamento")?.ToString() ?? "" : "";
                            string resolvedTipoLanc = !string.IsNullOrWhiteSpace(rawTipoLanc)
                                ? rawTipoLanc
                                : (prodId.HasValue && prodId.Value > 0 ? "Venda de produto" : "Serviço");

                            string cpf = "";
                            try { cpf = Database.FieldOrDbNull(r, "CpfResolvido")?.ToString() ?? ""; } catch { }
                            if (string.IsNullOrWhiteSpace(cpf))
                                cpf = Database.FieldOrDbNull(r, "CPF_CNPJ")?.ToString() ?? "";

                            list.Add(new Venda
                            {
                                Id = Convert.ToInt64(Database.FieldOrDbNull(r, "Id")),
                                Cliente = Database.FieldOrDbNull(r, "Cliente")?.ToString() ?? "",
                                Contato = Database.FieldOrDbNull(r, "Contato")?.ToString() ?? "",
                                Data = ParseDbDate(Database.FieldOrDbNull(r, "Data")),
                                Gastos = ParseMoney(Database.FieldOrDbNull(r, "Gastos")),
                                VendaValor = ParseMoney(Database.FieldOrDbNull(r, "Venda")),
                                TipoServico = Database.FieldOrDbNull(r, "TipoServico")?.ToString() ?? "",
                                FormaPag = Database.FieldOrDbNull(r, "FormaPag")?.ToString() ?? "",
                                Pago = Database.FieldOrDbNull(r, "Pago")?.ToString() ?? "",
                                CPF_CNPJ = cpf,
                                ProdutoId = prodId,
                                QuantidadeProduto = Database.FieldOrDbNull(r, "QuantidadeProduto") != DBNull.Value && Database.FieldOrDbNull(r, "QuantidadeProduto") != null
                                    ? Convert.ToInt32(Database.FieldOrDbNull(r, "QuantidadeProduto")) : 0,
                                TipoLancamento = resolvedTipoLanc
                            });
                        }
                    }

                    var cmdSum = Database.Cmd(conn, $"SELECT Venda, Gastos FROM Vendas {wherePlain}");
                    if (!string.IsNullOrEmpty(_currentFilter)) cmdSum.Parameters.AddWithValue("@q", $"%{_currentFilter}%");
                    using (var rSum = cmdSum.ExecuteReader())
                    {
                        while (rSum.Read())
                        {
                            totalVendas += ParseMoney(Database.FieldOrDbNull(rSum, "Venda"));
                            totalGastos += ParseMoney(Database.FieldOrDbNull(rSum, "Gastos"));
                        }
                    }
                }

                GridVendas.ItemsSource = list;
                _totalPages = (int)Math.Ceiling((double)totalRegistros / ITEMS_PER_PAGE);
                if (_totalPages < 1) _totalPages = 1;
                LblPageInfo.Text = $"Pág {_currentPage}/{_totalPages} · {totalRegistros} reg. · Vendas {totalVendas:C2}";
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao carregar dados: {ex.Message}"); }
        }

        private void BtnSalvarVenda_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CbCliente.Text) || string.IsNullOrEmpty(TxtVenda.Text))
            {
                MessageBox.Show("Cliente e Venda obrigatórios.");
                return;
            }

            bool isProduto = CbTipoLancamento.SelectedIndex == 1;
            long? produtoId = null;
            int qtdProduto = 0;
            string tipoServicoTexto;
            string tipoLancamentoTexto = isProduto ? "Venda de produto" : "Serviço";

            if (isProduto)
            {
                var prod = ObterProdutoSelecionado();
                string textoDigitado = (CbProduto.Text ?? "").Trim();

                if (prod == null && string.IsNullOrEmpty(textoDigitado))
                {
                    MessageBox.Show("Informe o produto (do estoque ou digite manualmente).", "Venda de produto",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(TxtQtdProduto.Text?.Trim(), out qtdProduto) || qtdProduto <= 0)
                {
                    qtdProduto = 1;
                }

                if (prod != null)
                {
                    int estoqueDisponivel = prod.Quantidade;
                    if (_editingId.HasValue)
                    {
                        var antigo = ObterVinculoProdutoVenda(_editingId.Value);
                        if (antigo.ProdutoId == prod.Id)
                            estoqueDisponivel += antigo.Quantidade;
                    }

                    if (qtdProduto > estoqueDisponivel)
                    {
                        MessageBox.Show(
                            $"Estoque insuficiente.\nDisponível: {estoqueDisponivel}\nSolicitado: {qtdProduto}",
                            "Estoque", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    produtoId = prod.Id;
                    tipoServicoTexto = qtdProduto > 1
                        ? $"{prod.Nome} (x{qtdProduto})"
                        : prod.Nome;
                }
                else
                {
                    // Produto digitado manualmente (sem cadastro no estoque)
                    produtoId = null;
                    tipoServicoTexto = qtdProduto > 1
                        ? $"{textoDigitado} (x{qtdProduto})"
                        : textoDigitado;
                }
            }
            else
            {
                tipoServicoTexto = TxtServico.Text?.Trim() ?? "";
            }

            string cliNome = CbCliente.Text.Trim();
            bool clientExists = false;
            try
            {
                using (var conn = Database.GetConnection())
                {
                    var cmd = Database.Cmd(conn, "SELECT Count(*) FROM Clientes WHERE Nome = @n");
                    cmd.Parameters.AddWithValue("@n", cliNome);
                    clientExists = Convert.ToInt64(cmd.ExecuteScalar()) > 0;
                }
            }
            catch { }

            if (!clientExists && MessageBox.Show($"O cliente '{cliNome}' não existe. Cadastrar agora?", "Novo Cliente", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    Database.ExecuteNonQuery("INSERT INTO Clientes (Nome, Contato, Cpf_Cnpj) VALUES (@n, @c, @d)",
                        new Dictionary<string, object> { { "@n", cliNome }, { "@c", GetDbValue(TxtContato.Text) }, { "@d", GetDbValue(TxtCpf.Text) } });
                    LoadClients();
                }
                catch { }
            }

            DateTime dataVenda = (DpData.SelectedDate ?? DateTime.Today).Date;

            var vinculoAnterior = _editingId.HasValue
                ? ObterVinculoProdutoVenda(_editingId.Value)
                : (ProdutoId: (long?)null, Quantidade: 0);

            string sql;
            var p = new Dictionary<string, object> {
                {"@cl", cliNome}, {"@co", GetDbValue(TxtContato.Text)},
                {"@dt", dataVenda},
                {"@ga", ParseUiMoney(TxtGastos.Text)}, {"@ve", ParseUiMoney(TxtVenda.Text)},
                {"@sv", string.IsNullOrWhiteSpace(tipoServicoTexto) ? DBNull.Value : tipoServicoTexto},
                {"@fp", GetDbValue(CbFormaPag.Text)}, {"@pg", GetDbValue(CbStatus.Text)}, {"@cp", GetDbValue(TxtCpf.Text)},
                {"@pid", produtoId.HasValue ? produtoId.Value : DBNull.Value},
                {"@pqtd", qtdProduto},
                {"@tl", tipoLancamentoTexto}
            };

            if (_editingId.HasValue)
            {
                sql = "UPDATE Vendas SET Cliente=@cl, Contato=@co, Data=@dt, Gastos=@ga, Venda=@ve, TipoServico=@sv, FormaPag=@fp, Pago=@pg, CPF_CNPJ=@cp, ProdutoId=@pid, QuantidadeProduto=@pqtd, TipoLancamento=@tl WHERE Id=@id";
                p.Add("@id", _editingId.Value);
            }
            else
            {
                sql = "INSERT INTO Vendas (Cliente, Contato, Data, Gastos, Venda, TipoServico, FormaPag, Pago, CPF_CNPJ, ProdutoId, QuantidadeProduto, TipoLancamento) VALUES (@cl, @co, @dt, @ga, @ve, @sv, @fp, @pg, @cp, @pid, @pqtd, @tl)";
            }

            try
            {
                // Devolve estoque da venda antiga (se havia produto vinculado)
                if (vinculoAnterior.ProdutoId.HasValue && vinculoAnterior.Quantidade > 0)
                    AjustarEstoque(vinculoAnterior.ProdutoId.Value, vinculoAnterior.Quantidade);

                Database.ExecuteNonQuery(sql, p);

                // Baixa estoque da nova venda de produto
                if (produtoId.HasValue && qtdProduto > 0)
                    AjustarEstoque(produtoId.Value, -qtdProduto);

                MessageBox.Show("Salvo!");
                ClearForm();
                FormOverlayVenda.Visibility = Visibility.Collapsed;
                LoadProdutosEstoque();
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void CbTipoLancamento_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            AtualizarPainelTipoLancamento();
        }

        private void AtualizarPainelTipoLancamento()
        {
            if (PanelServico == null || PanelProduto == null) return;
            bool produto = CbTipoLancamento?.SelectedIndex == 1;
            PanelServico.Visibility = produto ? Visibility.Collapsed : Visibility.Visible;
            PanelProduto.Visibility = produto ? Visibility.Visible : Visibility.Collapsed;
            if (produto)
                LoadProdutosEstoque();
        }

        private void LoadProdutosEstoque()
        {
            _produtosEstoque.Clear();
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = Database.Cmd(conn,
                    @"SELECT Id, Nome, PrecoVenda, CustoProduto, Quantidade,
                             CbsAliquota, IbsAliquota, IbsCbsReducao, CstIbsCbs, ClassTrib
                      FROM Produtos WHERE Ativo = 1 ORDER BY Nome");
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    _produtosEstoque.Add(new ProdutoOpcao
                    {
                        Id = Convert.ToInt64(Database.FieldOrDbNull(r, "Id")),
                        Nome = Database.FieldOrDbNull(r, "Nome")?.ToString() ?? "",
                        PrecoVenda = ParseMoney(Database.FieldOrDbNull(r, "PrecoVenda")),
                        CustoProduto = ParseMoney(Database.FieldOrDbNull(r, "CustoProduto")),
                        Quantidade = Database.FieldOrDbNull(r, "Quantidade") != DBNull.Value ? Convert.ToInt32(Database.FieldOrDbNull(r, "Quantidade")) : 0,
                        CbsAliquota = ParseMoney(Database.FieldOrDbNull(r, "CbsAliquota")),
                        IbsAliquota = ParseMoney(Database.FieldOrDbNull(r, "IbsAliquota")),
                        IbsCbsReducao = ParseMoney(Database.FieldOrDbNull(r, "IbsCbsReducao")),
                        CstIbsCbs = Database.FieldOrDbNull(r, "CstIbsCbs")?.ToString() ?? "000",
                        ClassTrib = Database.FieldOrDbNull(r, "ClassTrib")?.ToString() ?? "000001"
                    });
                }
            }
            catch { }

            if (CbProduto == null) return;

            long? selectedId = (CbProduto.SelectedItem as ProdutoOpcao)?.Id;
            string textoAtual = CbProduto.Text ?? "";

            _suppressProdutoFill = true;
            try
            {
                _produtoFiltroTexto = "";
                CbProduto.ItemsSource = _produtosEstoque;
                var view = CollectionViewSource.GetDefaultView(CbProduto.ItemsSource);
                if (view != null)
                    view.Filter = FiltrarProdutoView;

                if (selectedId.HasValue)
                {
                    CbProduto.SelectedItem = _produtosEstoque.FirstOrDefault(p => p.Id == selectedId.Value);
                }
                else
                {
                    CbProduto.SelectedItem = null;
                    if (!string.IsNullOrWhiteSpace(textoAtual))
                        CbProduto.Text = textoAtual;
                }
            }
            finally
            {
                _suppressProdutoFill = false;
            }
        }

        private bool FiltrarProdutoView(object obj)
        {
            if (obj is not ProdutoOpcao p) return false;
            if (string.IsNullOrWhiteSpace(_produtoFiltroTexto)) return true;
            return p.Nome.Contains(_produtoFiltroTexto, StringComparison.OrdinalIgnoreCase)
                || p.Display.Contains(_produtoFiltroTexto, StringComparison.OrdinalIgnoreCase);
        }

        private void AtualizarFiltroProdutos(bool abrirDropdown)
        {
            if (CbProduto.ItemsSource == null)
            {
                CbProduto.ItemsSource = _produtosEstoque;
                var viewInit = CollectionViewSource.GetDefaultView(CbProduto.ItemsSource);
                if (viewInit != null)
                    viewInit.Filter = FiltrarProdutoView;
            }

            var view = CollectionViewSource.GetDefaultView(CbProduto.ItemsSource);
            view?.Refresh();

            if (!abrirDropdown) return;

            if (!string.IsNullOrWhiteSpace(_produtoFiltroTexto) && view != null && !view.IsEmpty)
                CbProduto.IsDropDownOpen = true;
            else if (!string.IsNullOrWhiteSpace(_produtoFiltroTexto) && (view == null || view.IsEmpty))
                CbProduto.IsDropDownOpen = false;
        }

        private void CbProduto_DropDownOpened(object sender, EventArgs e)
        {
            string texto = CbProduto.Text ?? "";
            if (CbProduto.SelectedItem is ProdutoOpcao sel &&
                string.Equals(texto, sel.Display, StringComparison.Ordinal))
                _produtoFiltroTexto = "";
            else
                _produtoFiltroTexto = texto.Trim();

            AtualizarFiltroProdutos(abrirDropdown: false);
        }

        private void CbProduto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressProdutoFill) return;

            // Não reescreve Text nem caret — só filtra a lista (evita letras trocadas).
            _produtoFiltroTexto = (CbProduto.Text ?? "").Trim();
            AtualizarFiltroProdutos(abrirDropdown: _produtoFiltroTexto.Length > 0);
        }

        private ProdutoOpcao? ObterProdutoSelecionado()
        {
            if (CbProduto.SelectedItem is ProdutoOpcao sel)
                return sel;

            string texto = (CbProduto.Text ?? "").Trim();
            if (string.IsNullOrEmpty(texto)) return null;

            return _produtosEstoque.FirstOrDefault(p =>
                       string.Equals(p.Display, texto, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(p.Nome, texto, StringComparison.OrdinalIgnoreCase))
                   ?? _produtosEstoque.FirstOrDefault(p =>
                       p.Nome.StartsWith(texto, StringComparison.OrdinalIgnoreCase));
        }

        private void CbProduto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressProdutoFill) return;
            if (CbProduto?.SelectedItem is not ProdutoOpcao prod) return;

            _suppressProdutoFill = true;
            try
            {
                CbProduto.Text = prod.Display;
                _produtoFiltroTexto = "";
                CollectionViewSource.GetDefaultView(CbProduto.ItemsSource)?.Refresh();
            }
            finally
            {
                _suppressProdutoFill = false;
            }

            AplicarValoresProduto(prod);
        }

        private void TxtQtdProduto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressProdutoFill) return;
            var prod = ObterProdutoSelecionado();
            if (prod == null) return;
            AplicarValoresProduto(prod);
        }

        private void AplicarValoresProduto(ProdutoOpcao prod)
        {
            int qtd = 1;
            if (!string.IsNullOrWhiteSpace(TxtQtdProduto?.Text) && int.TryParse(TxtQtdProduto.Text.Trim(), out int q) && q > 0)
                qtd = q;

            _suppressProdutoFill = true;
            try
            {
                TxtVenda.Text = (prod.PrecoVenda * qtd).ToString("N2");
                TxtGastos.Text = (prod.CustoProduto * qtd).ToString("N2");
                decimal v = ParseUiMoney(TxtVenda.Text);
                decimal g = ParseUiMoney(TxtGastos.Text);
                TxtLucro.Text = (v - g).ToString("C2");
                AtualizarEstimativaIbsCbs(v, prod);
            }
            finally
            {
                _suppressProdutoFill = false;
            }
        }

        private static (long? ProdutoId, int Quantidade) ObterVinculoProdutoVenda(long vendaId)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = Database.Cmd(conn, 
                    "SELECT ProdutoId, QuantidadeProduto FROM Vendas WHERE Id=@id");
                cmd.Parameters.AddWithValue("@id", vendaId);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    long? pid = Database.FieldOrDbNull(r, "ProdutoId") != DBNull.Value ? Convert.ToInt64(Database.FieldOrDbNull(r, "ProdutoId")) : null;
                    int qtd = Database.FieldOrDbNull(r, "QuantidadeProduto") != DBNull.Value ? Convert.ToInt32(Database.FieldOrDbNull(r, "QuantidadeProduto")) : 0;
                    return (pid, qtd);
                }
            }
            catch { }
            return (null, 0);
        }

        private static void AjustarEstoque(long produtoId, int delta)
        {
            Database.ExecuteNonQuery(
                "UPDATE Produtos SET Quantidade = GREATEST(0, Quantidade + @d) WHERE Id=@id",
                new Dictionary<string, object> { { "@d", delta }, { "@id", produtoId } });
        }

        private void BtnCadastrarVenda_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            LblFormVendaTitulo.Text = "Cadastrar venda";
            FormOverlayVenda.Visibility = Visibility.Visible;
            CbCliente.Focus();
        }

        private void BtnFecharCadastro_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            FormOverlayVenda.Visibility = Visibility.Collapsed;
        }

        private void BtnEditRow_Click(object sender, RoutedEventArgs e)
        {
            if (GridVendas.SelectedItem is Venda v)
            {
                FormOverlayVenda.Visibility = Visibility.Visible;
                LblFormVendaTitulo.Text = "Editar venda";
                _editingId = v.Id;
                _suppressClienteAutoFill = true;
                _suppressProdutoFill = true;
                try
                {
                    CbCliente.Text = v.Cliente;
                    TxtContato.Text = v.Contato;
                    DpData.SelectedDate = v.Data;
                    TxtCpf.Text = v.CPF_CNPJ;
                    CbFormaPag.Text = v.FormaPag;
                    CbStatus.Text = v.Pago;
                    TxtGastos.Text = v.Gastos.ToString("N2");
                    TxtVenda.Text = v.VendaValor.ToString("N2");
                    BtnSalvar.Content = "ATUALIZAR VENDA";

                    bool isProduto = string.Equals(v.TipoLancamento, "Venda de produto", StringComparison.OrdinalIgnoreCase)
                        || (v.ProdutoId.HasValue && v.ProdutoId.Value > 0);

                    if (isProduto)
                    {
                        CbTipoLancamento.SelectedIndex = 1;
                        AtualizarPainelTipoLancamento();
                        LoadProdutosEstoque();
                        if (v.ProdutoId.HasValue && v.ProdutoId.Value > 0)
                        {
                            CbProduto.SelectedItem = _produtosEstoque.FirstOrDefault(p => p.Id == v.ProdutoId.Value);
                        }
                        else
                        {
                            CbProduto.SelectedItem = null;
                            string textoProd = v.TipoServico;
                            int qtd = Math.Max(1, v.QuantidadeProduto);
                            if (qtd > 1 && textoProd.EndsWith($" (x{qtd})"))
                                textoProd = textoProd.Substring(0, textoProd.Length - $" (x{qtd})".Length);
                            CbProduto.Text = textoProd;
                        }
                        TxtQtdProduto.Text = Math.Max(1, v.QuantidadeProduto).ToString();
                        TxtServico.Text = "";
                    }
                    else
                    {
                        CbTipoLancamento.SelectedIndex = 0;
                        AtualizarPainelTipoLancamento();
                        TxtServico.Text = v.TipoServico;
                        CbProduto.SelectedItem = null;
                        CbProduto.Text = "";
                        TxtQtdProduto.Text = "1";
                    }
                }
                finally
                {
                    _suppressClienteAutoFill = false;
                    _suppressProdutoFill = false;
                }
            }
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (GridVendas.SelectedItem is Venda v && MessageBox.Show("Excluir?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (v.ProdutoId.HasValue && v.QuantidadeProduto > 0)
                    AjustarEstoque(v.ProdutoId.Value, v.QuantidadeProduto);

                Database.ExecuteNonQuery("DELETE FROM Vendas WHERE Id=@id", new Dictionary<string, object> { { "@id", v.Id } });
                LoadProdutosEstoque();
                LoadData();
            }
        }

        private void GridVendas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool vendaSelecionada = GridVendas.SelectedItem is Venda;
            BtnImprimir.IsEnabled = vendaSelecionada;
            BtnWhatsapp.IsEnabled = vendaSelecionada;
        }

        private void BtnWhatsapp_Click(object sender, RoutedEventArgs e)
        {
            if (GridVendas.SelectedItem is not Venda venda)
            {
                MessageBox.Show(
                    "Selecione uma venda na tabela para abrir o WhatsApp.",
                    "WhatsApp",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!WhatsAppHelper.OpenChat(venda.Contato, out string? erro))
            {
                MessageBox.Show(
                    erro ?? "Não foi possível abrir o WhatsApp.",
                    "WhatsApp",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void BtnImprimir_Click(object sender, RoutedEventArgs e)
        {
            if (GridVendas.SelectedItem is not Venda venda)
            {
                MessageBox.Show("Selecione uma venda na tabela para imprimir o cupom.", "Impressão",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!ThermalPrinterService.IsPrinterConfigured)
            {
                MessageBox.Show(
                    "Nenhuma impressora configurada.\n\nVolte à tela de módulos (após o login) e selecione a impressora MP-2500 HT.",
                    "Impressora",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var owner = Window.GetWindow(this);
            var dialog = new ConfirmPrintWindow(venda)
            {
                Owner = owner
            };

            dialog.ShowDialog();
        }

        private void BtnMarkPaid_Click(object sender, RoutedEventArgs e)
        {
            if (GridVendas.SelectedItem is Venda v)
            {
                Database.ExecuteNonQuery("UPDATE Vendas SET Pago='Pago' WHERE Id=@id", new Dictionary<string, object> { { "@id", v.Id } });
                LoadData();
            }
        }

        private void BtnLimpar_Click(object sender, RoutedEventArgs e) => ClearForm();
        private void ClearForm()
        {
            _editingId = null;
            BtnSalvar.Content = "SALVAR VENDA";
            _suppressClienteAutoFill = true;
            _suppressProdutoFill = true;
            try
            {
                CbCliente.Text = "";
                CbCliente.SelectedItem = null;
                CbCliente.Items.Clear();
                foreach (string nome in _todosClientesNomes)
                    CbCliente.Items.Add(nome);
                CbCliente.IsDropDownOpen = false;
                TxtContato.Text = "";
                TxtCpf.Text = "";
                TxtServico.Text = "";
                TxtGastos.Text = "";
                TxtVenda.Text = "";
                TxtLucro.Text = "";
                TxtQtdProduto.Text = "1";
                CbProduto.SelectedItem = null;
                CbProduto.Text = "";
                _produtoFiltroTexto = "";
                CollectionViewSource.GetDefaultView(CbProduto.ItemsSource)?.Refresh();
                CbTipoLancamento.SelectedIndex = 0;
                AtualizarPainelTipoLancamento();
                DpData.SelectedDate = DateTime.Today;
                CbFormaPag.SelectedIndex = -1;
                CbStatus.SelectedIndex = 1;
            }
            finally
            {
                _suppressClienteAutoFill = false;
                _suppressProdutoFill = false;
            }
        }

        private void Calc_Lucro(object sender, TextChangedEventArgs e)
        {
            decimal v = ParseUiMoney(TxtVenda.Text);
            decimal g = ParseUiMoney(TxtGastos.Text);
            TxtLucro.Text = (v - g).ToString("C2");
            AtualizarEstimativaIbsCbs(v, ObterProdutoSelecionado());
        }

        private void AtualizarEstimativaIbsCbs(decimal baseCalculo, ProdutoOpcao? prod)
        {
            if (TxtVendaCbs == null) return;

            ReformaTributariaService.Resultado r;
            if (prod != null && (prod.CbsAliquota > 0 || prod.IbsAliquota > 0 || prod.IbsCbsReducao > 0))
            {
                r = ReformaTributariaService.Calcular(
                    baseCalculo,
                    aliqCbsOverride: prod.CbsAliquota > 0 ? prod.CbsAliquota : null,
                    aliqIbsOverride: prod.IbsAliquota > 0 ? prod.IbsAliquota : null,
                    reducaoPercentual: prod.IbsCbsReducao,
                    cst: prod.CstIbsCbs,
                    classTrib: prod.ClassTrib);
            }
            else
            {
                r = ReformaTributariaService.Calcular(baseCalculo);
            }

            TxtVendaCbs.Text = r.ValorCbs.ToString("C2");
            TxtVendaIbs.Text = r.ValorIbs.ToString("C2");
            TxtVendaIva.Text = r.ValorTotalIva.ToString("C2");
            if (LblIbsCbsObs != null)
                LblIbsCbsObs.Text = r.Observacao + " " + ReformaTributariaService.DescricaoPreset(null);
        }

        private void LoadClients()
        {
            _todosClientesNomes.Clear();
            _clientesPorNome.Clear();
            try
            {
                using (var conn = Database.GetConnection())
                using (var cmd = Database.Cmd(conn, "SELECT Nome FROM Clientes ORDER BY Nome"))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string nome = r.GetString(0);
                        _todosClientesNomes.Add(nome);
                        _clientesPorNome.Add(nome);
                    }
                }
            }
            catch { }

            string texto = CbCliente.Text ?? "";
            string filtro = texto.Trim();
            if (string.IsNullOrEmpty(filtro))
            {
                _suppressClienteAutoFill = true;
                try
                {
                    CbCliente.Items.Clear();
                    foreach (string nome in _todosClientesNomes)
                        CbCliente.Items.Add(nome);
                }
                finally
                {
                    _suppressClienteAutoFill = false;
                }
            }
            else
            {
                var textBox = GetCbClienteTextBox();
                int caret = textBox?.CaretIndex ?? texto.Length;
                AplicarFiltroClientesDropdown(filtro, texto, caret, abrirDropdown: false);
            }
        }

        private TextBox? GetCbClienteTextBox() =>
            CbCliente.Template?.FindName("PART_EditableTextBox", CbCliente) as TextBox;

        private List<string> ObterClientesFiltrados(string filtro)
        {
            if (string.IsNullOrWhiteSpace(filtro))
                return new List<string>(_todosClientesNomes);

            return _todosClientesNomes
                .Where(n => n.Contains(filtro.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void AplicarFiltroClientesDropdown(string filtro, string textoExibido, int caret, bool abrirDropdown)
        {
            List<string> filtrados = ObterClientesFiltrados(filtro);

            _suppressClienteAutoFill = true;
            try
            {
                CbCliente.Items.Clear();
                foreach (string nome in filtrados)
                    CbCliente.Items.Add(nome);

                CbCliente.SelectedItem = null;
                CbCliente.Text = textoExibido;

                TextBox? textBox = GetCbClienteTextBox();
                if (textBox != null)
                {
                    int posicaoCaret = Math.Min(caret, textoExibido.Length);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        textBox.CaretIndex = posicaoCaret;
                        textBox.SelectionLength = 0;
                    }), DispatcherPriority.Input);
                }

                if (abrirDropdown && filtrados.Count > 0)
                    CbCliente.IsDropDownOpen = true;
                else if (filtrados.Count == 0)
                    CbCliente.IsDropDownOpen = false;
            }
            finally
            {
                _suppressClienteAutoFill = false;
            }
        }

        private void LimparSelecaoClienteParcial(string textoDigitado)
        {
            if (CbCliente.SelectedItem != null &&
                !string.Equals(CbCliente.SelectedItem.ToString(), textoDigitado, StringComparison.OrdinalIgnoreCase))
            {
                CbCliente.SelectedItem = null;
            }
        }

        private bool ClienteNomeExisteExato(string nome) =>
            !string.IsNullOrWhiteSpace(nome) && _clientesPorNome.Contains(nome.Trim());

        private void PreencherDadosCliente(string nomeCliente)
        {
            try
            {
                using (var conn = Database.GetConnection())
                using (var cmd = Database.Cmd(conn, "SELECT Contato, Cpf_Cnpj FROM Clientes WHERE Nome=@n"))
                {
                    cmd.Parameters.AddWithValue("@n", nomeCliente);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            TxtContato.Text = Database.FieldOrDbNull(r, "Contato")?.ToString() ?? "";
                            TxtCpf.Text = Database.FieldOrDbNull(r, "Cpf_Cnpj")?.ToString() ?? "";
                        }
                    }
                }
            }
            catch { }
        }

        private void CbCliente_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressClienteAutoFill || CbCliente.SelectedItem is not string nomeSelecionado)
                return;

            string textoDigitado = CbCliente.Text?.Trim() ?? "";
            if (!string.Equals(nomeSelecionado, textoDigitado, StringComparison.OrdinalIgnoreCase))
                return;

            PreencherDadosCliente(nomeSelecionado);
        }

        private void CbCliente_DropDownOpened(object sender, EventArgs e)
        {
            if (_suppressClienteAutoFill)
                return;

            string texto = CbCliente.Text ?? "";
            string filtro = texto.Trim();
            TextBox? textBox = GetCbClienteTextBox();
            int caret = textBox?.CaretIndex ?? texto.Length;
            AplicarFiltroClientesDropdown(filtro, texto, caret, abrirDropdown: true);
        }

        private void CbCliente_DropDownClosed(object sender, EventArgs e)
        {
            if (_suppressClienteAutoFill || CbCliente.SelectedItem is not string nomeSelecionado)
                return;

            PreencherDadosCliente(nomeSelecionado);
        }

        private void CbCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressClienteAutoFill)
                return;

            TextBox? textBox = e.OriginalSource as TextBox;
            string texto = CbCliente.Text ?? "";
            string filtro = texto.Trim();
            int caret = textBox?.CaretIndex ?? texto.Length;

            if (string.IsNullOrEmpty(filtro))
            {
                TxtContato.Text = "";
                TxtCpf.Text = "";
                AplicarFiltroClientesDropdown(filtro, texto, caret, abrirDropdown: false);
                return;
            }

            if (ClienteNomeExisteExato(filtro))
                PreencherDadosCliente(filtro);
            else
            {
                TxtContato.Text = "";
                TxtCpf.Text = "";
                LimparSelecaoClienteParcial(texto);
            }

            AplicarFiltroClientesDropdown(filtro, texto, caret, abrirDropdown: true);
        }

        private void BtnQuickAddClient_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CbCliente.Text)) return;
            try
            {
                Database.ExecuteNonQuery("INSERT INTO Clientes (Nome, Contato, Cpf_Cnpj) VALUES (@n, @c, @d)",
                    new Dictionary<string, object> { { "@n", CbCliente.Text }, { "@c", GetDbValue(TxtContato.Text) }, { "@d", GetDbValue(TxtCpf.Text) } });
                LoadClients();
                MessageBox.Show("Cliente cadastrado!");
            }
            catch { }
        }

        private void BtnClientManager_Click(object sender, RoutedEventArgs e) { ClientsGrid.Visibility = Visibility.Visible; LoadClientsGrid(); }
        private void BtnCloseClients_Click(object sender, RoutedEventArgs e) { ClientsGrid.Visibility = Visibility.Collapsed; LoadClients(); ClearClientForm(); }

        private void LoadClientsGrid()
        {
            var list = new List<ClienteModel>();
            try
            {
                using (var conn = Database.GetConnection())
                using (var cmd = Database.Cmd(conn, "SELECT * FROM Clientes ORDER BY Nome"))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(new ClienteModel { Id = r.GetInt64(0), Nome = r.GetString(1), Contato = Database.FieldOrDbNull(r, "Contato")?.ToString() ?? "", CpfCnpj = Database.FieldOrDbNull(r, "Cpf_Cnpj")?.ToString() ?? "" });
                GridClientes.ItemsSource = list;
            }
            catch { }
        }

        private void BtnAddClientInternal_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNewCliNome.Text)) return;
            string sql = _editingClientId.HasValue ? "UPDATE Clientes SET Nome=@n, Contato=@c, Cpf_Cnpj=@d WHERE Id=@id" : "INSERT INTO Clientes (Nome, Contato, Cpf_Cnpj) VALUES (@n, @c, @d)";
            var p = new Dictionary<string, object> { { "@n", TxtNewCliNome.Text }, { "@c", GetDbValue(TxtNewCliContato.Text) }, { "@d", GetDbValue(TxtNewCliDoc.Text) } };
            if (_editingClientId.HasValue) p.Add("@id", _editingClientId.Value);

            try
            {
                Database.ExecuteNonQuery(sql, p);
                ClearClientForm(); LoadClientsGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnEditClientInternal_Click(object sender, RoutedEventArgs e)
        {
            if (GridClientes.SelectedItem is ClienteModel c)
            {
                _editingClientId = c.Id;
                TxtNewCliNome.Text = c.Nome; TxtNewCliContato.Text = c.Contato; TxtNewCliDoc.Text = c.CpfCnpj;
                BtnSaveClient.Content = "💾 Salvar";
            }
        }

        private void BtnDeleteClientInternal_Click(object sender, RoutedEventArgs e)
        {
            if (GridClientes.SelectedItem is ClienteModel c && MessageBox.Show("Excluir?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Database.ExecuteNonQuery("DELETE FROM Clientes WHERE Id=@id", new Dictionary<string, object> { { "@id", c.Id } });
                LoadClientsGrid();
            }
        }

        private void ClearClientForm() { _editingClientId = null; TxtNewCliNome.Text = ""; TxtNewCliContato.Text = ""; TxtNewCliDoc.Text = ""; BtnSaveClient.Content = "+ Adicionar"; }

        private void BtnAutoFit_Click(object sender, RoutedEventArgs e)
        {
            foreach (var column in GridVendas.Columns)
            {
                column.Width = new DataGridLength(1.0, DataGridLengthUnitType.Auto);
                column.Width = new DataGridLength(1.0, DataGridLengthUnitType.SizeToHeader);
                column.Width = DataGridLength.Auto;
            }
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) { _currentFilter = TxtBusca.Text.Trim(); _currentPage = 1; LoadData(); }
        private void BtnRefresh_Click(object sender, RoutedEventArgs e) { _currentFilter = TxtBusca.Text.Trim(); _currentPage = 1; LoadData(); }
        private void BtnPrevPage_Click(object sender, RoutedEventArgs e) { if (_currentPage > 1) { _currentPage--; LoadData(); } }
        private void BtnNextPage_Click(object sender, RoutedEventArgs e) { if (_currentPage < _totalPages) { _currentPage++; LoadData(); } }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Vendas.xlsx" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("Vendas");
                        var list = GridVendas.ItemsSource as List<Venda>;
                        ws.Cell(1, 1).InsertTable(list);
                        wb.SaveAs(sfd.FileName);
                        MessageBox.Show("Exportado!");
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }
    }
}