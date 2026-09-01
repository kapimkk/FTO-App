using System.Printing;
using FTO_App.Services;

namespace FTO_App.Tests;

/// <summary>
/// O cupom saía bem por USB e falhado na mesma impressora em rede porque cada fila do Windows tem
/// seus próprios padrões de papel, cor, qualidade e resolução. Estes testes travam as escolhas que
/// o app força no PrintTicket para as duas filas imprimirem igual.
/// </summary>
public class CupomImpressaoTermicaTests
{
    private const double DipPorMm = 96.0 / 25.4;

    private static double Mm(double mm) => mm * DipPorMm;

    [Fact]
    public void Bobina_SemNomePadronizado_EhAceita()
    {
        var rolo = new PageMediaSize(Mm(80), Mm(300));

        Assert.True(CupomPrintHelper.PareceMidiaDeBobina(rolo));
    }

    [Fact]
    public void A4_NaoEhBobina()
    {
        var a4 = new PageMediaSize(PageMediaSizeName.ISOA4, Mm(210), Mm(297));

        Assert.False(CupomPrintHelper.PareceMidiaDeBobina(a4));
    }

    [Fact]
    public void Envelope_NaFaixaDeLargura_NaoEhBobina()
    {
        // JapanChou4 tem 90 mm de largura: cai na faixa do rolo e seria escolhido por engano,
        // trocando o papel da fila por envelope.
        var envelope = new PageMediaSize(PageMediaSizeName.JapanChou4Envelope, Mm(90), Mm(205));

        Assert.False(CupomPrintHelper.PareceMidiaDeBobina(envelope));
    }

    [Fact]
    public void Cor_PrefereMonocromatico()
    {
        var escolhida = CupomPrintHelper.EscolherCor(
            new[] { OutputColor.Color, OutputColor.Grayscale, OutputColor.Monochrome });

        Assert.Equal(OutputColor.Monochrome, escolhida);
    }

    [Fact]
    public void Cor_SemMonocromatico_CaiEmTonsDeCinza()
    {
        var escolhida = CupomPrintHelper.EscolherCor(new[] { OutputColor.Color, OutputColor.Grayscale });

        Assert.Equal(OutputColor.Grayscale, escolhida);
    }

    [Fact]
    public void Cor_SoColor_NaoForcaNada()
    {
        Assert.Null(CupomPrintHelper.EscolherCor(new[] { OutputColor.Color }));
        Assert.Null(CupomPrintHelper.EscolherCor(null));
    }

    [Fact]
    public void Qualidade_NuncaEscolheRascunho()
    {
        // Draft na térmica = menos pontos por linha: cupom apagado e com falhas.
        var escolhida = CupomPrintHelper.EscolherQualidade(
            new[] { OutputQuality.Draft, OutputQuality.Normal, OutputQuality.High });

        Assert.Equal(OutputQuality.Normal, escolhida);
    }

    [Fact]
    public void Qualidade_SemNormal_UsaAlta()
    {
        var escolhida = CupomPrintHelper.EscolherQualidade(new[] { OutputQuality.Draft, OutputQuality.High });

        Assert.Equal(OutputQuality.High, escolhida);
    }

    [Fact]
    public void Resolucao_MaiorHorizontal_EmpateNaMaisQuadrada()
    {
        // 203×203 em vez de 203×406: mesma nitidez, metade dos dados até a impressora.
        var escolhida = CupomPrintHelper.EscolherResolucao(new[]
        {
            new PageResolution(152, 152),
            new PageResolution(203, 406),
            new PageResolution(203, 203)
        });

        Assert.Equal(203, escolhida!.X);
        Assert.Equal(203, escolhida.Y);
    }

    [Fact]
    public void Resolucao_SemNadaDeclarado_NaoForcaNada()
    {
        Assert.Null(CupomPrintHelper.EscolherResolucao(System.Array.Empty<PageResolution>()));
        Assert.Null(CupomPrintHelper.EscolherResolucao(null));
    }

    [Fact]
    public void Escala_CabendoNaAreaImprimivel_NaoEncolhe()
    {
        Assert.Equal(1.0, CupomPrintHelper.EscalaDeSeguranca(Mm(72), Mm(72)));
        Assert.Equal(1.0, CupomPrintHelper.EscalaDeSeguranca(Mm(60), Mm(72)));
    }

    [Fact]
    public void Escala_LayoutMaiorQueAreaImprimivel_Encolhe()
    {
        // Etiquetadora de 40 mm com layout mínimo de 40 mm de conteúdo: reduz em vez de cortar.
        double escala = CupomPrintHelper.EscalaDeSeguranca(Mm(40), Mm(36));

        Assert.Equal(0.9, escala, 3);
    }
}
