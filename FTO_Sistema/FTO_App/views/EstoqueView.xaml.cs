using ClosedXML.Excel;
using FTO_App.Models;
using FTO_App.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
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

        public EstoqueView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                LoadCategorias();
                LoadProdutos();
                _isLoaded = true;
                TxtCodigoBarras.Focus();
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
                using var cmd = new SQLiteCommand("SELECT * FROM Produtos WHERE CodigoBarras = @c LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@c", code);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    // Produto encontrado — carregar para edição
                    PreencherFormulario(r);
                    TxtNome.Focus();
                }
                else
                {
                    // Produto não existe — mover foco para nome
                    TxtNome.Focus();
                }
            }
            catch { TxtNome.Focus(); }
        }

        private void PreencherFormulario(SQLiteDataReader r)
        {
            _editingId = Convert.ToInt64(r["Id"]);
            TxtCodigoBarras.Text = r["CodigoBarras"]?.ToString() ?? "";
            TxtNome.Text = r["Nome"]?.ToString() ?? "";
            TxtDescricao.Text = r["Descricao"]?.ToString() ?? "";
            TxtCusto.Text = ParseDecimalDb(r["CustoProduto"]).ToString("N2");
            TxtPrecoVenda.Text = ParseDecimalDb(r["PrecoVenda"]).ToString("N2");
            TxtQuantidade.Text = r["Quantidade"]?.ToString() ?? "0";
            CbCategoria.Text = r["Categoria"]?.ToString() ?? "";
            CbUnidade.Text = r["Unidade"]?.ToString() ?? "UN";
            BtnSalvarProduto.Content = "💾 ATUALIZAR";
            BtnExcluirProduto.IsEnabled = true;
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
                { "@dc", DateTime.Today.ToString("yyyy-MM-dd") }
            };

            string sql;
            if (_editingId.HasValue)
            {
                sql = "UPDATE Produtos SET CodigoBarras=@cb, Nome=@no, Descricao=@de, PrecoVenda=@pv, CustoProduto=@cp, Quantidade=@qt, Categoria=@ca, Unidade=@un WHERE Id=@id";
                p.Add("@id", _editingId.Value);
            }
            else
            {
                sql = "INSERT INTO Produtos (CodigoBarras, Nome, Descricao, PrecoVenda, CustoProduto, Quantidade, Categoria, Unidade, Ativo, DataCadastro) VALUES (@cb, @no, @de, @pv, @cp, @qt, @ca, @un, 1, @dc)";
            }

            try
            {
                Database.ExecuteNonQuery(sql, p);
                MessageBox.Show(_editingId.HasValue ? "Produto atualizado!" : "Produto cadastrado!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                LimparFormulario();
                LoadCategorias();
                LoadProdutos();
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExcluirProduto_Click(object sender, RoutedEventArgs e)
        {
            if (!_editingId.HasValue) return;
            if (MessageBox.Show("Deseja realmente excluir este produto?", "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                Database.ExecuteNonQuery("DELETE FROM Produtos WHERE Id=@id", new Dictionary<string, object> { { "@id", _editingId.Value } });
                LimparFormulario();
                LoadProdutos();
            }
            catch (Exception ex) { MessageBox.Show($"Erro: {ex.Message}"); }
        }

        private void BtnLimpar_Click(object sender, RoutedEventArgs e) => LimparFormulario();

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
            BtnSalvarProduto.Content = "💾 SALVAR PRODUTO";
            BtnExcluirProduto.IsEnabled = false;
            TxtCodigoBarras.Focus();
        }

        // ─── Carregar dados ───────────────────────────────────────────────
        private void LoadCategorias()
        {
            _categorias.Clear();
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = new SQLiteCommand("SELECT DISTINCT Categoria FROM Produtos WHERE Categoria IS NOT NULL AND Categoria != '' ORDER BY Categoria", conn);
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

            try
            {
                using var conn = Database.GetConnection();
                using var cmd = new SQLiteCommand($"SELECT * FROM Produtos {where} ORDER BY Nome", conn);
                if (!string.IsNullOrEmpty(_currentFilter))
                    cmd.Parameters.AddWithValue("@q", $"%{_currentFilter}%");

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new ProdutoModel
                    {
                        Id = Convert.ToInt64(r["Id"]),
                        CodigoBarras = r["CodigoBarras"]?.ToString() ?? "",
                        Nome = r["Nome"]?.ToString() ?? "",
                        Descricao = r["Descricao"]?.ToString() ?? "",
                        PrecoVenda = ParseDecimalDb(r["PrecoVenda"]),
                        CustoProduto = ParseDecimalDb(r["CustoProduto"]),
                        Quantidade = r["Quantidade"] != DBNull.Value ? Convert.ToInt32(r["Quantidade"]) : 0,
                        Categoria = r["Categoria"]?.ToString() ?? "",
                        Unidade = r["Unidade"]?.ToString() ?? "UN",
                        Ativo = r["Ativo"] != DBNull.Value ? Convert.ToInt32(r["Ativo"]) : 1
                    });
                }

                GridProdutos.ItemsSource = list;
                LblTotalProdutos.Text = list.Count.ToString();
                LblTotalItens.Text = list.Sum(p => p.Quantidade).ToString();
                LblValorEstoque.Text = list.Sum(p => p.ValorEstoque).ToString("C2");
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao carregar produtos: {ex.Message}"); }
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

        private void GridProdutos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridProdutos.SelectedItem is ProdutoModel p)
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
                BtnSalvarProduto.Content = "💾 ATUALIZAR";
                BtnExcluirProduto.IsEnabled = true;
            }
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            _currentFilter = TxtBuscaProduto.Text.Trim();
            LoadProdutos();
        }

        private void CbFiltroCat_Changed(object sender, SelectionChangedEventArgs e) { if (_isLoaded) LoadProdutos(); }
        private void CbFiltroStatus_Changed(object sender, SelectionChangedEventArgs e) { if (_isLoaded) LoadProdutos(); }
        private void BtnFiltrar_Click(object sender, RoutedEventArgs e) { _currentFilter = TxtBuscaProduto.Text.Trim(); LoadProdutos(); }

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
        private decimal ParseUiDecimal(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Replace("R$", "").Trim();
            if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal v)) return v;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v2)) return v2;
            return 0;
        }
        private decimal ParseDecimalDb(object? o)
        {
            if (o == null || o == DBNull.Value) return 0;
            // Nunca use ToString()+InvariantCulture: em pt-BR, 19.9 vira "19,9"
            // e NumberStyles.Any interpreta a vírgula como milhar → 199.
            return o switch
            {
                decimal d => d,
                double dbl => Convert.ToDecimal(dbl),
                float f => Convert.ToDecimal(f),
                int i => i,
                long l => l,
                _ => MoneyInputHelper.Parse(o.ToString())
            };
        }
    }
}
