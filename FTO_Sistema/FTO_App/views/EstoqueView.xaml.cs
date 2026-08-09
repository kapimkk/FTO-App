using ClosedXML.Excel;
using FTO_App.Models;
using FTO_App.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using Npgsql;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FTO_App.Views
{
    public partial class EstoqueView : UserControl
    {
        public event EventHandler OnBackRequest;

        private long? _editingId = null;
        private string _currentFilter = "";
        private readonly List<string> _categorias = new();
        private bool _isLoaded = false;
        private bool _suppressMoneyFormat;
        private const int PageSize = 50;
        private int _page = 1;
        private int _totalPages = 1;

        public EstoqueView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                LoadCategorias();
                LoadProdutos();
                _isLoaded = true;
            };
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => OnBackRequest?.Invoke(this, EventArgs.Empty);

        // ─── Código de Barras ─────────────────────────────────────────────
        private void BtnFocusBarcode_Click(object sender, RoutedEventArgs e)
            => TxtCodigoBarras.Focus();

        private void TxtCodigoBarras_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                string code = TxtCodigoBarras.Text.Trim();
                if (string.IsNullOrWhiteSpace(code)) return;
                BuscarPorCodigoBarras(code);
                e.Handled = true;
            }
        }

        private void BuscarPorCodigoBarras(string code)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = Database.Cmd(conn, "SELECT * FROM Produtos WHERE CodigoBarras = @c LIMIT 1");
                cmd.Parameters.AddWithValue("@c", code);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    PreencherFormulario(r);
                    LblFormTitulo.Text = "Editar produto";
                    FormOverlay.Visibility = Visibility.Visible;
                    TxtNome.Focus();
                }
                else
                {
                    TxtNome.Focus();
                }
            }
            catch { TxtNome.Focus(); }
        }

        private void PreencherFormulario(NpgsqlDataReader r)
        {
            _editingId = Convert.ToInt64(Database.FieldOrDbNull(r, "Id"));
            TxtCodigoBarras.Text = Database.FieldOrDbNull(r, "CodigoBarras")?.ToString() ?? "";
            TxtNome.Text = Database.FieldOrDbNull(r, "Nome")?.ToString() ?? "";
            TxtDescricao.Text = Database.FieldOrDbNull(r, "Descricao")?.ToString() ?? "";
            TxtCusto.Text = ParseDecimalDb(Database.FieldOrDbNull(r, "CustoProduto")).ToString("N2");
            TxtPrecoVenda.Text = ParseDecimalDb(Database.FieldOrDbNull(r, "PrecoVenda")).ToString("N2");
            TxtQuantidade.Text = Database.FieldOrDbNull(r, "Quantidade")?.ToString() ?? "0";
            CbCategoria.Text = Database.FieldOrDbNull(r, "Categoria")?.ToString() ?? "";
            CbUnidade.Text = Database.FieldOrDbNull(r, "Unidade")?.ToString() ?? "UN";
            PreencherFiscalDoReader(r);
            BtnSalvarProduto.Content = "💾 ATUALIZAR";
            BtnExcluirProduto.IsEnabled = true;
        }

        private void PreencherFiscalDoReader(NpgsqlDataReader r)
        {
            TxtNcm.Text = StrField(r, "Ncm");
            TxtCest.Text = StrField(r, "Cest");
            TxtCfop.Text = StrField(r, "Cfop");
            SetComboTag(CbOrigem, StrField(r, "Origem", "0"));
            CbCsosn.Text = StrField(r, "Csosn");
            CbCstIcms.Text = StrField(r, "CstIcms");
            TxtIcmsAliquota.Text = FormatAliq(ParseDecimalDb(SafeField(r, "IcmsAliquota")));
            CbPisCst.Text = StrField(r, "PisCst");
            TxtPisAliquota.Text = FormatAliq(ParseDecimalDb(SafeField(r, "PisAliquota")));
            CbCofinsCst.Text = StrField(r, "CofinsCst");
            TxtCofinsAliquota.Text = FormatAliq(ParseDecimalDb(SafeField(r, "CofinsAliquota")));
            TxtCodigoBeneficio.Text = StrField(r, "CodigoBeneficio");
            TxtInfAdicionais.Text = StrField(r, "InfAdicionais");
            TxtCstIbsCbs.Text = StrField(r, "CstIbsCbs", "000");
            TxtClassTrib.Text = StrField(r, "ClassTrib", "000001");
            TxtCbsAliqProd.Text = FormatAliq(ParseDecimalDb(SafeField(r, "CbsAliquota")));
            TxtIbsAliqProd.Text = FormatAliq(ParseDecimalDb(SafeField(r, "IbsAliquota")));
            TxtIbsCbsReducao.Text = FormatAliq(ParseDecimalDb(SafeField(r, "IbsCbsReducao")));
        }

        private static string FormatAliq(decimal v)
        {
            string s = v.ToString("0.####");
            return string.IsNullOrEmpty(s) ? "0" : s;
        }

        private static object? SafeField(NpgsqlDataReader r, string name)
        {
            try { return Database.FieldOrDbNull(r, name); }
            catch { return DBNull.Value; }
        }

        private static string StrField(NpgsqlDataReader r, string name, string fallback = "")
        {
            try { return Database.FieldOrDbNull(r, name)?.ToString() ?? fallback; }
            catch { return fallback; }
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
            cb.SelectedIndex = 0;
        }

        private static string GetComboTagOrText(ComboBox cb)
        {
            if (cb.SelectedItem is ComboBoxItem item && item.Tag != null)
                return item.Tag.ToString() ?? "";
            return cb.Text?.Trim() ?? "";
        }

        // ─── Cálculo de Margem / dinheiro ──────────────────────────────────
        private void TxtMoney_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox) return;
            MoneyInputHelper.ApplyLiveFormat((TextBox)sender, ref _suppressMoneyFormat, RecalcMargem);
        }

        private void RecalcMargem()
        {
            decimal custo = MoneyInputHelper.Parse(TxtCusto?.Text);
            decimal venda = MoneyInputHelper.Parse(TxtPrecoVenda?.Text);
            if (TxtMargem == null) return;
            if (venda > 0)
                TxtMargem.Text = (((venda - custo) / venda) * 100).ToString("N1") + "%";
            else
                TxtMargem.Text = "0,0%";
        }

        private void CalcMargem(object sender, TextChangedEventArgs e) => RecalcMargem();

        // ─── CRUD ─────────────────────────────────────────────────────────
        private void BtnSalvarProduto_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNome.Text))
            {
                MessageBox.Show("O nome do produto é obrigatório.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNome.Focus();
                return;
            }

            var p = new Dictionary<string, object>
            {
                { "@cb", DbVal(TxtCodigoBarras.Text) },
                { "@no", TxtNome.Text.Trim() },
                { "@de", DbVal(TxtDescricao.Text) },
                { "@pv", MoneyInputHelper.Parse(TxtPrecoVenda.Text) },
                { "@cp", MoneyInputHelper.Parse(TxtCusto.Text) },
                { "@qt", ParseInt(TxtQuantidade.Text) },
                { "@ca", DbVal(CbCategoria.Text) },
                { "@un", CbUnidade.Text ?? "UN" },
                { "@dc", DateTime.Today.ToString("yyyy-MM-dd") },
                { "@ncm", DbVal(TxtNcm.Text) },
                { "@cest", DbVal(TxtCest.Text) },
                { "@cfop", DbVal(TxtCfop.Text) },
                { "@ori", GetComboTagOrText(CbOrigem) },
                { "@csosn", DbVal(CbCsosn.Text) },
                { "@csticms", DbVal(CbCstIcms.Text) },
                { "@icmsa", MoneyInputHelper.Parse(TxtIcmsAliquota.Text) },
                { "@piscst", DbVal(CbPisCst.Text) },
                { "@pisa", MoneyInputHelper.Parse(TxtPisAliquota.Text) },
                { "@cofcst", DbVal(CbCofinsCst.Text) },
                { "@cofa", MoneyInputHelper.Parse(TxtCofinsAliquota.Text) },
                { "@benef", DbVal(TxtCodigoBeneficio.Text) },
                { "@inf", DbVal(TxtInfAdicionais.Text) },
                { "@cstibs", DbVal(TxtCstIbsCbs.Text) },
                { "@classt", DbVal(TxtClassTrib.Text) },
                { "@cbsa", MoneyInputHelper.Parse(TxtCbsAliqProd.Text) },
                { "@ibsa", MoneyInputHelper.Parse(TxtIbsAliqProd.Text) },
                { "@red", MoneyInputHelper.Parse(TxtIbsCbsReducao.Text) }
            };

            string sql;
            if (_editingId.HasValue)
            {
                sql = @"UPDATE Produtos SET CodigoBarras=@cb, Nome=@no, Descricao=@de, PrecoVenda=@pv, CustoProduto=@cp,
                    Quantidade=@qt, Categoria=@ca, Unidade=@un, Ncm=@ncm, Cest=@cest, Cfop=@cfop, Origem=@ori,
                    Csosn=@csosn, CstIcms=@csticms, IcmsAliquota=@icmsa, PisCst=@piscst, PisAliquota=@pisa,
                    CofinsCst=@cofcst, CofinsAliquota=@cofa, CodigoBeneficio=@benef, InfAdicionais=@inf,
                    CstIbsCbs=@cstibs, ClassTrib=@classt, CbsAliquota=@cbsa, IbsAliquota=@ibsa, IbsCbsReducao=@red WHERE Id=@id";
                p.Add("@id", _editingId.Value);
            }
            else
            {
                sql = @"INSERT INTO Produtos (CodigoBarras, Nome, Descricao, PrecoVenda, CustoProduto, Quantidade, Categoria, Unidade,
                    Ativo, DataCadastro, Ncm, Cest, Cfop, Origem, Csosn, CstIcms, IcmsAliquota, PisCst, PisAliquota,
                    CofinsCst, CofinsAliquota, CodigoBeneficio, InfAdicionais, CstIbsCbs, ClassTrib, CbsAliquota, IbsAliquota, IbsCbsReducao)
                    VALUES (@cb, @no, @de, @pv, @cp, @qt, @ca, @un, 1, @dc, @ncm, @cest, @cfop, @ori, @csosn, @csticms,
                    @icmsa, @piscst, @pisa, @cofcst, @cofa, @benef, @inf, @cstibs, @classt, @cbsa, @ibsa, @red)";
            }

            try
            {
                Database.ExecuteNonQuery(sql, p);
                MessageBox.Show(_editingId.HasValue ? "Produto atualizado!" : "Produto cadastrado!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                LimparFormulario();
                FormOverlay.Visibility = Visibility.Collapsed;
                LoadCategorias();
                LoadProdutos();
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExcluirProduto_Click(object sender, RoutedEventArgs e)
        {
            if (!_editingId.HasValue) return;
            ExcluirProduto(_editingId.Value, TxtNome?.Text);
        }

        private void BtnExcluirLista_Click(object sender, RoutedEventArgs e)
        {
            if (GridProdutos.SelectedItem is not ProdutoModel p)
            {
                MessageBox.Show("Selecione um produto na lista.", "Estoque", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExcluirProduto(p.Id, p.Nome);
        }

        private void ExcluirProduto(long id, string? nome)
        {
            string label = string.IsNullOrWhiteSpace(nome) ? $"#{id}" : nome.Trim();
            if (MessageBox.Show($"Deseja realmente excluir o produto \"{label}\"?", "Confirmar exclusão",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                Database.ExecuteNonQuery("DELETE FROM Produtos WHERE Id=@id", new Dictionary<string, object> { { "@id", id } });
                if (_editingId == id)
                {
                    LimparFormulario();
                    FormOverlay.Visibility = Visibility.Collapsed;
                }
                LoadProdutos();
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao excluir: {ex.Message}", "Estoque", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void LimparFormulario()
        {
            _editingId = null;
            TxtCodigoBarras.Text = "";
            TxtNome.Text = "";
            TxtDescricao.Text = "";
            TxtCusto.Text = "";
            TxtPrecoVenda.Text = "";
            TxtMargem.Text = "";
            TxtQuantidade.Text = "0";
            CbCategoria.Text = "";
            CbUnidade.SelectedIndex = 0;
            TxtNcm.Text = "";
            TxtCest.Text = "";
            TxtCfop.Text = "";
            CbOrigem.SelectedIndex = 0;
            CbCsosn.Text = "";
            CbCstIcms.Text = "";
            TxtIcmsAliquota.Text = "0";
            CbPisCst.Text = "";
            TxtPisAliquota.Text = "0";
            CbCofinsCst.Text = "";
            TxtCofinsAliquota.Text = "0";
            TxtCodigoBeneficio.Text = "";
            TxtInfAdicionais.Text = "";
            TxtCstIbsCbs.Text = "000";
            TxtClassTrib.Text = "000001";
            TxtCbsAliqProd.Text = "0";
            TxtIbsAliqProd.Text = "0";
            TxtIbsCbsReducao.Text = "0";
            BtnSalvarProduto.Content = "💾 SALVAR";
            BtnExcluirProduto.IsEnabled = false;
            LblFormTitulo.Text = "Cadastrar produto";
        }

        private void BtnCadastrarProduto_Click(object sender, RoutedEventArgs e)
        {
            LimparFormulario();
            LblFormTitulo.Text = "Cadastrar produto";
            FormOverlay.Visibility = Visibility.Visible;
            TxtCodigoBarras.Focus();
        }

        private void BtnFecharCadastro_Click(object sender, RoutedEventArgs e)
        {
            LimparFormulario();
            FormOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnEditarProduto_Click(object sender, RoutedEventArgs e)
        {
            if (GridProdutos.SelectedItem is not ProdutoModel p)
            {
                MessageBox.Show("Selecione um produto na lista.", "Estoque", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            PreencherFormularioDoModel(p);
            FormOverlay.Visibility = Visibility.Visible;
        }

        private void GridProdutos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Seleção só destaca a linha; formulário abre via Editar / duplo clique
        }

        private void GridProdutos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridProdutos.SelectedItem is ProdutoModel p)
            {
                PreencherFormularioDoModel(p);
                FormOverlay.Visibility = Visibility.Visible;
            }
        }

        private void PreencherFormularioDoModel(ProdutoModel p)
        {
            _editingId = p.Id;
            TxtCodigoBarras.Text = p.CodigoBarras;
            TxtNome.Text = p.Nome;
            TxtDescricao.Text = p.Descricao;
            TxtCusto.Text = p.CustoProduto.ToString("N2");
            TxtPrecoVenda.Text = p.PrecoVenda.ToString("N2");
            TxtQuantidade.Text = p.Quantidade.ToString();
            CbCategoria.Text = p.Categoria;
            CbUnidade.Text = p.Unidade;
            TxtNcm.Text = p.Ncm;
            TxtCest.Text = p.Cest;
            TxtCfop.Text = p.Cfop;
            SetComboTag(CbOrigem, string.IsNullOrWhiteSpace(p.Origem) ? "0" : p.Origem);
            CbCsosn.Text = p.Csosn;
            CbCstIcms.Text = p.CstIcms;
            TxtIcmsAliquota.Text = FormatAliq(p.IcmsAliquota);
            CbPisCst.Text = p.PisCst;
            TxtPisAliquota.Text = FormatAliq(p.PisAliquota);
            CbCofinsCst.Text = p.CofinsCst;
            TxtCofinsAliquota.Text = FormatAliq(p.CofinsAliquota);
            TxtCodigoBeneficio.Text = p.CodigoBeneficio;
            TxtInfAdicionais.Text = p.InfAdicionais;
            TxtCstIbsCbs.Text = string.IsNullOrWhiteSpace(p.CstIbsCbs) ? "000" : p.CstIbsCbs;
            TxtClassTrib.Text = string.IsNullOrWhiteSpace(p.ClassTrib) ? "000001" : p.ClassTrib;
            TxtCbsAliqProd.Text = FormatAliq(p.CbsAliquota);
            TxtIbsAliqProd.Text = FormatAliq(p.IbsAliquota);
            TxtIbsCbsReducao.Text = FormatAliq(p.IbsCbsReducao);
            BtnSalvarProduto.Content = "💾 ATUALIZAR";
            BtnExcluirProduto.IsEnabled = true;
            LblFormTitulo.Text = "Editar produto";
            RecalcMargem();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_page > 1) { _page--; LoadProdutos(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_page < _totalPages) { _page++; LoadProdutos(); }
        }

        // ─── Carregar dados ───────────────────────────────────────────────
        private void LoadCategorias()
        {
            _categorias.Clear();
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = Database.Cmd(conn, "SELECT DISTINCT Categoria FROM Produtos WHERE Categoria IS NOT NULL AND Categoria != '' ORDER BY Categoria");
                using var r = cmd.ExecuteReader();
                while (r.Read()) _categorias.Add(r.GetString(0));
            }
            catch { }

            CbFiltroCat.Items.Clear();
            CbFiltroCat.Items.Add("Todas as Categorias");
            foreach (var c in _categorias) CbFiltroCat.Items.Add(c);
            CbFiltroCat.SelectedIndex = 0;
        }

        private void LoadProdutos()
        {
            string where = BuildWhere();
            var list = new List<ProdutoModel>();
            int total = 0;
            decimal valorEstoque = 0;
            int itens = 0;

            try
            {
                using var conn = Database.GetConnection();

                using (var cmdCount = Database.Cmd(conn, $"SELECT COUNT(*) FROM Produtos {where}"))
                {
                    if (!string.IsNullOrEmpty(_currentFilter))
                        cmdCount.Parameters.AddWithValue("@q", $"%{_currentFilter}%");
                    total = Convert.ToInt32(cmdCount.ExecuteScalar());
                }

                _totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
                if (_page > _totalPages) _page = _totalPages;
                int offset = (_page - 1) * PageSize;

                using (var cmd = Database.Cmd(conn,
                    $"SELECT * FROM Produtos {where} ORDER BY Nome LIMIT {PageSize} OFFSET {offset}"))
                {
                    if (!string.IsNullOrEmpty(_currentFilter))
                        cmd.Parameters.AddWithValue("@q", $"%{_currentFilter}%");

                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                        list.Add(MapProduto(r));
                }

                using (var cmdTot = Database.Cmd(conn,
                    $"SELECT COALESCE(SUM(Quantidade),0), COALESCE(SUM(CustoProduto * Quantidade),0) FROM Produtos {where}"))
                {
                    if (!string.IsNullOrEmpty(_currentFilter))
                        cmdTot.Parameters.AddWithValue("@q", $"%{_currentFilter}%");
                    using var rt = cmdTot.ExecuteReader();
                    if (rt.Read())
                    {
                        itens = Convert.ToInt32(rt.GetValue(0));
                        valorEstoque = Convert.ToDecimal(rt.GetValue(1));
                    }
                }

                GridProdutos.ItemsSource = list;
                LblTotalProdutos.Text = total.ToString();
                LblTotalItens.Text = itens.ToString();
                LblValorEstoque.Text = valorEstoque.ToString("C2");
                LblPageInfo.Text = $"Pág {_page}/{_totalPages} · {total} produto(s)";
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao carregar produtos: {ex.Message}"); }
        }

        private static ProdutoModel MapProduto(NpgsqlDataReader r)
        {
            return new ProdutoModel
            {
                Id = Convert.ToInt64(Database.FieldOrDbNull(r, "Id")),
                CodigoBarras = Database.FieldOrDbNull(r, "CodigoBarras")?.ToString() ?? "",
                Nome = Database.FieldOrDbNull(r, "Nome")?.ToString() ?? "",
                Descricao = Database.FieldOrDbNull(r, "Descricao")?.ToString() ?? "",
                PrecoVenda = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "PrecoVenda")),
                CustoProduto = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "CustoProduto")),
                Quantidade = Database.FieldOrDbNull(r, "Quantidade") != DBNull.Value ? Convert.ToInt32(Database.FieldOrDbNull(r, "Quantidade")) : 0,
                Categoria = Database.FieldOrDbNull(r, "Categoria")?.ToString() ?? "",
                Unidade = Database.FieldOrDbNull(r, "Unidade")?.ToString() ?? "UN",
                Ativo = Database.FieldOrDbNull(r, "Ativo") != DBNull.Value ? Convert.ToInt32(Database.FieldOrDbNull(r, "Ativo")) : 1,
                Ncm = StrField(r, "Ncm"),
                Cest = StrField(r, "Cest"),
                Cfop = StrField(r, "Cfop"),
                Origem = StrField(r, "Origem", "0"),
                Csosn = StrField(r, "Csosn"),
                CstIcms = StrField(r, "CstIcms"),
                IcmsAliquota = MoneyInputHelper.ParseDb(SafeField(r, "IcmsAliquota")),
                PisCst = StrField(r, "PisCst"),
                PisAliquota = MoneyInputHelper.ParseDb(SafeField(r, "PisAliquota")),
                CofinsCst = StrField(r, "CofinsCst"),
                CofinsAliquota = MoneyInputHelper.ParseDb(SafeField(r, "CofinsAliquota")),
                CodigoBeneficio = StrField(r, "CodigoBeneficio"),
                InfAdicionais = StrField(r, "InfAdicionais"),
                CstIbsCbs = StrField(r, "CstIbsCbs", "000"),
                ClassTrib = StrField(r, "ClassTrib", "000001"),
                CbsAliquota = MoneyInputHelper.ParseDb(SafeField(r, "CbsAliquota")),
                IbsAliquota = MoneyInputHelper.ParseDb(SafeField(r, "IbsAliquota")),
                IbsCbsReducao = MoneyInputHelper.ParseDb(SafeField(r, "IbsCbsReducao"))
            };
        }

        private string BuildWhere()
        {
            var conditions = new List<string> { "1=1" };

            if (!string.IsNullOrEmpty(_currentFilter))
                conditions.Add("(Nome LIKE @q OR CodigoBarras LIKE @q OR Categoria LIKE @q)");

            if (CbFiltroCat?.SelectedIndex > 0 && CbFiltroCat.SelectedItem is string cat && cat != "Todas as Categorias")
                conditions.Add($"Categoria = '{cat.Replace("'", "''")}'");

            if (CbFiltroStatus?.SelectedIndex == 1) conditions.Add("Ativo = 1");
            else if (CbFiltroStatus?.SelectedIndex == 2) conditions.Add("Ativo = 0");

            return "WHERE " + string.Join(" AND ", conditions);
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            _currentFilter = TxtBuscaProduto.Text.Trim();
            _page = 1;
            LoadProdutos();
        }

        private void CbFiltroCat_Changed(object sender, SelectionChangedEventArgs e) { if (_isLoaded) { _page = 1; LoadProdutos(); } }
        private void CbFiltroStatus_Changed(object sender, SelectionChangedEventArgs e) { if (_isLoaded) { _page = 1; LoadProdutos(); } }
        private void BtnFiltrar_Click(object sender, RoutedEventArgs e) { _currentFilter = TxtBuscaProduto.Text.Trim(); _page = 1; LoadProdutos(); }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Estoque_{DateTime.Now:yyyyMMdd}.xlsx" };
            if (sfd.ShowDialog() != true) return;
            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Estoque");
                var list = GridProdutos.ItemsSource as List<ProdutoModel>;
                ws.Cell(1, 1).InsertTable(list);
                ws.Columns().AdjustToContents();
                wb.SaveAs(sfd.FileName);
                MessageBox.Show("Exportado com sucesso!", "Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Erro: {ex.Message}"); }
        }

        // ─── Helpers ──────────────────────────────────────────────────────
        private object DbVal(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
        private int ParseInt(string? s) { int.TryParse(s?.Trim(), out int v); return v; }
        private decimal ParseUiDecimal(string? s) => MoneyInputHelper.Parse(s);
        private decimal ParseDecimalDb(object? o) => MoneyInputHelper.ParseDb(o);
    }
}
