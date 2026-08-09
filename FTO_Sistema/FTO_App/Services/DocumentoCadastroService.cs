using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FTO_App.Services
{
    /// <summary>
    /// Consulta cadastro público por CNPJ (BrasilAPI) — preenche razão social e endereço.
    /// CPF não possui API pública gratuita estável; nesse caso retorna mensagem orientativa.
    /// </summary>
    public static class DocumentoCadastroService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

        public sealed class Resultado
        {
            public bool Sucesso { get; init; }
            public string? Erro { get; init; }
            public string Nome { get; init; } = "";
            public string NomeFantasia { get; init; } = "";
            public string Logradouro { get; init; } = "";
            public string Numero { get; init; } = "";
            public string Complemento { get; init; } = "";
            public string Bairro { get; init; } = "";
            public string Municipio { get; init; } = "";
            public string Uf { get; init; } = "";
            public string Cep { get; init; } = "";
            public string? CodigoIbge { get; init; }
            public string Tipo { get; init; } = ""; // F | J
        }

        public static async Task<Resultado> BuscarAsync(string? cpfCnpj)
        {
            string digits = DocumentValidator.OnlyDigits(cpfCnpj);
            if (digits.Length == 11)
            {
                return new Resultado
                {
                    Sucesso = false,
                    Tipo = "F",
                    Erro = "Consulta automática de CPF não está disponível (não há API pública gratuita estável). Informe o nome manualmente."
                };
            }

            if (digits.Length != 14 || !DocumentValidator.IsValidCnpj(digits))
            {
                return new Resultado
                {
                    Sucesso = false,
                    Erro = "Informe um CNPJ válido (14 dígitos) para buscar o cadastro na BrasilAPI."
                };
            }

            try
            {
                string url = $"https://brasilapi.com.br/api/cnpj/v1/{digits}";
                using var resp = await Http.GetAsync(url).ConfigureAwait(false);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return new Resultado { Sucesso = false, Tipo = "J", Erro = "CNPJ não encontrado na base da Receita (BrasilAPI)." };
                if (!resp.IsSuccessStatusCode)
                    return new Resultado { Sucesso = false, Tipo = "J", Erro = $"Falha ao consultar CNPJ (HTTP {(int)resp.StatusCode})." };

                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string razao = GetStr(root, "razao_social");
                string fantasia = GetStr(root, "nome_fantasia");
                string logradouro = Join(" ", GetStr(root, "descricao_tipo_de_logradouro"), GetStr(root, "logradouro")).Trim();
                string numero = GetStr(root, "numero");
                string complemento = GetStr(root, "complemento");
                string bairro = GetStr(root, "bairro");
                string municipio = GetStr(root, "municipio");
                string uf = GetStr(root, "uf");
                string cep = DocumentValidator.OnlyDigits(GetStr(root, "cep"));
                string? ibge = root.TryGetProperty("codigo_municipio_ibge", out var ib) && ib.ValueKind != JsonValueKind.Null
                    ? ib.ToString()
                    : null;

                if (string.IsNullOrWhiteSpace(razao) && string.IsNullOrWhiteSpace(fantasia))
                    return new Resultado { Sucesso = false, Tipo = "J", Erro = "CNPJ encontrado, mas sem razão social na resposta." };

                return new Resultado
                {
                    Sucesso = true,
                    Tipo = "J",
                    Nome = string.IsNullOrWhiteSpace(razao) ? fantasia : razao,
                    NomeFantasia = fantasia,
                    Logradouro = logradouro,
                    Numero = numero,
                    Complemento = complemento,
                    Bairro = bairro,
                    Municipio = municipio,
                    Uf = uf,
                    Cep = cep.Length == 8 ? $"{cep[..4]}-{cep[4..]}" : cep,
                    CodigoIbge = ibge
                };
            }
            catch (TaskCanceledException)
            {
                return new Resultado { Sucesso = false, Tipo = "J", Erro = "Tempo esgotado ao consultar o CNPJ." };
            }
            catch (Exception ex)
            {
                return new Resultado { Sucesso = false, Tipo = "J", Erro = $"Erro ao consultar CNPJ: {ex.Message}" };
            }
        }

        private static string GetStr(JsonElement root, string name) =>
            root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                ? (el.GetString() ?? "").Trim()
                : "";

        private static string Join(string sep, params string[] parts)
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var p in parts)
                if (!string.IsNullOrWhiteSpace(p)) list.Add(p.Trim());
            return string.Join(sep, list);
        }
    }
}
