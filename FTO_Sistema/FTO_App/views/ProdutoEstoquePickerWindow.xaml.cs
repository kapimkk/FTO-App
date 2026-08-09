using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using FTO_App.Models;
using FTO_App.Services;
using Npgsql;

namespace FTO_App.Views
{
    public partial class ProdutoEstoquePickerWindow : Window
    {
        public ProdutoModel? ProdutoSelecionado { get; private set; }

        public ProdutoEstoquePickerWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => Carregar();
        }

        private void TxtBusca_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Carregar();
            }
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e) => Carregar();

        private void Carregar()
        {
            string q = (TxtBusca.Text ?? "").Trim();
            var list = new List<ProdutoModel>();

            try
            {
                using var conn = Database.GetConnection();
                string sql = @"SELECT * FROM Produtos
                    WHERE Ativo = 1 AND Quantidade > 0
                      AND (@q = '' OR Nome ILIKE @like OR CodigoBarras ILIKE @like OR Categoria ILIKE @like OR COALESCE(Ncm,'') ILIKE @like)
                    ORDER BY Nome
                    LIMIT 200";
                using var cmd = Database.Cmd(conn, sql);
                cmd.Parameters.AddWithValue("@q", q);
                cmd.Parameters.AddWithValue("@like", $"%{q}%");
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(Map(r));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar estoque: {ex.Message}", "Produto",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            GridProdutos.ItemsSource = list;
        }

        private void BtnUsar_Click(object sender, RoutedEventArgs e) => Confirmar();

        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Confirmar();

        private void Confirmar()
        {
            if (GridProdutos.SelectedItem is not ProdutoModel p)
            {
                MessageBox.Show("Selecione um produto com estoque.", "Produto",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ProdutoSelecionado = p;
            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static ProdutoModel Map(NpgsqlDataReader r)
        {
            static string Str(NpgsqlDataReader rd, string col, string def = "")
            {
                var v = Database.FieldOrDbNull(rd, col);
                return v == null || v == DBNull.Value ? def : v.ToString()?.Trim() ?? def;
            }

            return new ProdutoModel
            {
                Id = Convert.ToInt64(Database.FieldOrDbNull(r, "Id")),
                CodigoBarras = Str(r, "CodigoBarras"),
                Nome = Str(r, "Nome"),
                Descricao = Str(r, "Descricao"),
                PrecoVenda = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "PrecoVenda")),
                CustoProduto = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "CustoProduto")),
                Quantidade = Database.FieldOrDbNull(r, "Quantidade") != DBNull.Value
                    ? Convert.ToInt32(Database.FieldOrDbNull(r, "Quantidade")) : 0,
                Categoria = Str(r, "Categoria"),
                Unidade = Str(r, "Unidade", "UN"),
                Ativo = Database.FieldOrDbNull(r, "Ativo") != DBNull.Value
                    ? Convert.ToInt32(Database.FieldOrDbNull(r, "Ativo")) : 1,
                Ncm = Str(r, "Ncm"),
                Cest = Str(r, "Cest"),
                Cfop = Str(r, "Cfop"),
                Origem = Str(r, "Origem", "0"),
                Csosn = Str(r, "Csosn"),
                CstIcms = Str(r, "CstIcms"),
                IcmsAliquota = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "IcmsAliquota")),
                PisCst = Str(r, "PisCst"),
                PisAliquota = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "PisAliquota")),
                CofinsCst = Str(r, "CofinsCst"),
                CofinsAliquota = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "CofinsAliquota")),
                CodigoBeneficio = Str(r, "CodigoBeneficio"),
                InfAdicionais = Str(r, "InfAdicionais"),
                CstIbsCbs = Str(r, "CstIbsCbs", "000"),
                ClassTrib = Str(r, "ClassTrib", "000001"),
                CbsAliquota = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "CbsAliquota")),
                IbsAliquota = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "IbsAliquota")),
                IbsCbsReducao = MoneyInputHelper.ParseDb(Database.FieldOrDbNull(r, "IbsCbsReducao"))
            };
        }
    }
}
