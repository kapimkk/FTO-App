using System;
using System.IO;
using System.Linq;
using System.Reflection;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FTO_App.Services.Danfse
{
    /// <summary>
    /// Renderiza DANFSe A4 em 1 página conforme NT 008/2026 v1.02 (modelo v2.0).
    /// </summary>
    public static class DanfseNt008Renderer
    {
        private static readonly Color CinzaTitulo = Color.FromRGB(242, 242, 242);
        private static readonly Color CinzaMarcaDagua = Color.FromRGB(166, 166, 166);
        private static readonly Color VermelhoHomolog = Color.FromRGB(200, 16, 46);

        public const string UrlConsultaPublicaPrefix =
            "https://www.nfse.gov.br/ConsultaPublica/?tpc=1&chave=";

        static DanfseNt008Renderer()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static byte[] Render(DanfseDocumentModel m)
        {
            ArgumentNullException.ThrowIfNull(m);
            byte[]? logo = LoadLogoBytes();
            byte[] qr = GerarQr(UrlConsultaPublicaPrefix + m.ChaveAcesso);
            const string fontTitle = "Arial";
            const string fontBody = "Arial";

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(0.18f, Unit.Centimetre);
                    page.MarginVertical(0.18f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontFamily(fontBody).FontSize(7).FontColor(Colors.Black));

                    page.Content().Layers(layers =>
                    {
                        if (m.Cancelada || m.Substituida)
                        {
                            string marca = m.Cancelada ? "CANCELADA" : "SUBSTITUÍDA";
                            layers.Layer().AlignCenter().AlignMiddle()
                                .Rotate(-35)
                                .Text(marca)
                                .FontFamily(fontTitle)
                                .FontSize(50)
                                .FontColor(CinzaMarcaDagua);
                        }

                        layers.PrimaryLayer().Border(1).Padding(3).Column(col =>
                        {
                            col.Spacing(2);
                            col.Item().Element(e => Cabecalho(e, m, logo, qr, fontTitle, fontBody));
                            col.Item().Element(e => BlocoIdentificacao(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoPrestador(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoTomador(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoDestinatario(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoIntermediario(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoServico(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoIssqn(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoFederal(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoIbsCbs(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoTotais(e, m, fontTitle, fontBody));
                            col.Item().Element(e => BlocoComplementares(e, m, fontTitle, fontBody));
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static void Cabecalho(IContainer c, DanfseDocumentModel m, byte[]? logo, byte[] qr,
            string fontTitle, string fontBody)
        {
            c.Column(outer =>
            {
                outer.Item().Background(CinzaTitulo).Border(0.5f).Padding(3).Row(row =>
                {
                    row.ConstantItem(110).Height(40).Element(box =>
                    {
                        if (logo is { Length: > 0 })
                            box.Image(logo).FitArea();
                        else
                            box.AlignMiddle().AlignCenter().Text("NFS-e").Bold().FontSize(12).FontFamily(fontTitle);
                    });

                    row.RelativeItem().PaddingHorizontal(4).Column(col =>
                    {
                        col.Item().AlignCenter().Text("DANFSe v2.0").Bold().FontSize(9).FontFamily(fontTitle);
                        col.Item().AlignCenter().Text("Documento Auxiliar da NFS-e").Bold().FontSize(9).FontFamily(fontTitle);
                        if (m.EhHomologacao)
                        {
                            col.Item().AlignCenter().Text("NFS-e SEM VALIDADE JURÍDICA")
                                .Bold().FontSize(9).FontFamily(fontTitle).FontColor(VermelhoHomolog);
                        }
                    });

                    row.ConstantItem(95).Column(col =>
                    {
                        string mun = string.IsNullOrWhiteSpace(m.MunicipioEmitenteNome) ? "—" : m.MunicipioEmitenteNome;
                        col.Item().Text($"Município: {mun}").FontSize(8).FontFamily(fontBody);
                        col.Item().Text($"Amb. gerador: {NullDash(m.AmbGer)}").FontSize(6).FontFamily(fontBody);
                        col.Item().Text($"Tipo amb.: {(m.TpAmb == "1" ? "Produção" : "Homologação")} (tpAmb={m.TpAmb})")
                            .FontSize(6).FontFamily(fontBody);
                    });

                    row.ConstantItem(58).Width(52).Height(52).Image(qr);
                });

                outer.Item().AlignRight().MaxWidth(190).PaddingTop(1)
                    .Text("A autenticidade desta NFS-e pode ser verificada pela leitura deste código QR ou pela consulta da chave de acesso no portal nacional da NFS-e.")
                    .FontSize(6).FontFamily(fontBody);
            });
        }

        private static void BlocoIdentificacao(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "IDENTIFICAÇÃO DA NFS-e", fontTitle);
                col.Item().Border(0.5f).Padding(3).Column(body =>
                {
                    body.Item().Text($"CHAVE DE ACESSO: {FormatChave(m.ChaveAcesso)}").FontSize(7).FontFamily(fontBody);
                    body.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"NÚMERO: {m.NumeroNfse}").FontSize(7).Bold().FontFamily(fontTitle);
                        r.RelativeItem().Text($"COMPETÊNCIA: {m.Competencia}").FontSize(7).Bold().FontFamily(fontTitle);
                        r.RelativeItem().Text($"DH EMISSÃO NFS-e: {m.DhProcNfse}").FontSize(7).Bold().FontFamily(fontTitle);
                    });
                    body.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"Nº DPS: {m.NumeroDps}").FontSize(7).Bold().FontFamily(fontTitle);
                        r.RelativeItem().Text($"SÉRIE DPS: {m.SerieDps}").FontSize(7).Bold().FontFamily(fontTitle);
                        r.RelativeItem().Text($"DH EMISSÃO DPS: {m.DhEmiDps}").FontSize(7).Bold().FontFamily(fontTitle);
                    });
                    body.Item().Row(r =>
                    {
                        r.RelativeItem().Background(CinzaTitulo).Padding(2)
                            .Text($"EMITENTE: {NullDash(m.EmitenteTipo)}").FontSize(7).Bold().FontFamily(fontTitle);
                        r.RelativeItem().Padding(2).Text($"SITUAÇÃO: {m.Situacao}").FontSize(7).Bold().FontFamily(fontTitle);
                        r.RelativeItem().Padding(2).Text($"FINALIDADE: {NullDash(m.Finalidade)}").FontSize(7).Bold().FontFamily(fontTitle);
                    });
                });
            });
        }

        private static void BlocoPrestador(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "PRESTADOR / FORNECEDOR", fontTitle);
                col.Item().Border(0.5f).Padding(3).Element(b => Pessoa(b, m.Prestador, m, fontTitle, fontBody, incluirSn: true));
            });
        }

        private static void BlocoTomador(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "TOMADOR / ADQUIRENTE DA OPERAÇÃO", fontTitle);
                if (m.Tomador is null)
                {
                    col.Item().Border(0.5f).Padding(3)
                        .Text("TOMADOR/ADQUIRENTE DA OPERAÇÃO NÃO IDENTIFICADO NA NFS-e")
                        .FontSize(7).FontFamily(fontBody);
                    return;
                }
                col.Item().Border(0.5f).Padding(3).Element(b => Pessoa(b, m.Tomador, m, fontTitle, fontBody, false));
            });
        }

        private static void BlocoDestinatario(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "DESTINATÁRIO DA OPERAÇÃO", fontTitle);
                if (m.DestinatarioEhTomador || (m.Destinatario is null && m.Tomador is not null))
                {
                    col.Item().Border(0.5f).Padding(3)
                        .Text("O DESTINATÁRIO É O PRÓPRIO TOMADOR/ADQUIRENTE DA OPERAÇÃO")
                        .FontSize(7).FontFamily(fontBody);
                    return;
                }
                if (m.Destinatario is null)
                {
                    col.Item().Border(0.5f).Padding(3)
                        .Text("DESTINATÁRIO DA OPERAÇÃO NÃO IDENTIFICADO NA NFS-e")
                        .FontSize(7).FontFamily(fontBody);
                    return;
                }
                col.Item().Border(0.5f).Padding(3).Element(b => Pessoa(b, m.Destinatario, m, fontTitle, fontBody, false));
            });
        }

        private static void BlocoIntermediario(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "INTERMEDIÁRIO DA OPERAÇÃO", fontTitle);
                if (m.Intermediario is null)
                {
                    col.Item().Border(0.5f).Padding(3)
                        .Text("INTERMEDIÁRIO DA OPERAÇÃO NÃO IDENTIFICADO NA NFS-e")
                        .FontSize(7).FontFamily(fontBody);
                    return;
                }
                col.Item().Border(0.5f).Padding(3).Element(b => Pessoa(b, m.Intermediario, m, fontTitle, fontBody, false));
            });
        }

        private static void Pessoa(IContainer box, DanfsePessoaModel p, DanfseDocumentModel m,
            string fontTitle, string fontBody, bool incluirSn)
        {
            box.Column(col =>
            {
                col.Item().Text($"{p.TipoDocumento}: {NullDash(p.Documento)}  |  IM: {NullDash(p.Im)}  |  Fone: {NullDash(p.Telefone)}")
                    .FontSize(7).FontFamily(fontBody);
                col.Item().Text($"Nome: {NullDash(p.Nome)}").FontSize(7).FontFamily(fontBody);
                string end = string.Join(", ", new[] { p.Logradouro, p.Numero, p.Complemento, p.Bairro }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                col.Item().Text($"Endereço: {NullDash(end)}").FontSize(7).FontFamily(fontBody);
                col.Item().Text($"Município/UF: {NullDash(p.Municipio)}/{NullDash(p.Uf)}  |  IBGE: {NullDash(p.CodIbge)}  |  CEP: {NullDash(p.Cep)}")
                    .FontSize(7).FontFamily(fontBody);
                col.Item().Text($"E-mail: {NullDash(p.Email)}").FontSize(7).FontFamily(fontBody);
                if (incluirSn)
                {
                    col.Item().Text($"Simples Nacional (opSimpNac): {NullDash(m.OpSimpNac)}  |  Regime apuração SN: {NullDash(m.RegApTribSN)}")
                        .FontSize(7).FontFamily(fontBody);
                }
            });
        }

        private static void BlocoServico(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "SERVIÇO PRESTADO", fontTitle);
                col.Item().Border(0.5f).Padding(3).Column(body =>
                {
                    body.Item().Text($"cTribNac: {NullDash(m.CodTribNac)}  |  cTribMun: {NullDash(m.CodTribMun)}  |  NBS: {NullDash(m.CodNbs)}")
                        .FontSize(7).FontFamily(fontBody);
                    body.Item().Text($"Desc. trib. nac.: {NullDash(m.DescTribNac)}").FontSize(7).FontFamily(fontBody);
                    if (!string.IsNullOrWhiteSpace(m.DescTribMun))
                        body.Item().Text($"Desc. trib. mun.: {m.DescTribMun}").FontSize(7).FontFamily(fontBody);
                    if (!string.IsNullOrWhiteSpace(m.DescNbs))
                        body.Item().Text($"Desc. NBS: {m.DescNbs}").FontSize(7).FontFamily(fontBody);
                    body.Item().Text($"Local prestação: {NullDash(m.XLocPrestacao)} (IBGE {NullDash(m.CodLocPrestacao)})")
                        .FontSize(7).FontFamily(fontBody);
                    body.Item().PaddingTop(2).Text("Descrição do serviço:").Bold().FontSize(6).FontFamily(fontTitle);
                    body.Item().MinHeight(24).Text(NullDash(m.DescServico)).FontSize(7).FontFamily(fontBody);
                });
            });
        }

        private static void BlocoIssqn(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "TRIBUTAÇÃO MUNICIPAL (ISSQN)", fontTitle);
                if (m.SemIssqn && string.IsNullOrWhiteSpace(m.TribIssqn))
                {
                    col.Item().Border(0.5f).Padding(3)
                        .Text("TRIBUTAÇÃO MUNICIPAL (ISSQN) - OPERAÇÃO NÃO SUJEITA AO ISSQN")
                        .FontSize(7).FontFamily(fontBody);
                    return;
                }
                col.Item().Border(0.5f).Padding(3).Column(body =>
                {
                    body.Item().Text($"Tipo trib. ISSQN: {NullDash(m.TribIssqn)}  |  Incidência: {NullDash(m.LocIncidenciaIss)}")
                        .FontSize(7).FontFamily(fontBody);
                    body.Item().Text($"Regime especial: {NullDash(m.RegEspTrib)}  |  Retenção: {NullDash(m.TpRetIssqn)}")
                        .FontSize(7).FontFamily(fontBody);
                    body.Item().Text($"BC: {NullDash(m.BcIssqn)}  |  Alíquota: {NullDash(m.AliqIssqn)}  |  Valor ISSQN: {NullDash(m.ValorIssqn)}")
                        .FontSize(7).FontFamily(fontBody);
                });
            });
        }

        private static void BlocoFederal(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "TRIBUTAÇÃO FEDERAL (EXCETO CBS)", fontTitle);
                col.Item().Border(0.5f).Padding(3).Text(
                        $"PIS: {NullDash(m.ValorPis)}  |  COFINS: {NullDash(m.ValorCofins)}  |  IRRF: {NullDash(m.ValorIrrf)}  |  Ret. PIS/COFINS: {NullDash(m.TpRetPisCofins)}")
                    .FontSize(7).FontFamily(fontBody);
            });
        }

        private static void BlocoIbsCbs(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "TRIBUTAÇÃO IBS/CBS", fontTitle);
                if (!m.TemIbsCbs)
                {
                    col.Item().Border(0.5f).Padding(3)
                        .Text("Grupo IBS/CBS não informado no XML da NFS-e.").FontSize(7).FontFamily(fontBody);
                    return;
                }
                col.Item().Border(0.5f).Padding(3).Text(
                        $"CST: {NullDash(m.CstIbsCbs)}  |  cClassTrib: {NullDash(m.ClassTrib)}  |  vIBS: {NullDash(m.ValorIbs)}  |  vCBS: {NullDash(m.ValorCbs)}")
                    .FontSize(7).FontFamily(fontBody);
            });
        }

        private static void BlocoTotais(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "VALOR TOTAL DA NFS-e", fontTitle);
                col.Item().Border(0.5f).Padding(3).Column(body =>
                {
                    body.Item().Text($"Valor do serviço: {NullDash(m.ValorServico)}").FontSize(7).FontFamily(fontBody);
                    body.Item().Background(CinzaTitulo).Padding(2)
                        .Text($"Valor líquido da NFS-e + IBS/CBS: {NullDash(m.ValorLiquido)}" +
                              (string.IsNullOrWhiteSpace(m.ValorTotalNfse) ? "" : $"  |  Total NF: {m.ValorTotalNfse}"))
                        .Bold().FontSize(7).FontFamily(fontTitle);
                });
            });
        }

        private static void BlocoComplementares(IContainer c, DanfseDocumentModel m, string fontTitle, string fontBody)
        {
            c.Column(col =>
            {
                Titulo(col, "INFORMAÇÕES COMPLEMENTARES", fontTitle);
                col.Item().Border(0.5f).Padding(3).Column(body =>
                {
                    if (!string.IsNullOrWhiteSpace(m.PTotTribFed) || !string.IsNullOrWhiteSpace(m.PTotTribEst) ||
                        !string.IsNullOrWhiteSpace(m.PTotTribMun))
                    {
                        body.Item().Text(
                                $"Totais aproximados de tributos (Lei 12.741): Fed {NullDash(m.PTotTribFed)}% | Est {NullDash(m.PTotTribEst)}% | Mun {NullDash(m.PTotTribMun)}%")
                            .FontSize(7).FontFamily(fontBody);
                    }
                    body.Item().MinHeight(16).Text(NullDash(m.InfComplementares)).FontSize(7).FontFamily(fontBody);
                });
            });
        }

        private static void Titulo(ColumnDescriptor col, string titulo, string fontTitle) =>
            col.Item().Background(CinzaTitulo).Border(0.5f).Padding(2)
                .Text(titulo).Bold().FontSize(7).FontFamily(fontTitle);

        private static string FormatChave(string digitos)
        {
            if (digitos.Length != 50) return digitos;
            return string.Join(" ", Enumerable.Range(0, 10).Select(i => digitos.Substring(i * 5, 5)));
        }

        private static string NullDash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

        private static byte[] GerarQr(string url)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            return new PngByteQRCode(data).GetGraphic(4);
        }

        private static byte[]? LoadLogoBytes()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Resources", "nfse-logo-horizontal.png");
                if (File.Exists(path)) return File.ReadAllBytes(path);

                var asm = Assembly.GetExecutingAssembly();
                string? res = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("nfse-logo-horizontal.png", StringComparison.OrdinalIgnoreCase));
                if (res is null) return null;
                using var stream = asm.GetManifestResourceStream(res);
                if (stream is null) return null;
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}
