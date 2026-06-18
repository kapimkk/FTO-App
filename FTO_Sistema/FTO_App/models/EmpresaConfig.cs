namespace FTO_App.Models
{
    /// <summary>Dados da empresa carregados do arquivo .env (fora do código-fonte).</summary>
    public class EmpresaConfig
    {
        public string Nome { get; set; } = string.Empty;
        public string Subtitulo { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Ie { get; set; } = string.Empty;
        public string CupomTitulo { get; set; } = "CUPOM NAO FISCAL";
        public string CupomRodape { get; set; } = string.Empty;

        public string TelefoneExibicao =>
            string.IsNullOrWhiteSpace(Telefone) ? string.Empty : $"Tel: {Telefone}";

        public string CnpjExibicao =>
            string.IsNullOrWhiteSpace(Cnpj) ? string.Empty : $"CNPJ: {Cnpj}";

        public string IeExibicao =>
            string.IsNullOrWhiteSpace(Ie) ? string.Empty : $"IE: {Ie}";
    }
}
