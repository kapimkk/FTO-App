using System;

namespace FTO_App.Models
{
    public class ProdutoModel
    {
        public long Id { get; set; }
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public decimal PrecoVenda { get; set; }
        public decimal CustoProduto { get; set; }
        public int Quantidade { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string Unidade { get; set; } = "UN";
        public int Ativo { get; set; } = 1;
        public DateTime DataCadastro { get; set; } = DateTime.Today;

        public decimal Margem => PrecoVenda > 0 ? ((PrecoVenda - CustoProduto) / PrecoVenda) * 100 : 0;
        public decimal ValorEstoque => CustoProduto * Quantidade;

        public string PrecoFormatado => PrecoVenda.ToString("C2");
        public string CustoFormatado => CustoProduto.ToString("C2");
        public string MargemFormatada => Margem.ToString("N1") + "%";
        public string ValorEstoqueFormatado => ValorEstoque.ToString("C2");
        public string DataCadastroFormatada => DataCadastro.ToString("dd/MM/yyyy");
        public string StatusAtivo => Ativo == 1 ? "Ativo" : "Inativo";
    }
}
