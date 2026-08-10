using System.IO;
using FTO_App.Services;
using FTO_App.Services.Danfse;

namespace FTO_App.Tests;

public class DanfseNt008Tests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "nfse-autorizada-homolog-sanitizada.xml");

    private static string LoadFixtureXml() => File.ReadAllText(FixturePath);

    [Fact]
    public void Parser_ExtraiChave50DigitosECamposObrigatorios()
    {
        var model = NfseXmlDanfseParser.Parse(LoadFixtureXml());

        Assert.Equal(50, model.ChaveAcesso.Length);
        Assert.True(model.ChaveAcesso.All(char.IsDigit));
        Assert.Equal("11", model.NumeroNfse);
        Assert.False(string.IsNullOrWhiteSpace(model.Competencia));
        Assert.Equal("11", model.NumeroDps);
        Assert.Equal("1", model.SerieDps);
        Assert.Equal("2", model.TpAmb);
        Assert.True(model.EhHomologacao);
        Assert.False(string.IsNullOrWhiteSpace(model.Prestador.Documento));
        Assert.Contains("software", model.DescServico, StringComparison.OrdinalIgnoreCase);
        Assert.True(model.TemIbsCbs);
        Assert.False(string.IsNullOrWhiteSpace(model.ValorServico) || model.ValorServico == "—");
    }

    [Fact]
    public void GerarDeXml_ProduzPdfNaoVazio()
    {
        byte[] pdf = DanfsePdfService.GerarDeXml(LoadFixtureXml());

        Assert.True(pdf.Length > 100);
        Assert.Equal(0x25, pdf[0]); // %
        Assert.Equal(0x50, pdf[1]); // P
        Assert.Equal(0x44, pdf[2]); // D
        Assert.Equal(0x46, pdf[3]); // F
    }

    [Fact]
    public void Homologacao_TpAmb2_MarcaSemValidadeJuridica()
    {
        var model = DanfsePdfService.ParseXml(LoadFixtureXml());
        Assert.Equal("2", model.TpAmb);
        Assert.True(model.EhHomologacao);
    }

    [Fact]
    public void XmlSemChave_LancaDanfseXmlException()
    {
        const string xml =
            """
            <NFSe xmlns="http://www.sped.fazenda.gov.br/nfse">
              <infNFSe Id="NFS">
                <nNFSe>1</nNFSe>
              </infNFSe>
            </NFSe>
            """;

        var ex = Assert.Throws<DanfseXmlException>(() => NfseXmlDanfseParser.Parse(xml));
        Assert.Contains(ex.CamposFaltantes, c => c.Contains("chave", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void XmlVazio_LancaDanfseXmlException()
    {
        Assert.Throws<DanfseXmlException>(() => NfseXmlDanfseParser.Parse(""));
    }
}
