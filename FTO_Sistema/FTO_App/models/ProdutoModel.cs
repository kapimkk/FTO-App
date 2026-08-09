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

        // Tributação / impostos do item
        public string Ncm { get; set; } = string.Empty;
        public string Cest { get; set; } = string.Empty;
        public string Cfop { get; set; } = string.Empty;
        public string Origem { get; set; } = "0";
        public string Csosn { get; set; } = string.Empty;
        public string CstIcms { get; set; } = string.Empty;
        public decimal IcmsAliquota { get; set; }
        public string PisCst { get; set; } = string.Empty;
        public decimal PisAliquota { get; set; }
        public string CofinsCst { get; set; } = string.Empty;
        public decimal CofinsAliquota { get; set; }
        public string CodigoBeneficio { get; set; } = string.Empty;
        public string InfAdicionais { get; set; } = string.Empty;

        // IBS / CBS (reforma)
        public string CstIbsCbs { get; set; } = "000";
        public string ClassTrib { get; set; } = "000001";
        public decimal CbsAliquota { get; set; }
        public decimal IbsAliquota { get; set; }
        public decimal IbsCbsReducao { get; set; }

        public decimal Margem => PrecoVenda > 0 ? ((PrecoVenda - CustoProduto) / PrecoVenda) * 100 : 0;
        public decimal ValorEstoque => CustoProduto * Quantidade;

        public string PrecoFormatado => PrecoVenda.ToString("C2");
        public string CustoFormatado => CustoProduto.ToString("C2");
        public string MargemFormatada => Margem.ToString("N1") + "%";
        public string ValorEstoqueFormatado => ValorEstoque.ToString("C2");
        public string DataCadastroFormatada => DataCadastro.ToString("dd/MM/yyyy");
        public string StatusAtivo => Ativo == 1 ? "Ativo" : "Inativo";
        public string NcmExibicao => string.IsNullOrWhiteSpace(Ncm) ? "—" : Ncm;
    }
}
