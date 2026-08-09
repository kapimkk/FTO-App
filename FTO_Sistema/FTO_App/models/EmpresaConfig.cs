namespace FTO_App.Models
{
    /// <summary>Dados da empresa e configuração fiscal (persistidos no PostgreSQL).</summary>
    public class EmpresaConfig
    {
        public string Nome { get; set; } = string.Empty;
        public string Subtitulo { get; set; } = string.Empty;
        public string RazaoSocial { get; set; } = string.Empty;
        public string NomeFantasia { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Complemento { get; set; } = string.Empty;
        public string Bairro { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
        public string Cep { get; set; } = string.Empty;
        public string CodigoIbge { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Ie { get; set; } = string.Empty;
        public string Im { get; set; } = string.Empty;
        public string Cnae { get; set; } = string.Empty;
        public string RegimeTributario { get; set; } = "1"; // 1=Simples, 2=Simples excesso, 3=Normal
        public string AmbienteNfe { get; set; } = "2"; // 1=Produção, 2=Homologação
        public string SerieNfe { get; set; } = "1";
        public string UltimoNumeroNfe { get; set; } = "0";
        /// <summary>CSC (Código de Segurança do Contribuinte) para NFC-e — par usado quando a nota está em Homologação (tpAmb=2).</summary>
        public string CscIdHomologacao { get; set; } = string.Empty;
        public string CscTokenHomologacao { get; set; } = string.Empty;
        /// <summary>CSC de Produção (tpAmb=1) — gerado separadamente no portal da SEFAZ/API Fiscal; nunca é o mesmo token da Homologação.</summary>
        public string CscIdProducao { get; set; } = string.Empty;
        public string CscTokenProducao { get; set; } = string.Empty;
        public string CertificadoPath { get; set; } = string.Empty;
        public string LogoPath { get; set; } = string.Empty;
        public string CupomTitulo { get; set; } = "Comprovante de Vendas";
        public string CupomRodape { get; set; } = string.Empty;

        // Reforma tributária — IBS / CBS (LC 214/2025)
        /// <summary>teste2026 | projetado | personalizado</summary>
        public string IbsCbsPreset { get; set; } = "teste2026";
        public bool IbsCbsCalculoAutomatico { get; set; } = true;
        public bool IbsCbsDestaqueObrigatorio { get; set; } = true;
        public decimal CbsAliquota { get; set; } = 0.9m;
        public decimal IbsAliquota { get; set; } = 0.1m;
        public decimal IbsAliquotaUf { get; set; } = 0.1m;
        public decimal IbsAliquotaMun { get; set; } = 0m;

        // Integração com a API Fiscal (PFCode) — NF-e/NFC-e via requisições HTTP
        public string FiscalApiUrlNfe { get; set; } = "http://localhost:5001";
        public string FiscalApiUrlNfce { get; set; } = "http://localhost:5002";
        /// <summary>URL base do microsserviço Fiscal.NFSe.API (padrão nacional, porta 5003).</summary>
        public string FiscalApiUrlNfse { get; set; } = "http://localhost:5003";
        /// <summary>Persistida criptografada (DPAPI) — mesma técnica do CscToken.</summary>
        public string FiscalApiKey { get; set; } = string.Empty;
        /// <summary>Série da DPS (NFS-e).</summary>
        public string SerieNfse { get; set; } = "1";
        /// <summary>Último número de DPS emitido (controle local).</summary>
        public string UltimoNumeroNfse { get; set; } = "0";

        /// <summary>Retorna o par (idCSC, CSC) correto para o ambiente informado ("1"=Produção, demais=Homologação).</summary>
        public (string CscId, string CscToken) ObterCsc(string? ambiente)
        {
            bool producao = (ambiente ?? "").Trim() == "1";
            return producao
                ? (CscIdProducao?.Trim() ?? "", CscTokenProducao?.Trim() ?? "")
                : (CscIdHomologacao?.Trim() ?? "", CscTokenHomologacao?.Trim() ?? "");
        }

        public string TelefoneExibicao =>
            string.IsNullOrWhiteSpace(Telefone) ? string.Empty : $"Tel: {Telefone}";

        public string CnpjExibicao =>
            string.IsNullOrWhiteSpace(Cnpj) ? string.Empty : $"CNPJ: {Cnpj}";

        public string IeExibicao =>
            string.IsNullOrWhiteSpace(Ie) ? string.Empty : $"IE: {Ie}";
    }
}
