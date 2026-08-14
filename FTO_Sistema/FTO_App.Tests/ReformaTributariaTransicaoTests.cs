using FTO_App.Models;
using FTO_App.Services;

namespace FTO_App.Tests;

/// <summary>
/// Alíquotas de transição da reforma (LC 214/2025 arts. 343/346). A virada de 2026 → 2027
/// muda CBS e IBS de uma vez; estes testes travam esse comportamento.
/// </summary>
public class ReformaTributariaTransicaoTests
{
    private static EmpresaConfig ConfigTeste2026() => new()
    {
        IbsCbsPreset = ReformaTributariaService.PresetTeste2026,
        CbsAliquota = 0.9m,
        IbsAliquota = 0.1m,
        IbsAliquotaUf = 0.1m,
        IbsAliquotaMun = 0m
    };

    private static EmpresaConfig ConfigCbsConfirmada(decimal cbsCheia) => new()
    {
        IbsCbsPreset = ReformaTributariaService.PresetPersonalizado,
        CbsAliquota = cbsCheia,
        IbsAliquotaUf = 0.05m,
        IbsAliquotaMun = 0.05m
    };

    [Fact]
    public void FaseDeTeste_MantemIbsInteiroNaUf()
    {
        // pIBSMun tem de ser 0 em 2026 — dividir 0,05/0,05 gera rejeição 1026 na SEFAZ
        var (cbs, ibsUf, ibsMun) = ReformaTributariaService.AliquotasOficiaisTransicao(2026, ConfigTeste2026());

        Assert.Equal(0.9m, cbs);
        Assert.Equal(0.1m, ibsUf);
        Assert.Equal(0m, ibsMun);
    }

    [Theory]
    [InlineData(2027)]
    [InlineData(2028)]
    public void ApartirDe2027_IbsVaiPara005MaisEA_CbsSaiDaAliquotaTeste(int ano)
    {
        var (cbs, ibsUf, ibsMun) = ReformaTributariaService.AliquotasOficiaisTransicao(ano, ConfigTeste2026());

        Assert.Equal(0.05m, ibsUf);
        Assert.Equal(0.05m, ibsMun);
        // O bug corrigido: antes seguia devolvendo 0,9% (alíquota de teste) em 2027
        Assert.NotEqual(0.9m, cbs);
        Assert.Equal(ReformaTributariaService.CbsReferenciaProjetada - 0.1m, cbs);
    }

    [Fact]
    public void CbsConfirmadaEmConfiguracoes_TemPrioridadeSobreOFallback()
    {
        var cfg = ConfigCbsConfirmada(8.8m);

        var (cbs, _, _) = ReformaTributariaService.AliquotasOficiaisTransicao(2027, cfg);

        // Redução de 0,1 p.p. prevista para 2027-2028
        Assert.Equal(8.7m, cbs);
        Assert.False(ReformaTributariaService.UsandoCbsDeFallback(cfg));
    }

    [Fact]
    public void PresetDeTeste_SinalizaQueACbsCheiaAindaNaoFoiConfirmada()
    {
        Assert.True(ReformaTributariaService.UsandoCbsDeFallback(ConfigTeste2026()));
    }

    [Fact]
    public void CalcularParaEmissao_2027_UsaAsAliquotasDaTransicao()
    {
        var nota = new NotaFiscalModel { ProdutoValorTotal = 1000m };

        var r = ReformaTributariaService.CalcularParaEmissao(1000m, nota, 2027, ConfigCbsConfirmada(8.8m));

        Assert.Equal(87.00m, r.ValorCbs);   // 1000 × 8,7%
        Assert.Equal(0.50m, r.ValorIbsUf);  // 1000 × 0,05%
        Assert.Equal(0.50m, r.ValorIbsMun);
        Assert.Equal(1.00m, r.ValorIbs);
        Assert.Equal(88.00m, r.ValorTotalIva);
    }

    [Fact]
    public void CalcularParaEmissao_2026_ContinuaNaFaseDeTeste()
    {
        var nota = new NotaFiscalModel { ProdutoValorTotal = 1000m };

        var r = ReformaTributariaService.CalcularParaEmissao(1000m, nota, 2026, ConfigTeste2026());

        Assert.Equal(9.00m, r.ValorCbs);   // 1000 × 0,9%
        Assert.Equal(1.00m, r.ValorIbsUf); // 1000 × 0,1%
        Assert.Equal(0m, r.ValorIbsMun);
    }
}
