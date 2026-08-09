using System;
using FTO_App.Models;

namespace FTO_App.Services
{
    /// <summary>
    /// Reforma tributária (EC 132/2023 + LC 214/2025 arts. 343/346/348):
    /// CBS (federal) e IBS (estadual/municipal — IVA dual).
    /// 2026 = alíquota-teste CBS 0,9% + IBS 0,1% (destaque em DF-e; compensável / dispensa de recolhimento
    /// se obrigações acessórias forem cumpridas). Alíquotas cheias projetadas ~9,21% + 18,7%.
    /// </summary>
    public static class ReformaTributariaService
    {
        public const string PresetTeste2026 = "teste2026";
        public const string PresetProjetadoCheio = "projetado";
        public const string PresetPersonalizado = "personalizado";

        /// <summary>CST padrão tributação integral (destaque obrigatório em DF-e regime regular).</summary>
        public const string CstPadrao = "000";
        /// <summary>cClassTrib padrão operação tributada integralmente.</summary>
        public const string ClassTribPadrao = "000001";

        public sealed class Resultado
        {
            public decimal BaseCalculo { get; init; }
            public decimal AliquotaCbs { get; init; }
            public decimal AliquotaIbs { get; init; }
            public decimal AliquotaIbsUf { get; init; }
            public decimal AliquotaIbsMun { get; init; }
            public decimal ValorCbs { get; init; }
            public decimal ValorIbs { get; init; }
            public decimal ValorIbsUf { get; init; }
            public decimal ValorIbsMun { get; init; }
            public decimal ValorTotalIva { get; init; }
            public string Cst { get; init; } = CstPadrao;
            public string ClassTrib { get; init; } = ClassTribPadrao;
            public string Observacao { get; init; } = "";
        }

        public static (decimal cbs, decimal ibs, decimal ibsUf, decimal ibsMun) AliquotasDoPreset(string? preset, EmpresaConfig? cfg = null)
        {
            cfg ??= EmpresaConfigStore.Current;
            return (preset ?? cfg.IbsCbsPreset) switch
            {
                PresetProjetadoCheio => (9.21m, 18.70m, 9.35m, 9.35m),
                PresetPersonalizado => (
                    cfg.CbsAliquota,
                    cfg.IbsAliquota,
                    cfg.IbsAliquotaUf > 0 ? cfg.IbsAliquotaUf : cfg.IbsAliquota / 2m,
                    cfg.IbsAliquotaMun > 0 ? cfg.IbsAliquotaMun : cfg.IbsAliquota / 2m),
                _ => (0.9m, 0.1m, 0.05m, 0.05m) // teste 2026 (ADCT art. 125)
            };
        }

        public static string DescricaoPreset(string? preset) => (preset ?? PresetTeste2026) switch
        {
            PresetProjetadoCheio => "Projetado cheio (~27,91%: CBS 9,21% + IBS 18,7%)",
            PresetPersonalizado => "Personalizado (Configurações)",
            _ => "Teste 2026 (CBS 0,9% + IBS 0,1% — sem efeito arrecadatório)"
        };

        /// <summary>
        /// Calcula IBS/CBS sobre a base (valor do item/produtos).
        /// Redução percentual aplica-se às alíquotas (ex.: 60% = cobra 40% da alíquota).
        /// </summary>
        public static Resultado Calcular(
            decimal baseCalculo,
            EmpresaConfig? cfg = null,
            decimal? aliqCbsOverride = null,
            decimal? aliqIbsOverride = null,
            decimal reducaoPercentual = 0,
            string? cst = null,
            string? classTrib = null)
        {
            cfg ??= EmpresaConfigStore.Current;
            var (cbs, ibs, ibsUf, ibsMun) = AliquotasDoPreset(cfg.IbsCbsPreset, cfg);

            if (aliqCbsOverride.HasValue) cbs = aliqCbsOverride.Value;
            if (aliqIbsOverride.HasValue)
            {
                ibs = aliqIbsOverride.Value;
                ibsUf = ibs / 2m;
                ibsMun = ibs / 2m;
            }

            if (reducaoPercentual > 0 && reducaoPercentual <= 100)
            {
                decimal fator = 1m - (reducaoPercentual / 100m);
                cbs *= fator;
                ibs *= fator;
                ibsUf *= fator;
                ibsMun *= fator;
            }

            baseCalculo = Math.Round(Math.Max(0, baseCalculo), 2);
            decimal vCbs = Math.Round(baseCalculo * cbs / 100m, 2);
            decimal vIbs = Math.Round(baseCalculo * ibs / 100m, 2);
            decimal vUf = Math.Round(baseCalculo * ibsUf / 100m, 2);
            decimal vMun = Math.Round(baseCalculo * ibsMun / 100m, 2);
            // Ajuste centavos: soma UF+Mun deve fechar o IBS
            if (vUf + vMun != vIbs)
                vMun = vIbs - vUf;

            string obs = cfg.IbsCbsPreset == PresetTeste2026
                ? "Valores de teste 2026 (destaque em DF-e). Sem efeito financeiro real nesta fase."
                : "Cálculo IBS/CBS conforme alíquotas configuradas.";

            if (string.Equals(cfg.RegimeTributario, "1", StringComparison.Ordinal) &&
                cfg.IbsCbsPreset == PresetTeste2026)
            {
                obs += " Simples Nacional: destaque obrigatório de IBS/CBS em DF-e a partir de 2027 (opcional em 2026).";
            }

            return new Resultado
            {
                BaseCalculo = baseCalculo,
                AliquotaCbs = cbs,
                AliquotaIbs = ibs,
                AliquotaIbsUf = ibsUf,
                AliquotaIbsMun = ibsMun,
                ValorCbs = vCbs,
                ValorIbs = vIbs,
                ValorIbsUf = vUf,
                ValorIbsMun = vMun,
                ValorTotalIva = vCbs + vIbs,
                Cst = string.IsNullOrWhiteSpace(cst) ? CstPadrao : cst.Trim(),
                ClassTrib = string.IsNullOrWhiteSpace(classTrib) ? ClassTribPadrao : classTrib.Trim(),
                Observacao = obs
            };
        }

        public static Resultado CalcularParaProduto(decimal baseCalculo, ProdutoModel? produto, EmpresaConfig? cfg = null)
        {
            if (produto == null)
                return Calcular(baseCalculo, cfg);

            decimal? oCbs = produto.CbsAliquota > 0 ? produto.CbsAliquota : null;
            decimal? oIbs = produto.IbsAliquota > 0 ? produto.IbsAliquota : null;
            return Calcular(
                baseCalculo,
                cfg,
                oCbs,
                oIbs,
                produto.IbsCbsReducao,
                produto.CstIbsCbs,
                produto.ClassTrib);
        }
    }
}
