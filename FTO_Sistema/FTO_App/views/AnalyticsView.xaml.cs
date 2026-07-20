using FTO_App.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FTO_App.Views
{
    public partial class AnalyticsView : UserControl
    {
        public event EventHandler OnBackRequest;

        // Cores do gráfico de pizza
        private static readonly string[] PizzaColors =
        {
            "#0b3d91", "#FFD700", "#2E7D32", "#C62828",
            "#FF6F00", "#6A1B9A", "#00838F", "#4E342E"
        };

        public AnalyticsView()
        {
            InitializeComponent();
            PopulateFilters();
            Loaded += (s, e) => LoadAllData();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => OnBackRequest?.Invoke(this, EventArgs.Empty);

        private void BtnAtualizar_Click(object sender, RoutedEventArgs e) => LoadAllData();
        private void BtnAplicarFiltros_Click(object sender, RoutedEventArgs e) => LoadAllData();

        // ─── Filtros ──────────────────────────────────────────────────────
        private void PopulateFilters()
        {
            CbFiltroMes.Items.Clear();
            CbFiltroMes.Items.Add("Todos os Meses");
            string[] meses = { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
                                "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };
            foreach (var m in meses) CbFiltroMes.Items.Add(m);
            CbFiltroMes.SelectedIndex = 0;

            CbFiltroAno.Items.Clear();
            CbFiltroAno.Items.Add("Todos os Anos");
            for (int y = 2025; y <= 2060; y++) CbFiltroAno.Items.Add(y.ToString());
            CbFiltroAno.SelectedIndex = 0;

            CbFiltroFormaPag.Items.Clear();
            CbFiltroFormaPag.Items.Add("Todas as Formas");
            CbFiltroFormaPag.Items.Add("Pix");
            CbFiltroFormaPag.Items.Add("Cartão");
            CbFiltroFormaPag.Items.Add("Dinheiro");
            CbFiltroFormaPag.Items.Add("Boleto");
            CbFiltroFormaPag.SelectedIndex = 0;
        }

        private string BuildWhere()
        {
            var parts = new List<string> { "1=1" };

            if (CbFiltroMes?.SelectedIndex > 0)
            {
                string m = CbFiltroMes.SelectedIndex.ToString("00");
                parts.Add($"(strftime('%m', Data) = '{m}' OR substr(Data,6,2) = '{m}')");
            }
            if (CbFiltroAno?.SelectedIndex > 0)
            {
                string y = CbFiltroAno.SelectedItem?.ToString() ?? "";
                if (!string.IsNullOrEmpty(y))
                    parts.Add($"(strftime('%Y', Data) = '{y}' OR substr(Data,1,4) = '{y}')");
            }
            if (CbFiltroFormaPag?.SelectedIndex > 0)
            {
                string fp = CbFiltroFormaPag.SelectedItem?.ToString() ?? "";
                if (!string.IsNullOrEmpty(fp))
                    parts.Add($"FormaPag = '{fp.Replace("'", "''")}'");
            }

            return "WHERE " + string.Join(" AND ", parts);
        }

        // ─── Carregamento principal ────────────────────────────────────────
        private void LoadAllData()
        {
            try
            {
                string where = BuildWhere();
                LoadKPIs(where);
                LoadChartVendasMes();
                LoadChartFormaPag(where);
                LoadTopClientes(where);
                LoadVendasRecentes(where);
                LoadEstoqueResumo();
                LblUltimaAtualizacao.Text = $"Última atualização: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAllData error: {ex.Message}");
            }
        }

        // ─── KPIs ─────────────────────────────────────────────────────────
        private void LoadKPIs(string where)
        {
            try
            {
                using var conn = Database.GetConnection();

                // Totais
                using var cmd = new SQLiteCommand($"SELECT Venda, Gastos, Pago FROM Vendas {where}", conn);
                using var r = cmd.ExecuteReader();

                decimal totalVendas = 0, totalGastos = 0, totalPagas = 0, totalAberto = 0;
                int qtdTotal = 0, qtdPagas = 0, qtdAberto = 0;

                while (r.Read())
                {
                    decimal v = ParseDec(r["Venda"]);
                    decimal g = ParseDec(r["Gastos"]);
                    string pago = r["Pago"]?.ToString() ?? "";
                    totalVendas += v;
                    totalGastos += g;
                    qtdTotal++;
                    if (pago == "Pago") { totalPagas += v; qtdPagas++; }
                    else { totalAberto += v; qtdAberto++; }
                }

                decimal lucro = totalVendas - totalGastos;
                decimal margem = totalVendas > 0 ? (lucro / totalVendas) * 100 : 0;
                decimal ticket = qtdTotal > 0 ? totalVendas / qtdTotal : 0;

                KpiVendas.Text = totalVendas.ToString("C2");
                KpiVendasQtd.Text = $"{qtdTotal} registros";
                KpiGastos.Text = totalGastos.ToString("C2");
                KpiLucro.Text = lucro.ToString("C2");
                KpiLucro.Foreground = lucro >= 0
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
                KpiMargemPct.Text = $"margem {margem:N1}%";
                KpiTicket.Text = ticket.ToString("C2");
                KpiPagas.Text = totalPagas.ToString("C2");
                KpiPagasQtd.Text = $"{qtdPagas} registros";
                KpiAberto.Text = totalAberto.ToString("C2");
                KpiAbertoQtd.Text = $"{qtdAberto} registros";
            }
            catch (Exception ex) { MessageBox.Show($"Erro nos KPIs: {ex.Message}"); }
        }

        // ─── Gráfico de Barras: Vendas por Mês ────────────────────────────
        private void LoadChartVendasMes()
        {
            try
            {
                using var conn = Database.GetConnection();

                string yearFilter = "";
                if (CbFiltroAno?.SelectedIndex > 0)
                {
                    string y = CbFiltroAno.SelectedItem?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(y))
                        yearFilter = $"WHERE (strftime('%Y', Data) = '{y}' OR substr(Data,1,4) = '{y}')";
                }

                // Lê linha por linha e soma em C# com ParseDec (correto para qualquer formato de texto)
                using var cmd = new SQLiteCommand(
                    $"SELECT substr(Data,1,7) as Mes, Venda FROM Vendas {yearFilter} ORDER BY Mes", conn);
                using var r = cmd.ExecuteReader();

                var dados = new Dictionary<string, decimal>();
                while (r.Read())
                {
                    string mes = r["Mes"]?.ToString() ?? "";
                    decimal val = ParseDec(r["Venda"]);
                    if (string.IsNullOrEmpty(mes)) continue;
                    if (dados.ContainsKey(mes)) dados[mes] += val;
                    else dados[mes] = val;
                }

                if (dados.Count == 0) { ChartVendasMes.ItemsSource = null; return; }

                double maxVal = (double)dados.Values.Max();
                double chartHeight = 160.0;
                string[] nomeMeses = { "", "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };

                var items = dados.Select(kv =>
                {
                    string[] parts = kv.Key.Split('-');
                    string label = parts.Length >= 2 && int.TryParse(parts[1], out int mi) && mi >= 1 && mi <= 12
                        ? $"{nomeMeses[mi]}\n{parts[0]}" : kv.Key;

                    // Escala logarítmica: evita que barras colapsem quando há outliers grandes
                    double logMax = Math.Log10(Math.Max(1, maxVal));
                    double logVal = Math.Log10(Math.Max(1, (double)kv.Value));
                    double barH = logMax > 0 ? (logVal / logMax) * chartHeight : 4;

                    // Label abreviado para caber nas barras
                    string valorLabel = FormatValorAbreviado(kv.Value);

                    return new { Label = label, ValorLabel = valorLabel, BarHeight = Math.Max(6, barH) };
                }).ToList();

                ChartVendasMes.ItemsSource = items;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Chart error: {ex.Message}"); }
        }

        // Formata valor de forma abreviada: R$ 290M, R$ 1,3K, R$ 850
        private static string FormatValorAbreviado(decimal valor)
        {
            if (valor >= 1_000_000m) return $"R$ {valor / 1_000_000m:N1}M";
            if (valor >= 1_000m)    return $"R$ {valor / 1_000m:N1}K";
            return valor.ToString("C0");
        }

        // ─── Gráfico de Pizza: Formas de Pagamento ────────────────────────
        private void LoadChartFormaPag(string where)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = new SQLiteCommand(
                    $"SELECT FormaPag, SUM(CAST(Venda AS REAL)) as Total FROM Vendas {where} GROUP BY FormaPag ORDER BY Total DESC", conn);
                using var r = cmd.ExecuteReader();

                var dados = new List<(string Label, decimal Total)>();
                while (r.Read())
                {
                    string fp = r["FormaPag"]?.ToString() ?? "Outros";
                    if (string.IsNullOrWhiteSpace(fp)) fp = "Outros";
                    dados.Add((fp, ParseDec(r["Total"])));
                }

                CanvasPizza.Children.Clear();
                LegendaPizza.ItemsSource = null;

                if (dados.Count == 0) return;

                decimal grand = dados.Sum(d => d.Total);
                if (grand == 0) return;

                double cx = 60, cy = 60, radius = 55;
                double startAngle = -90.0;
                var legendaItems = new List<object>();

                for (int i = 0; i < dados.Count; i++)
                {
                    decimal pct = dados[i].Total / grand;
                    double sweep = (double)pct * 360.0;
                    string cor = PizzaColors[i % PizzaColors.Length];
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cor));

                    if (dados.Count == 1 || Math.Abs(sweep - 360) < 0.01)
                    {
                        var ellipse = new System.Windows.Shapes.Ellipse
                        {
                            Width = radius * 2, Height = radius * 2,
                            Fill = brush
                        };
                        Canvas.SetLeft(ellipse, cx - radius);
                        Canvas.SetTop(ellipse, cy - radius);
                        CanvasPizza.Children.Add(ellipse);
                    }
                    else
                    {
                        double endAngle = startAngle + sweep;
                        var path = new System.Windows.Shapes.Path
                        {
                            Fill = brush,
                            Stroke = Brushes.White,
                            StrokeThickness = 1.5
                        };

                        double rad1 = startAngle * Math.PI / 180.0;
                        double rad2 = endAngle * Math.PI / 180.0;
                        double x1 = cx + radius * Math.Cos(rad1);
                        double y1 = cy + radius * Math.Sin(rad1);
                        double x2 = cx + radius * Math.Cos(rad2);
                        double y2 = cy + radius * Math.Sin(rad2);
                        bool largeArc = sweep > 180;

                        var geo = new PathGeometry();
                        var fig = new PathFigure { StartPoint = new Point(cx, cy) };
                        fig.Segments.Add(new LineSegment(new Point(x1, y1), true));
                        fig.Segments.Add(new ArcSegment(new Point(x2, y2), new Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true));
                        fig.IsClosed = true;
                        geo.Figures.Add(fig);
                        path.Data = geo;
                        CanvasPizza.Children.Add(path);
                    }

                    legendaItems.Add(new
                    {
                        Cor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cor)),
                        Label = dados[i].Label,
                        PctLabel = $"({pct:P0})"
                    });

                    startAngle += sweep;
                }

                LegendaPizza.ItemsSource = legendaItems;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Pizza error: {ex.Message}"); }
        }

        // ─── Top Clientes ─────────────────────────────────────────────────
        private void LoadTopClientes(string where)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = new SQLiteCommand(
                    $"SELECT Cliente, SUM(CAST(Venda AS REAL)) as Total FROM Vendas {where} GROUP BY Cliente ORDER BY Total DESC LIMIT 5", conn);
                using var r = cmd.ExecuteReader();

                var items = new List<object>();
                int rank = 1;
                string[] medals = { "🥇", "🥈", "🥉", "4°", "5°" };
                while (r.Read())
                {
                    items.Add(new
                    {
                        Rank = medals[rank - 1],
                        Cliente = r["Cliente"]?.ToString() ?? "",
                        ValorFormatado = ParseDec(r["Total"]).ToString("C2")
                    });
                    rank++;
                }
                ListTopClientes.ItemsSource = items;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"TopClientes error: {ex.Message}"); }
        }

        // ─── Vendas Recentes ──────────────────────────────────────────────
        private void LoadVendasRecentes(string where)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = new SQLiteCommand(
                    $"SELECT * FROM Vendas {where} ORDER BY Data DESC, Id DESC LIMIT 10", conn);
                using var r = cmd.ExecuteReader();

                var list = new List<Venda>();
                while (r.Read())
                {
                    list.Add(new Venda
                    {
                        Id = Convert.ToInt64(r["Id"]),
                        Cliente = r["Cliente"]?.ToString() ?? "",
                        Data = ParseDate(r["Data"]),
                        VendaValor = ParseDec(r["Venda"]),
                        Gastos = ParseDec(r["Gastos"]),
                        TipoServico = r["TipoServico"]?.ToString() ?? "",
                        Pago = r["Pago"]?.ToString() ?? ""
                    });
                }
                GridRecentes.ItemsSource = list;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Recentes error: {ex.Message}"); }
        }

        // ─── Resumo Estoque ───────────────────────────────────────────────
        private void LoadEstoqueResumo()
        {
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = new SQLiteCommand(
                    "SELECT COUNT(*) as TotalProd, SUM(Quantidade) as TotalItens, SUM(CustoProduto * Quantidade) as ValorTotal, SUM(CASE WHEN Quantidade = 0 THEN 1 ELSE 0 END) as SemEstoque FROM Produtos WHERE Ativo = 1", conn);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    EstTotalProd.Text = r["TotalProd"]?.ToString() ?? "0";
                    EstTotalItens.Text = r["TotalItens"] != DBNull.Value ? r["TotalItens"].ToString() : "0";
                    EstValorTotal.Text = ParseDec(r["ValorTotal"]).ToString("C2");
                    EstSemEstoque.Text = r["SemEstoque"]?.ToString() ?? "0";
                }
            }
            catch { EstTotalProd.Text = "—"; EstTotalItens.Text = "—"; EstValorTotal.Text = "—"; EstSemEstoque.Text = "—"; }
        }

        // ─── Helpers ──────────────────────────────────────────────────────
        private decimal ParseDec(object? o)
        {
            if (o == null || o == DBNull.Value) return 0;
            if (decimal.TryParse(o.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v)) return v;
            return 0;
        }

        private DateTime ParseDate(object? o)
        {
            if (o == null || o == DBNull.Value) return DateTime.Today;
            string s = o.ToString() ?? "";
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)) return d;
            if (DateTime.TryParse(s, out DateTime d2)) return d2;
            return DateTime.Today;
        }
    }
}
