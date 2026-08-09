using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FTO_App.Services
{
    /// <summary>Um resultado de busca de NCM (código + descrição oficial).</summary>
    public sealed class NcmResult
    {
        public string Codigo { get; init; } = "";
        public string Descricao { get; init; } = "";

        /// <summary>Texto pronto para exibir na lista de sugestões: "código — descrição".</summary>
        public override string ToString() => $"{Codigo} — {Descricao}";
    }

    /// <summary>
    /// Consulta a tabela NCM (Nomenclatura Comum do Mercosul) via BrasilAPI — sem chave de API.
    /// Mesmo padrão tolerante do <see cref="CepService"/>: nunca lança exceção, falha de rede apenas
    /// retorna lista vazia (o usuário sempre pode digitar o NCM manualmente).
    /// </summary>
    public static class NcmService
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        /// <summary>Busca por código (com ou sem pontos) ou por palavra da descrição. Mínimo 3 caracteres.</summary>
        public static async Task<List<NcmResult>> BuscarAsync(string? termo)
        {
            var resultados = new List<NcmResult>();
            termo = (termo ?? "").Trim();
            if (termo.Length < 3) return resultados;

            try
            {
                string url = $"https://brasilapi.com.br/api/ncm/v1?search={Uri.EscapeDataString(termo)}";
                using var resp = await Http.GetAsync(url).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return resultados;

                string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return resultados;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    string codigo = item.TryGetProperty("codigo", out var c) ? c.GetString() ?? "" : "";
                    string descricao = item.TryGetProperty("descricao", out var d) ? d.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(codigo))
                        resultados.Add(new NcmResult { Codigo = codigo, Descricao = descricao });
                }

                // NCM completo (8 dígitos, usado no XML/JSON fiscal) primeiro; capítulos/posições genéricas depois.
                resultados.Sort((a, b) => DigitosNcm(b.Codigo).Length.CompareTo(DigitosNcm(a.Codigo).Length));
            }
            catch
            {
                // Rede indisponível/timeout — devolve lista vazia, sem interromper o cadastro.
            }

            return resultados;
        }

        private static string DigitosNcm(string codigo)
        {
            var chars = new System.Text.StringBuilder(codigo.Length);
            foreach (char ch in codigo)
                if (char.IsDigit(ch)) chars.Append(ch);
            return chars.ToString();
        }
    }
}
