using System;

namespace FTO_App.Models
{
    /// <summary>Campos do corpo NF-e alinhados ao contrato da API Fiscal (autorização).</summary>
    public class NotaFiscalModel
    {
        public long Id { get; set; }
        public string NaturezaOperacao { get; set; } = "Venda de mercadoria";
        public string Modelo { get; set; } = "55"; // 55=NF-e
        public string Serie { get; set; } = "1";
        public long Numero { get; set; }
        public DateTime DataEmissao { get; set; } = DateTime.Now;
        public string TipoOperacao { get; set; } = "1"; // 0=Entrada, 1=Saída
        public string Finalidade { get; set; } = "1"; // 1=Normal
        public string ConsumidorFinal { get; set; } = "1";
        public string PresencaComprador { get; set; } = "1";
        public string Ambiente { get; set; } = "2"; // 1=Produção, 2=Homologação

        /// <summary>1=Interna, 2=Interestadual, 3=Exterior</summary>
        public string IdDest { get; set; } = "1";

        public long? ClienteId { get; set; }
        public string DestNome { get; set; } = string.Empty;
        public string DestCpfCnpj { get; set; } = string.Empty;
        public string DestIe { get; set; } = string.Empty;
        /// <summary>1=Contribuinte, 2=Isento, 9=Não contribuinte</summary>
        public string IndIEDest { get; set; } = "9";
        public string DestEmail { get; set; } = string.Empty;
        public string DestLogradouro { get; set; } = string.Empty;
        public string DestNumero { get; set; } = string.Empty;
        public string DestComplemento { get; set; } = string.Empty;
        public string DestBairro { get; set; } = string.Empty;
        public string DestMunicipio { get; set; } = string.Empty;
        public string DestUf { get; set; } = string.Empty;
        public string DestCep { get; set; } = string.Empty;
        public string DestCodigoIbge { get; set; } = string.Empty;

        public string ProdutoCodigo { get; set; } = string.Empty;
        public string ProdutoDescricao { get; set; } = string.Empty;
        public string ProdutoNcm { get; set; } = string.Empty;
        public string ProdutoCest { get; set; } = string.Empty;
        public string ProdutoGtin { get; set; } = "SEM GTIN";
        public string ProdutoCfop { get; set; } = "5102";
        public string ProdutoUnidade { get; set; } = "UN";
        public decimal ProdutoQuantidade { get; set; } = 1;
        public decimal ProdutoValorUnitario { get; set; }
        public decimal ProdutoValorTotal { get; set; }

        public string IcmsOrigem { get; set; } = "0";
        public string IcmsCst { get; set; } = "00";
        /// <summary>CSOSN (Simples Nacional) — usado quando CRT ≠ 3</summary>
        public string Csosn { get; set; } = "102";
        public decimal IcmsAliquota { get; set; }
        public decimal IcmsValor { get; set; }
        public string PisCst { get; set; } = "01";
        public decimal PisAliquota { get; set; }
        public decimal PisValor { get; set; }
        public string CofinsCst { get; set; } = "01";
        public decimal CofinsAliquota { get; set; }
        public decimal CofinsValor { get; set; }

        // IBS / CBS
        public string CstIbsCbs { get; set; } = "000";
        public string ClassTrib { get; set; } = "000001";
        public decimal CbsAliquota { get; set; }
        public decimal CbsValor { get; set; }
        public decimal IbsAliquota { get; set; }
        public decimal IbsValor { get; set; }
        public decimal IbsAliquotaUf { get; set; }
        public decimal IbsValorUf { get; set; }
        public decimal IbsAliquotaMun { get; set; }
        public decimal IbsValorMun { get; set; }

        public decimal ValorProdutos { get; set; }
        public decimal ValorFrete { get; set; }
        public decimal ValorDesconto { get; set; }
        public decimal ValorTotalNota { get; set; }

        public string FormaPagamento { get; set; } = "01";
        public string InformacoesComplementares { get; set; } = string.Empty;
        public string Status { get; set; } = "Rascunho";
        public string CaminhoXml { get; set; } = string.Empty;

        // Resultado da última emissão/consulta na API Fiscal (PFCode)
        public string ChaveAcesso { get; set; } = string.Empty;
        public string NProt { get; set; } = string.Empty;
        public string DhRecbto { get; set; } = string.Empty;
        public string CStat { get; set; } = string.Empty;
        public string XMotivo { get; set; } = string.Empty;
        public string MensagemTraduzida { get; set; } = string.Empty;
        public string QrCodeUrl { get; set; } = string.Empty;
        public string XmlAutorizado { get; set; } = string.Empty;

        public bool TemChaveAcesso => !string.IsNullOrWhiteSpace(ChaveAcesso);

        /// <summary>Rótulo amigável do modelo fiscal, usado na grade e nos títulos das janelas.</summary>
        public string ModeloExibicao => "NF-e";

        public string DataEmissaoFormatada => DataEmissao.ToString("dd/MM/yyyy HH:mm");
        public string ValorTotalFormatado => ValorTotalNota.ToString("C2");
        public string NumeroExibicao => $"{Serie}/{Numero}";

        /// <summary>Resumo para a grade: "cStat - xMotivo" quando já houve alguma tentativa de emissão via API.</summary>
        public string SituacaoFiscalExibicao => string.IsNullOrWhiteSpace(CStat)
            ? "—"
            : $"{CStat} - {(string.IsNullOrWhiteSpace(MensagemTraduzida) ? XMotivo : MensagemTraduzida)}";
    }
}
