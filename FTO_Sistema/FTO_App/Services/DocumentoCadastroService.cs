using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FTO_App.Services
{
    /// <summary>
    /// Consulta CNPJ com base nos Dados Abertos da Receita Federal
    /// (API MinhaReceita — espelho público da base oficial).
    /// Em caso de indisponibilidade/429, tenta fallback em CNPJ.ws pública.
    /// Não consulta CPF.
    /// </summary>
    public static class DocumentoCadastroService
    {
        private static readonly HttpClient Http = CriarHttp();

        private static HttpClient CriarHttp()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FTO-App/1.0 (consulta-cnpj)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        public sealed class Resultado
        {
            public bool Sucesso { get; init; }
            public string? Erro { get; init; }
            public string Fonte { get; init; } = "";
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
            public string Tipo { get; init; } = "J";
        }

        /// <summary>Consulta apenas CNPJ (14 dígitos). CPF não é suportado.</summary>
        public static async Task<Resultado> BuscarAsync(string? cpfCnpj)
            => await BuscarCnpjAsync(cpfCnpj).ConfigureAwait(false);

        public static async Task<Resultado> BuscarCnpjAsync(string? cnpj)
        {
            string digits = DocumentValidator.OnlyDigits(cnpj);

            if (digits.Length == 11)
            {
                return new Resultado
                {
                    Sucesso = false,
                    Tipo = "F",
                    Erro = "A consulta automática é apenas para CNPJ. Informe o nome do CPF manualmente."
                };
            }

            if (digits.Length != 14 || !DocumentValidator.IsValidCnpj(digits))
            {
                return new Resultado
                {
                    Sucesso = false,
                    Erro = "Informe um CNPJ válido (14 dígitos) para consultar os Dados Abertos da Receita Federal."
                };
            }

            // 1) MinhaReceita = Dados Abertos RFB (oficial público)
            var primaria = await ConsultarMinhaReceitaAsync(digits).ConfigureAwait(false);
            if (primaria.Sucesso) return primaria;

            // 2) Fallback se 429 / indisponível
            bool tentarFallback = primaria.Erro?.Contains("429", StringComparison.Ordinal) == true
                                  || primaria.Erro?.Contains("indispon", StringComparison.OrdinalIgnoreCase) == true
                                  || primaria.Erro?.Contains("Tempo esgotado", StringComparison.OrdinalIgnoreCase) == true
                                  || primaria.Erro?.Contains("HTTP 5", StringComparison.Ordinal) == true;

            if (tentarFallback)
            {
                var fallback = await ConsultarCnpjWsAsync(digits).ConfigureAwait(false);
                if (fallback.Sucesso) return fallback;
                return new Resultado
                {
                    Sucesso = false,
                    Tipo = "J",
                    Erro = primaria.Erro + (string.IsNullOrWhiteSpace(fallback.Erro) ? "" : $" | Fallback: {fallback.Erro}")
                };
            }

            return primaria;
        }

        private static async Task<Resultado> ConsultarMinhaReceitaAsync(string digits)
        {
            try
            {
                // Retry único em 429 (rate limit)
                for (int tentativa = 0; tentativa < 2; tentativa++)
                {
                    if (tentativa > 0)
                        await Task.Delay(1600).ConfigureAwait(false);

                    string url = $"https://minhareceita.org/{digits}";
                    using var resp = await Http.GetAsync(url).ConfigureAwait(false);

                    if (resp.StatusCode == HttpStatusCode.NotFound)
                        return new Resultado { Sucesso = false, Tipo = "J", Erro = "CNPJ não encontrado na base da Receita Federal." };

                    if ((int)resp.StatusCode == 429)
                    {
                        if (tentativa == 0) continue;
                        return new Resultado
                        {
                            Sucesso = false,
                            Tipo = "J",
                            Erro = "Limite temporário de consultas (HTTP 429). Aguarde alguns segundos e tente novamente."
                        };
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        return new Resultado
                        {
                            Sucesso = false,
                            Tipo = "J",
                            Erro = $"Serviço da Receita indisponível (HTTP {(int)resp.StatusCode})."
                        };
                    }

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
                    string? ibge = GetAny(root, "codigo_municipio_ibge");

                    if (string.IsNullOrWhiteSpace(razao) && string.IsNullOrWhiteSpace(fantasia))
                        return new Resultado { Sucesso = false, Tipo = "J", Erro = "CNPJ encontrado, mas sem razão social na resposta." };

                    return new Resultado
                    {
                        Sucesso = true,
                        Tipo = "J",
                        Fonte = "Receita Federal (Dados Abertos / MinhaReceita)",
                        Nome = string.IsNullOrWhiteSpace(razao) ? fantasia : razao,
                        NomeFantasia = fantasia,
                        Logradouro = logradouro,
                        Numero = numero,
                        Complemento = complemento,
                        Bairro = bairro,
                        Municipio = municipio,
                        Uf = uf,
                        Cep = FormatCep(cep),
                        CodigoIbge = ibge
                    };
                }

                return new Resultado { Sucesso = false, Tipo = "J", Erro = "Não foi possível consultar o CNPJ." };
            }
            catch (TaskCanceledException)
            {
                return new Resultado { Sucesso = false, Tipo = "J", Erro = "Tempo esgotado ao consultar o CNPJ na Receita Federal." };
            }
            catch (Exception ex)
            {
                return new Resultado { Sucesso = false, Tipo = "J", Erro = $"Erro ao consultar CNPJ: {ex.Message}" };
            }
        }

        private static async Task<Resultado> ConsultarCnpjWsAsync(string digits)
        {
            try
            {
                string url = $"https://publica.cnpj.ws/cnpj/{digits}";
                using var resp = await Http.GetAsync(url).ConfigureAwait(false);
                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return new Resultado { Sucesso = false, Tipo = "J", Erro = "CNPJ não encontrado (fallback)." };
                if ((int)resp.StatusCode == 429)
                    return new Resultado { Sucesso = false, Tipo = "J", Erro = "HTTP 429 no fallback — aguarde e tente de novo." };
                if (!resp.IsSuccessStatusCode)
                    return new Resultado { Sucesso = false, Tipo = "J", Erro = $"Fallback HTTP {(int)resp.StatusCode}." };

                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string razao = GetStr(root, "razao_social");
                string fantasia = "";
                string logradouro = "";
                string numero = "";
                string complemento = "";
                string bairro = "";
                string municipio = "";
                string uf = "";
                string cep = "";
                string? ibge = null;

                if (root.TryGetProperty("estabelecimento", out var est) && est.ValueKind == JsonValueKind.Object)
                {
                    fantasia = GetStr(est, "nome_fantasia");
                    string tipoLog = GetStr(est, "tipo_logradouro");
                    logradouro = Join(" ", tipoLog, GetStr(est, "logradouro")).Trim();
                    numero = GetStr(est, "numero");
                    complemento = GetStr(est, "complemento");
                    bairro = GetStr(est, "bairro");
                    cep = DocumentValidator.OnlyDigits(GetStr(est, "cep"));
                    if (est.TryGetProperty("cidade", out var cidade) && cidade.ValueKind == JsonValueKind.Object)
                    {
                        municipio = GetStr(cidade, "nome");
                        ibge = GetAny(cidade, "ibge_id");
                    }
                    if (est.TryGetProperty("estado", out var estado) && estado.ValueKind == JsonValueKind.Object)
                        uf = GetStr(estado, "sigla");
                }

                if (string.IsNullOrWhiteSpace(razao) && string.IsNullOrWhiteSpace(fantasia))
                    return new Resultado { Sucesso = false, Tipo = "J", Erro = "Fallback sem razão social." };

                return new Resultado
                {
                    Sucesso = true,
                    Tipo = "J",
                    Fonte = "CNPJ.ws (fallback)",
                    Nome = string.IsNullOrWhiteSpace(razao) ? fantasia : razao,
                    NomeFantasia = fantasia,
                    Logradouro = logradouro,
                    Numero = numero,
                    Complemento = complemento,
                    Bairro = bairro,
                    Municipio = municipio,
                    Uf = uf,
                    Cep = FormatCep(cep),
                    CodigoIbge = ibge
                };
            }
            catch (Exception ex)
            {
                return new Resultado { Sucesso = false, Tipo = "J", Erro = ex.Message };
            }
        }

        private static string FormatCep(string cep) =>
            cep.Length == 8 ? $"{cep[..4]}-{cep[4..]}" : cep;

        private static string GetStr(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
                return "";
            return el.ValueKind switch
            {
                JsonValueKind.String => (el.GetString() ?? "").Trim(),
                JsonValueKind.Number => el.ToString(),
                _ => ""
            };
        }

        private static string? GetAny(JsonElement root, string name)
        {
            string s = GetStr(root, name);
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static string Join(string sep, params string[] parts)
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var p in parts)
                if (!string.IsNullOrWhiteSpace(p)) list.Add(p.Trim());
            return string.Join(sep, list);
        }
    }
}
