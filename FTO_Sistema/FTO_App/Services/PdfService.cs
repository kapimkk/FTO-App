using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FTO_App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FTO_App.Services
{
    /// <summary>Geração de PDFs (lista de clientes e cupom não fiscal).</summary>
    public static class PdfService
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        static PdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static void GerarListaClientesPdf(IReadOnlyList<ClienteModel> clientes, string caminhoArquivo)
        {
            if (clientes is null) throw new ArgumentNullException(nameof(clientes));
            if (string.IsNullOrWhiteSpace(caminhoArquivo)) throw new ArgumentException("Caminho inválido.", nameof(caminhoArquivo));

            var empresa = EmpresaConfigStore.Current;

            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(empresa.Nome).Bold().FontSize(16);
                        col.Item().PaddingTop(6).Text($"Lista de Clientes — {DateTime.Now:dd/MM/yyyy HH:mm}").SemiBold();
                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(45);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CabecalhoCelula).Text("ID");
                            header.Cell().Element(CabecalhoCelula).Text("Nome");
                            header.Cell().Element(CabecalhoCelula).Text("Contato");
                            header.Cell().Element(CabecalhoCelula).Text("CPF/CNPJ");
                        });

                        foreach (ClienteModel cliente in clientes.OrderBy(c => c.Nome))
                        {
                            table.Cell().Element(CorpoCelula).Text(cliente.Id.ToString(PtBr));
                            table.Cell().Element(CorpoCelula).Text(cliente.Nome);
                            table.Cell().Element(CorpoCelula).Text(string.IsNullOrWhiteSpace(cliente.Contato) ? "-" : cliente.Contato);
                            table.Cell().Element(CorpoCelula).Text(string.IsNullOrWhiteSpace(cliente.CpfCnpj) ? "-" : cliente.CpfCnpj);
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Total: ").SemiBold();
                        text.Span($"{clientes.Count} cliente(s)");
                        text.Span("  |  Página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf(caminhoArquivo);
        }

        public static void GerarCupomPdf(Venda venda, string caminhoArquivo)
        {
            if (venda is null) throw new ArgumentNullException(nameof(venda));
            if (string.IsNullOrWhiteSpace(caminhoArquivo)) throw new ArgumentException("Caminho inválido.", nameof(caminhoArquivo));

            var empresa = EmpresaConfigStore.Current;
            string impressoEm = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", PtBr);
            string servico = string.IsNullOrWhiteSpace(venda.TipoServico) ? "-" : venda.TipoServico.Trim();
            string formaPag = string.IsNullOrWhiteSpace(venda.FormaPag) ? "-" : venda.FormaPag.Trim();
            string cliente = string.IsNullOrWhiteSpace(venda.Cliente) ? "-" : venda.Cliente.Trim();

            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.ContinuousSize(80, Unit.Millimetre);
                    page.MarginVertical(8, Unit.Millimetre);
                    page.MarginHorizontal(6, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Content().Column(col =>
                    {
                        col.Spacing(4);

                        col.Item().AlignCenter().Column(blocoEmpresa =>
                        {
                            blocoEmpresa.Spacing(2);
                            blocoEmpresa.Item().Text(empresa.Nome).Bold().FontSize(12);
                            if (!string.IsNullOrWhiteSpace(empresa.Subtitulo))
                                blocoEmpresa.Item().Text(empresa.Subtitulo).Bold().FontSize(9);

                            if (!string.IsNullOrWhiteSpace(empresa.Endereco))
                                blocoEmpresa.Item().Text(empresa.Endereco).FontSize(8);
                            if (!string.IsNullOrWhiteSpace(empresa.Cidade))
                                blocoEmpresa.Item().Text(empresa.Cidade).FontSize(8);
                            if (!string.IsNullOrWhiteSpace(empresa.TelefoneExibicao))
                                blocoEmpresa.Item().Text(empresa.TelefoneExibicao).FontSize(8);
                            if (!string.IsNullOrWhiteSpace(empresa.CnpjExibicao))
                                blocoEmpresa.Item().Text(empresa.CnpjExibicao).FontSize(8);
                            if (!string.IsNullOrWhiteSpace(empresa.IeExibicao))
                                blocoEmpresa.Item().Text(empresa.IeExibicao).FontSize(8);

                            blocoEmpresa.Item().PaddingTop(2).Text(empresa.CupomTitulo).Bold().FontSize(9);
                        });

                        col.Item().PaddingTop(6).PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                        col.Item().Element(c => LinhaRotuloValor(c, "Venda Nº:", venda.Id.ToString(PtBr)));
                        col.Item().Element(c => LinhaRotuloValor(c, "Data:", venda.DataFormatada));
                        col.Item().Element(c => LinhaRotuloValor(c, "Impresso em:", impressoEm));
                        col.Item().Element(c => LinhaRotuloValor(c, "Cliente:", cliente));

                        col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                        col.Item().Text("Serviços:").SemiBold();
                        col.Item().Text(servico);

                        col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                        col.Item().LineHorizontal(1.5f).LineColor("#0b3d91");

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("TOTAL:").Bold().FontSize(11);
                            row.ConstantItem(100).AlignRight().Text(venda.VendaValor.ToString("C2", PtBr)).Bold().FontSize(13);
                        });

                        col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                        col.Item().Text("PAGAMENTO").Bold().FontSize(9);
                        col.Item().Element(c => LinhaRotuloValor(c, "Forma:", formaPag));

                        col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                        if (!string.IsNullOrWhiteSpace(empresa.CupomRodape))
                        {
                            col.Item().AlignCenter().Text(empresa.CupomRodape).Italic().FontSize(9);
                        }
                    });
                });
            }).GeneratePdf(caminhoArquivo);
        }

        private static void LinhaRotuloValor(IContainer container, string rotulo, string valor)
        {
            container.Row(row =>
            {
                row.AutoItem().Text(rotulo).SemiBold();
                row.ConstantItem(4);
                row.RelativeItem().Text(valor);
            });
        }

        private static IContainer CabecalhoCelula(IContainer container) =>
            container.DefaultTextStyle(x => x.SemiBold())
                .Background(Colors.Grey.Lighten3)
                .Border(0.5f)
                .BorderColor(Colors.Grey.Medium)
                .PaddingVertical(6)
                .PaddingHorizontal(4);

        private static IContainer CorpoCelula(IContainer container) =>
            container.Border(0.5f)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(5)
                .PaddingHorizontal(4);
    }
}
