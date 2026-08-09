using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FTO_App.Models;

namespace FTO_App.Services
{
    /// <summary>
    /// Cliente HTTP da API Fiscal PFCode (Fiscal.NFe.API / Fiscal.NFCe.API) — autenticação por
    /// X-API-Key, um HttpClient por instância do app (reaproveitado, evita esgotamento de sockets).
    /// Nunca lança exceção para quem chama: toda falha (rede, timeout, HTTP 4xx/5xx, JSON malformado)
    /// vira um <see cref="FiscalApiResult{T}"/> com código e mensagem concretos, prontos para exibir.
    /// </summary>
    public static class FiscalApiClient
    {
        private static readonly HttpClient Http = new(new HttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ---------------------------------------------------------------
        // Health check
        // ---------------------------------------------------------------

        public static async Task<FiscalApiResult<string>> TestarConexaoAsync(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return FiscalApiResult<string>.Falha(null, "URL_VAZIA", "Informe a URL base do serviço antes de testar.");

            try
            {
                using var resp = await Http.GetAsync(CombinarUrl(baseUrl, "/health"));
                string body = await resp.Content.ReadAsStringAsync();
                return resp.IsSuccessStatusCode
                    ? FiscalApiResult<string>.Ok(body, (int)resp.StatusCode)
                    : FiscalApiResult<string>.Falha((int)resp.StatusCode, resp.StatusCode.ToString(), body);
            }
            catch (TaskCanceledException)
            {
                return FiscalApiResult<string>.Falha(null, "TIMEOUT", $"Tempo limite excedido ao conectar em {baseUrl}.");
            }
            catch (HttpRequestException ex)
            {
                return FiscalApiResult<string>.Falha(null, "CONNECTION_ERROR", $"Não foi possível conectar em {baseUrl}. Verifique se o serviço está em execução. Detalhe: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------
        // Emissão
        // ---------------------------------------------------------------

        public static Task<FiscalApiResult<FiscalEmissaoNotaResponse>> EmitirAsync(
            NotaFiscalModel nota, EmpresaConfig empresa, string baseUrl, string apiKey)
        {
            bool isNfce = string.Equals((nota.Modelo ?? "55").Trim(), "65", StringComparison.Ordinal);
            var payload = FiscalPayloadBuilder.BuildEmissao(nota, empresa);
            string rota = isNfce ? "/api/v1/nfce/emitir" : "/api/v1/nfe/emitir";

            var headers = new System.Collections.Generic.Dictionary<string, string>();
            if (isNfce)
            {
                var (cscId, cscToken) = empresa.ObterCsc(nota.Ambiente);
                if (!string.IsNullOrWhiteSpace(cscId)) headers["X-CSC-Id"] = cscId;
                if (!string.IsNullOrWhiteSpace(cscToken)) headers["X-CSC-Secret"] = cscToken;
            }

            return PostAsync<FiscalEmissaoNotaResponse>(baseUrl, rota, payload, apiKey, headers);
        }

        // ---------------------------------------------------------------
        // Eventos (NF-e)
        // ---------------------------------------------------------------

        public static Task<FiscalApiResult<FiscalEventoNotaResponse>> CancelarAsync(
            string baseUrl, string apiKey, bool isNfce, string chaveAcesso, string cnpj, string nProt, string justificativa, string tpAmb)
        {
            var payload = new JsonObject
            {
                ["chaveAcesso"] = chaveAcesso,
                ["cnpj"] = cnpj,
                ["nProt"] = nProt,
                ["justificativa"] = justificativa,
                ["tpAmb"] = tpAmb
            };
            string rota = isNfce ? "/api/v1/nfce/cancelar" : "/api/v1/nfe/cancelar";
            return PostAsync<FiscalEventoNotaResponse>(baseUrl, rota, payload, apiKey, null);
        }

        public static Task<FiscalApiResult<FiscalEventoNotaResponse>> CartaCorrecaoAsync(
            string baseUrl, string apiKey, string chaveAcesso, string cnpj, string correcao, int sequencial, string tpAmb)
        {
            var payload = new JsonObject
            {
                ["chaveAcesso"] = chaveAcesso,
                ["cnpj"] = cnpj,
                ["correcao"] = correcao,
                ["sequencial"] = sequencial,
                ["tpAmb"] = tpAmb
            };
            return PostAsync<FiscalEventoNotaResponse>(baseUrl, "/api/v1/nfe/carta-correcao", payload, apiKey, null);
        }

        public static Task<FiscalApiResult<FiscalInutilizacaoResponse>> InutilizarAsync(
            string baseUrl, string apiKey, string cnpj, string cUF, string ano, string serie,
            string numeroInicial, string numeroFinal, string justificativa, string tpAmb)
        {
            var payload = new JsonObject
            {
                ["cnpj"] = cnpj,
                ["cUF"] = cUF,
                ["ano"] = ano,
                ["modelo"] = "55",
                ["serie"] = serie,
                ["numeroInicial"] = numeroInicial,
                ["numeroFinal"] = numeroFinal,
                ["justificativa"] = justificativa,
                ["tpAmb"] = tpAmb
            };
            return PostAsync<FiscalInutilizacaoResponse>(baseUrl, "/api/v1/nfe/inutilizar", payload, apiKey, null);
        }

        // ---------------------------------------------------------------
        // Consultas
        // ---------------------------------------------------------------

        public static async Task<FiscalApiResult<FiscalNotaStatusResponse>> ConsultarStatusAsync(
            string baseUrl, string apiKey, string chaveAcesso, string? tpAmb)
        {
            string rota = $"/api/v1/notas/status/{chaveAcesso}" + (string.IsNullOrWhiteSpace(tpAmb) ? "" : $"?tpAmb={tpAmb}");
            return await GetJsonAsync<FiscalNotaStatusResponse>(baseUrl, rota, apiKey);
        }

        public static async Task<FiscalApiResult<string>> ObterXmlAsync(
            string baseUrl, string apiKey, string chaveAcesso, string? tpAmb)
        {
            string rota = $"/api/v1/notas/xml/{chaveAcesso}" + (string.IsNullOrWhiteSpace(tpAmb) ? "" : $"?tpAmb={tpAmb}");
            try
            {
                using var req = CriarRequest(HttpMethod.Get, baseUrl, rota, apiKey, null);
                using var resp = await Http.SendAsync(req);
                string body = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode)
                    return FiscalApiResult<string>.Ok(body, (int)resp.StatusCode);
                return ExtrairErro<string>(resp.StatusCode, body);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return ErroDeTransporte<string>(ex, baseUrl);
            }
        }

        public static async Task<FiscalApiResult<byte[]>> ObterDanfeAsync(
            string baseUrl, string apiKey, string chaveAcesso, string? tpAmb)
        {
            string rota = $"/api/v1/notas/danfe/{chaveAcesso}" + (string.IsNullOrWhiteSpace(tpAmb) ? "" : $"?tpAmb={tpAmb}");
            try
            {
                using var req = CriarRequest(HttpMethod.Get, baseUrl, rota, apiKey, null);
                using var resp = await Http.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    byte[] pdf = await resp.Content.ReadAsByteArrayAsync();
                    return FiscalApiResult<byte[]>.Ok(pdf, (int)resp.StatusCode);
                }
                string body = await resp.Content.ReadAsStringAsync();
                return ExtrairErro<byte[]>(resp.StatusCode, body);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return ErroDeTransporte<byte[]>(ex, baseUrl);
            }
        }

        // ---------------------------------------------------------------
        // Infra HTTP comum
        // ---------------------------------------------------------------

        private static async Task<FiscalApiResult<T>> PostAsync<T>(
            string baseUrl, string rota, JsonObject payload, string apiKey,
            System.Collections.Generic.Dictionary<string, string>? extraHeaders) where T : class
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return FiscalApiResult<T>.Falha(null, "URL_VAZIA", "Configure a URL base da API Fiscal em Configurações → Integrações.");
            if (string.IsNullOrWhiteSpace(apiKey))
                return FiscalApiResult<T>.Falha(null, "API_KEY_VAZIA", "Configure a API Key da API Fiscal em Configurações → Integrações.");

            try
            {
                using var req = CriarRequest(HttpMethod.Post, baseUrl, rota, apiKey, extraHeaders);
                req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

                using var resp = await Http.SendAsync(req);
                string body = await resp.Content.ReadAsStringAsync();

                // 200 (autorizada/rejeitada) e 422 (rejeição local) usam o MESMO contrato de resposta —
                // o campo "aprovado"/"erro" é quem decide o resultado fiscal, não o HTTP status.
                if (resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    var dados = string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<T>(body, JsonOpts);
                    return dados != null
                        ? FiscalApiResult<T>.Ok(dados, (int)resp.StatusCode)
                        : FiscalApiResult<T>.Falha((int)resp.StatusCode, "RESPOSTA_VAZIA", "A API retornou um corpo vazio ou inválido.", body);
                }

                return ExtrairErro<T>(resp.StatusCode, body);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return ErroDeTransporte<T>(ex, baseUrl);
            }
        }

        private static async Task<FiscalApiResult<T>> GetJsonAsync<T>(string baseUrl, string rota, string apiKey) where T : class
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return FiscalApiResult<T>.Falha(null, "URL_VAZIA", "Configure a URL base da API Fiscal em Configurações → Integrações.");

            try
            {
                using var req = CriarRequest(HttpMethod.Get, baseUrl, rota, apiKey, null);
                using var resp = await Http.SendAsync(req);
                string body = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode)
                {
                    var dados = string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<T>(body, JsonOpts);
                    return dados != null
                        ? FiscalApiResult<T>.Ok(dados, (int)resp.StatusCode)
                        : FiscalApiResult<T>.Falha((int)resp.StatusCode, "RESPOSTA_VAZIA", "A API retornou um corpo vazio ou inválido.", body);
                }

                return ExtrairErro<T>(resp.StatusCode, body);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return ErroDeTransporte<T>(ex, baseUrl);
            }
        }

        private static HttpRequestMessage CriarRequest(
            HttpMethod method, string baseUrl, string rota, string apiKey,
            System.Collections.Generic.Dictionary<string, string>? extraHeaders)
        {
            var req = new HttpRequestMessage(method, CombinarUrl(baseUrl, rota));
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            if (extraHeaders != null)
                foreach (var kv in extraHeaders)
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            return req;
        }

        private static string CombinarUrl(string baseUrl, string rota) =>
            baseUrl.TrimEnd('/') + (rota.StartsWith('/') ? rota : "/" + rota);

        /// <summary>
        /// Interpreta o corpo de erro conforme o formato documentado (seção 9 do guia):
        /// 400 → {"erro":"..."}; 401/429 → {"error":{"code","message"}}; 5xx → ProblemDetails RFC 7807.
        /// </summary>
        private static FiscalApiResult<T> ExtrairErro<T>(HttpStatusCode status, string body) where T : class
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.Object)
                    {
                        string code = errorEl.TryGetProperty("code", out var c) ? c.GetString() ?? status.ToString() : status.ToString();
                        string msg = errorEl.TryGetProperty("message", out var m) ? m.GetString() ?? body : body;
                        return FiscalApiResult<T>.Falha((int)status, code, msg, body);
                    }

                    if (root.TryGetProperty("erro", out var erroEl) && erroEl.ValueKind == JsonValueKind.String)
                        return FiscalApiResult<T>.Falha((int)status, status.ToString(), erroEl.GetString() ?? body, body);

                    if (root.TryGetProperty("title", out var titleEl) && root.TryGetProperty("detail", out var detailEl))
                    {
                        string traceId = root.TryGetProperty("traceId", out var t) ? $" (traceId: {t.GetString()})" : "";
                        return FiscalApiResult<T>.Falha((int)status, titleEl.GetString() ?? status.ToString(), (detailEl.GetString() ?? body) + traceId, body);
                    }
                }
            }
            catch (JsonException)
            {
                // corpo não é JSON — cai no fallback abaixo
            }

            return FiscalApiResult<T>.Falha((int)status, status.ToString(),
                string.IsNullOrWhiteSpace(body) ? $"Erro HTTP {(int)status} sem detalhes." : body);
        }

        private static FiscalApiResult<T> ErroDeTransporte<T>(Exception ex, string baseUrl) where T : class => ex switch
        {
            TaskCanceledException => FiscalApiResult<T>.Falha(null, "TIMEOUT", $"Tempo limite excedido ao conectar em {baseUrl}. Verifique se o serviço está em execução."),
            HttpRequestException hre => FiscalApiResult<T>.Falha(null, "CONNECTION_ERROR", $"Não foi possível conectar em {baseUrl}. {hre.Message}"),
            JsonException je => FiscalApiResult<T>.Falha(null, "PARSE_ERROR", "A resposta da API veio em formato inesperado.", je.Message),
            _ => FiscalApiResult<T>.Falha(null, "UNEXPECTED_ERROR", ex.Message)
        };
    }
}
