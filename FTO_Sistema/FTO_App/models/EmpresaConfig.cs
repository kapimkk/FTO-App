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

        // Integração com a API Fiscal (PFCode) — NF-e e NFS-e via requisições HTTP
        public string FiscalApiUrlNfe { get; set; } = "http://localhost:5001";
        /// <summary>URL base do microsserviço Fiscal.NFSe.API (padrão nacional, porta 5003).</summary>
        public string FiscalApiUrlNfse { get; set; } = "http://localhost:5003";
        /// <summary>Persistida criptografada (DPAPI).</summary>
        public string FiscalApiKey { get; set; } = string.Empty;
        /// <summary>Série da DPS (NFS-e).</summary>
        public string SerieNfse { get; set; } = "1";
        /// <summary>Último número de DPS emitido (controle local).</summary>
        public string UltimoNumeroNfse { get; set; } = "0";

        // NFS-e — código de tributação nacional (cTribNac, 6 dígitos da lista nacional)
        /// <summary>Código sugerido no cadastro da NFS-e. Ex.: 010701 (serviços de TI).</summary>
        public string NfseCodTribNac { get; set; } = "010101";
        /// <summary>
        /// Se true, o cTribNac da configuração é fixo: o campo fica bloqueado no cadastro
        /// e toda NFS-e é salva com esse código. Se false, cada nota define o seu.
        /// </summary>
        public bool NfseCodTribNacFixo { get; set; }

        // NFS-e — Lei 12.741 (totais aproximados) e opções SEFIN
        /// <summary>% federal aproximado (pTotTribFed). Padrão 13,45.</summary>
        public decimal NfsePTotTribFed { get; set; } = 13.45m;
        /// <summary>% estadual aproximado (pTotTribEst).</summary>
        public decimal NfsePTotTribEst { get; set; } = 0m;
        /// <summary>
        /// % municipal aproximado (pTotTribMun). Se null, usa a alíquota ISS da nota.
        /// </summary>
        public decimal? NfsePTotTribMun { get; set; }
        /// <summary>
        /// Se true, sempre envia pAliq na DPS (municípios não ATIVOS / override).
        /// Se false, aplica regras SEFIN (omite em E0617/E0625).
        /// </summary>
        public bool NfseEnviarPAliq { get; set; }
        /// <summary>
        /// Se true, envia endereço do prestador (aba Empresa) na DPS.
        /// Padrão false — SEFIN E0128 rejeita com tpEmit=1 na maioria dos casos.
        /// </summary>
        public bool NfseEnviarEnderecoPrestador { get; set; }

        public string TelefoneExibicao =>
            string.IsNullOrWhiteSpace(Telefone) ? string.Empty : $"Tel: {Telefone}";

        public string CnpjExibicao =>
            string.IsNullOrWhiteSpace(Cnpj) ? string.Empty : $"CNPJ: {Cnpj}";

        public string IeExibicao =>
            string.IsNullOrWhiteSpace(Ie) ? string.Empty : $"IE: {Ie}";
    }
}
