using System;

namespace FTO_App.Models
{
    /// <summary>Rascunho / NFS-e (Padrão Nacional — DPS) persistida localmente.</summary>
    public class NotaServicoModel
    {
        public long Id { get; set; }
        public string Ambiente { get; set; } = "2"; // 1=Prod 2=Homol
        public string Serie { get; set; } = "1";
        public long NumeroDps { get; set; } = 1;
        public DateTime DataCompetencia { get; set; } = DateTime.Today;
        public string CodigoIbgeEmissao { get; set; } = "";

        // Tomador
        public string TomadorNome { get; set; } = "";
        public string TomadorCpfCnpj { get; set; } = "";
        public string TomadorEmail { get; set; } = "";
        public string TomadorFone { get; set; } = "";
        public string TomadorLgr { get; set; } = "";
        public string TomadorNro { get; set; } = "";
        public string TomadorBairro { get; set; } = "";
        public string TomadorMun { get; set; } = "";
        public string TomadorUf { get; set; } = "";
        public string TomadorCep { get; set; } = "";
        public string TomadorIbge { get; set; } = "";

        // Serviço
        public string CodTribNac { get; set; } = "010101";
        public string CodTribMun { get; set; } = "";
        public string DescricaoServico { get; set; } = "";
        public string CodNbs { get; set; } = "";
        public string CodIbgePrestacao { get; set; } = "";

        // Valores / ISS
        public decimal ValorServico { get; set; }
        public string TribIssqn { get; set; } = "1"; // 1 Operação tributável
        public string TpRetIssqn { get; set; } = "1"; // 1 Não retido
        public decimal AliquotaIss { get; set; } = 5m;

        // Prestador — regime na DPS (opSimpNac: 1 Não / 2 MEI / 3 ME-EPP)
        public string OpSimpNac { get; set; } = "1";
        /// <summary>Obrigatório se opSimpNac=3 (E0166). 1=SN federal+mun; 2=SN federal / ISS fora; 3=fora do SN.</summary>
        public string RegApTribSN { get; set; } = "1";
        public string RegEspTrib { get; set; } = "0";

        // IBS/CBS (opcional — classificação; SEFIN calcula valores)
        public bool IncluirIbsCbs { get; set; }
        public string CstIbsCbs { get; set; } = "000";
        public string ClassTrib { get; set; } = "000001";
        public string CodIndOp { get; set; } = "000001";

        // Resultado emissão
        public string Status { get; set; } = "Rascunho";
        public string ChaveAcesso { get; set; } = "";
        public string IdDps { get; set; } = "";
        public string DataProcessamento { get; set; } = "";
        public string CStat { get; set; } = "";
        public string XMotivo { get; set; } = "";
        public string XmlEnviado { get; set; } = "";
        public string XmlAutorizado { get; set; } = "";
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        public string NumeroExibicao => $"{Serie}/{NumeroDps}";
        public string ValorFormatado => ValorServico.ToString("C2");
        public string DataCompetenciaFormatada => DataCompetencia.ToString("dd/MM/yyyy");
    }
}
