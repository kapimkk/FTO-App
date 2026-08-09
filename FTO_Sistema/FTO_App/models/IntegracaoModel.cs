namespace FTO_App.Models
{
    public class IntegracaoModel
    {
        public long Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = "NFe"; // NFe, CEP, Outro
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Observacao { get; set; } = string.Empty;
        public int Ativo { get; set; } = 1;

        public string StatusExibicao => Ativo == 1 ? "Ativa" : "Inativa";
        public string ApiKeyMascarada =>
            string.IsNullOrWhiteSpace(ApiKey) ? "" :
            ApiKey.Length <= 8 ? "••••••••" :
            $"{ApiKey[..4]}…{ApiKey[^4..]}";
    }
}
