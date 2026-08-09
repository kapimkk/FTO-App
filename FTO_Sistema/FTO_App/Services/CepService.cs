using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FTO_App.Services
{
    public sealed class CepResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string Cep { get; init; } = "";
        public string Logradouro { get; init; } = "";
        public string Complemento { get; init; } = "";
        public string Bairro { get; init; } = "";
        public string Municipio { get; init; } = "";
        public string Uf { get; init; } = "";
        public string CodigoIbge { get; init; } = "";
    }

    /// <summary>Consulta ViaCEP (viacep.com.br) — sem chave de API.</summary>
    public static class CepService
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        public static async Task<CepResult> BuscarAsync(string? cep)
        {
            string digits = DocumentValidator.OnlyDigits(cep);
            if (digits.Length != 8)
                return new CepResult { Success = false, ErrorMessage = "CEP deve ter 8 dígitos." };

            try
            {
                string url = $"https://viacep.com.br/ws/{digits}/json/";
                using var resp = await Http.GetAsync(url).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    return new CepResult { Success = false, ErrorMessage = "Falha ao consultar CEP." };

                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("erro", out var erro) &&
                    (erro.ValueKind == JsonValueKind.True ||
                     (erro.ValueKind == JsonValueKind.String && erro.GetString() == "true")))
                {
                    return new CepResult { Success = false, ErrorMessage = "CEP não encontrado." };
                }

                return new CepResult
                {
                    Success = true,
                    Cep = FormatCep(digits),
                    Logradouro = Get(root, "logradouro"),
                    Complemento = Get(root, "complemento"),
                    Bairro = Get(root, "bairro"),
                    Municipio = Get(root, "localidade"),
                    Uf = Get(root, "uf"),
                    CodigoIbge = Get(root, "ibge")
                };
            }
            catch (Exception ex)
            {
                return new CepResult { Success = false, ErrorMessage = $"Erro na consulta: {ex.Message}" };
            }
        }

        private static string Get(JsonElement root, string name) =>
            root.TryGetProperty(name, out var p) ? p.GetString()?.Trim() ?? "" : "";

        private static string FormatCep(string d) =>
            d.Length == 8 ? $"{d[..5]}-{d[5..]}" : d;
    }
}
