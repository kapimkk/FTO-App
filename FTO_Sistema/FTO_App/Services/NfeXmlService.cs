using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using FTO_App.Models;

namespace FTO_App.Services
{
    /// <summary>
    /// Gera o corpo XML de NF-e alinhado ao contrato da API Fiscal (GUIA_INTEGRACAO §4 / §4.10).
    /// Não assina nem transmite — a API calcula chave/cNF, assina e monta infRespTec.
    /// </summary>
    public static class NfeXmlService
    {
        private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

        /// <summary>Mantido por compatibilidade — usar <see cref="FiscalHomologacaoTextos.XNomeDest"/>.</summary>
        public const string NomeDestHomologacao = FiscalHomologacaoTextos.XNomeDest;

        public static string GerarXml(NotaFiscalModel nota, EmpresaConfig emitente)
        {
            ArgumentNullException.ThrowIfNull(nota);
            ArgumentNullException.ThrowIfNull(emitente);

            string cnpjEmit = SomenteDigitos(emitente.Cnpj);
            string docDest = SomenteDigitos(nota.DestCpfCnpj);
            bool destPj = docDest.Length > 11;
            string ambiente = string.IsNullOrWhiteSpace(nota.Ambiente) ? "2" : nota.Ambiente.Trim();
            bool homolog = ambiente == "2";

            string destNome = homolog ? NomeDestHomologacao : nota.DestNome;
            string xProd = AplicarHomologDescricao(nota.ProdutoDescricao, homolog);

            string idDest = string.IsNullOrWhiteSpace(nota.IdDest)
                ? InferirIdDest(nota.ProdutoCfop, emitente.Uf, nota.DestUf)
                : nota.IdDest.Trim();

            string indIEDest = NormalizarIndIEDest(nota.IndIEDest, nota.DestIe);
            string crt = string.IsNullOrWhiteSpace(emitente.RegimeTributario) ? "1" : emitente.RegimeTributario.Trim();

            var ide = new XElement(Nfe + "ide",
                El("cUF", UfToCodigo(emitente.Uf)),
                El("natOp", nota.NaturezaOperacao),
                El("mod", string.IsNullOrWhiteSpace(nota.Modelo) ? "55" : nota.Modelo),
                El("serie", nota.Serie),
                El("nNF", nota.Numero.ToString(CultureInfo.InvariantCulture)),
                El("dhEmi", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")),
                El("tpNF", nota.TipoOperacao),
                El("idDest", idDest),
                El("cMunFG", emitente.CodigoIbge),
                El("tpImp", "1"),
                El("tpEmis", "1"),
                El("tpAmb", ambiente),
                El("finNFe", nota.Finalidade),
                El("indFinal", nota.ConsumidorFinal),
                El("indPres", nota.PresencaComprador),
                El("procEmi", "0"),
                El("verProc", "FTO_1.0")
            );

            var enderEmit = new XElement(Nfe + "enderEmit",
                El("xLgr", emitente.Endereco),
                El("nro", string.IsNullOrWhiteSpace(emitente.Numero) ? "S/N" : emitente.Numero),
                Opt("xCpl", emitente.Complemento),
                El("xBairro", emitente.Bairro),
                El("cMun", emitente.CodigoIbge),
                El("xMun", emitente.Cidade),
                El("UF", emitente.Uf),
                El("CEP", SomenteDigitos(emitente.Cep)),
                El("cPais", "1058"),
                El("xPais", "Brasil"),
                Opt("fone", SomenteDigitos(emitente.Telefone))
            );

            var emit = new XElement(Nfe + "emit",
                El("CNPJ", cnpjEmit),
                El("xNome", string.IsNullOrWhiteSpace(emitente.RazaoSocial) ? emitente.Nome : emitente.RazaoSocial),
                Opt("xFant", string.IsNullOrWhiteSpace(emitente.NomeFantasia) ? emitente.Subtitulo : emitente.NomeFantasia),
                enderEmit,
                El("IE", SomenteDigitosOuIsento(emitente.Ie)),
                El("CRT", crt)
            );

            var enderDest = new XElement(Nfe + "enderDest",
                El("xLgr", nota.DestLogradouro),
                El("nro", string.IsNullOrWhiteSpace(nota.DestNumero) ? "S/N" : nota.DestNumero),
                Opt("xCpl", nota.DestComplemento),
                El("xBairro", nota.DestBairro),
                El("cMun", nota.DestCodigoIbge),
                El("xMun", nota.DestMunicipio),
                El("UF", nota.DestUf),
                El("CEP", SomenteDigitos(nota.DestCep)),
                El("cPais", "1058"),
                El("xPais", "Brasil")
            );

            var dest = new XElement(Nfe + "dest",
                destPj ? El("CNPJ", docDest) : El("CPF", docDest),
                El("xNome", destNome),
                enderDest,
                El("indIEDest", indIEDest),
                indIEDest == "1" ? El("IE", SomenteDigitosOuIsento(nota.DestIe)) : null,
                Opt("email", nota.DestEmail)
            );

            var prod = new XElement(Nfe + "prod",
                El("cProd", string.IsNullOrWhiteSpace(nota.ProdutoCodigo) ? "001" : nota.ProdutoCodigo),
                El("cEAN", string.IsNullOrWhiteSpace(nota.ProdutoGtin) || nota.ProdutoGtin == "SEM GTIN" ? "SEM GTIN" : nota.ProdutoGtin),
                El("xProd", xProd),
                El("NCM", SomenteDigitos(nota.ProdutoNcm)));
            string cest = SomenteDigitos(nota.ProdutoCest);
            if (!string.IsNullOrEmpty(cest))
                prod.Add(El("CEST", cest));
            prod.Add(
                El("CFOP", nota.ProdutoCfop),
                El("uCom", nota.ProdutoUnidade),
                El("qCom", Dec(nota.ProdutoQuantidade, "0.####")),
                El("vUnCom", Dec(nota.ProdutoValorUnitario)),
                El("vProd", Dec(nota.ProdutoValorTotal)),
                El("cEANTrib", string.IsNullOrWhiteSpace(nota.ProdutoGtin) || nota.ProdutoGtin == "SEM GTIN" ? "SEM GTIN" : nota.ProdutoGtin),
                El("uTrib", nota.ProdutoUnidade),
                El("qTrib", Dec(nota.ProdutoQuantidade, "0.####")),
                El("vUnTrib", Dec(nota.ProdutoValorUnitario)),
                El("indTot", "1")
            );

            var det = new XElement(Nfe + "det", new XAttribute("nItem", "1"),
                prod,
                new XElement(Nfe + "imposto",
                    MontarIcms(nota, crt),
                    MontarPis(nota),
                    MontarCofins(nota),
                    MontarIbsCbsItem(nota)
                )
            );

            var total = new XElement(Nfe + "total",
                new XElement(Nfe + "ICMSTot",
                    El("vBC", Dec(crt == "3" ? nota.ProdutoValorTotal : 0m)),
                    El("vICMS", Dec(crt == "3" ? nota.IcmsValor : 0m)),
                    El("vICMSDeson", "0.00"),
                    El("vFCP", "0.00"),
                    El("vBCST", "0.00"),
                    El("vST", "0.00"),
                    El("vFCPST", "0.00"),
                    El("vFCPSTRet", "0.00"),
                    El("vProd", Dec(nota.ValorProdutos)),
                    El("vFrete", Dec(nota.ValorFrete)),
                    El("vSeg", "0.00"),
                    El("vDesc", Dec(nota.ValorDesconto)),
                    El("vII", "0.00"),
                    El("vIPI", "0.00"),
                    El("vIPIDevol", "0.00"),
                    El("vPIS", Dec(nota.PisValor)),
                    El("vCOFINS", Dec(nota.CofinsValor)),
                    El("vOutro", "0.00"),
                    El("vNF", Dec(nota.ValorTotalNota))
                ),
                MontarIbsCbsTot(nota)
            );

            var pag = new XElement(Nfe + "pag",
                new XElement(Nfe + "detPag",
                    El("tPag", string.IsNullOrWhiteSpace(nota.FormaPagamento) ? "01" : nota.FormaPagamento),
                    El("vPag", Dec(nota.ValorTotalNota))
                )
            );

            var infNFe = new XElement(Nfe + "infNFe", new XAttribute("versao", "4.00"),
                ide, emit, dest, det, total,
                new XElement(Nfe + "transp", El("modFrete", "9")),
                pag,
                string.IsNullOrWhiteSpace(nota.InformacoesComplementares)
                    ? null
                    : new XElement(Nfe + "infAdic", El("infCpl", nota.InformacoesComplementares))
            );

            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(Nfe + "NFe", infNFe)
            );

            return doc.ToString(SaveOptions.DisableFormatting);
        }

        public static string SalvarXml(NotaFiscalModel nota, EmpresaConfig emitente, string? pasta = null)
        {
            pasta ??= Path.Combine(AppContext.BaseDirectory, "xml_nfe");
            Directory.CreateDirectory(pasta);
            string xml = GerarXml(nota, emitente);
            string file = Path.Combine(pasta, $"NFe_{nota.Serie}_{nota.Numero}_{DateTime.Now:yyyyMMddHHmmss}.xml");
            File.WriteAllText(file, xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return file;
        }

        /// <summary>
        /// Em homologação (tpAmb=2), a SEFAZ exige que xProd do primeiro item seja EXATAMENTE
        /// <see cref="FiscalHomologacaoTextos.XProd"/> (Rejeição 373) — nunca a descrição real,
        /// nem um sufixo/variação dela. Fora de homologação, retorna a descrição normalmente.
        /// </summary>
        public static string AplicarHomologDescricao(string? descricao, bool homolog) =>
            homolog ? FiscalHomologacaoTextos.XProd : (descricao ?? "").Trim();

        public static string InferirIdDest(string? cfop, string? ufEmit, string? ufDest)
        {
            string c = (cfop ?? "").Trim();
            if (c.Length >= 1)
            {
                char d = c[0];
                if (d is '2' or '6') return "2";
                if (d is '3' or '7') return "3";
                if (d is '1' or '5') return "1";
            }

            string a = (ufEmit ?? "").Trim().ToUpperInvariant();
            string b = (ufDest ?? "").Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && a != b)
                return "2";
            return "1";
        }

        private static XElement MontarIcms(NotaFiscalModel nota, string crt)
        {
            string orig = string.IsNullOrWhiteSpace(nota.IcmsOrigem) ? "0" : nota.IcmsOrigem.Trim();

            // Regime normal (CRT=3): CST
            if (crt == "3")
            {
                string cst = string.IsNullOrWhiteSpace(nota.IcmsCst) ? "00" : nota.IcmsCst.Trim();
                return new XElement(Nfe + "ICMS",
                    new XElement(Nfe + "ICMS00",
                        El("orig", orig),
                        El("CST", cst),
                        El("modBC", "3"),
                        El("vBC", Dec(nota.ProdutoValorTotal)),
                        El("pICMS", Dec(nota.IcmsAliquota)),
                        El("vICMS", Dec(nota.IcmsValor))
                    )
                );
            }

            // Simples / MEI: CSOSN
            string csosn = string.IsNullOrWhiteSpace(nota.Csosn) ? "102" : nota.Csosn.Trim();
            return new XElement(Nfe + "ICMS", MontarIcmsSn(orig, csosn, nota));
        }

        private static XElement MontarIcmsSn(string orig, string csosn, NotaFiscalModel nota)
        {
            return csosn switch
            {
                "101" => new XElement(Nfe + "ICMSSN101",
                    El("orig", orig),
                    El("CSOSN", csosn),
                    El("pCredSN", Dec(nota.IcmsAliquota)),
                    El("vCredICMSSN", Dec(nota.IcmsValor))),
                "201" => new XElement(Nfe + "ICMSSN201",
                    El("orig", orig),
                    El("CSOSN", csosn),
                    El("modBCST", "4"),
                    El("pMVAST", "0.00"),
                    El("vBCST", "0.00"),
                    El("pICMSST", "0.00"),
                    El("vICMSST", "0.00"),
                    El("pCredSN", Dec(nota.IcmsAliquota)),
                    El("vCredICMSSN", Dec(nota.IcmsValor))),
                "202" or "203" => new XElement(Nfe + "ICMSSN202",
                    El("orig", orig),
                    El("CSOSN", csosn),
                    El("modBCST", "4"),
                    El("vBCST", "0.00"),
                    El("pICMSST", "0.00"),
                    El("vICMSST", "0.00")),
                "500" => new XElement(Nfe + "ICMSSN500",
                    El("orig", orig),
                    El("CSOSN", csosn),
                    El("vBCSTRet", "0.00"),
                    El("pST", "0.00"),
                    El("vICMSSTRet", "0.00")),
                "900" => new XElement(Nfe + "ICMSSN900",
                    El("orig", orig),
                    El("CSOSN", csosn)),
                // 102, 103, 300, 400 e demais sem crédito
                _ => new XElement(Nfe + "ICMSSN102",
                    El("orig", orig),
                    El("CSOSN", csosn is "102" or "103" or "300" or "400" ? csosn : "102"))
            };
        }

        private static XElement MontarPis(NotaFiscalModel nota)
        {
            string cst = string.IsNullOrWhiteSpace(nota.PisCst) ? "01" : nota.PisCst.Trim();
            if (cst is "04" or "05" or "06" or "07" or "08" or "09")
            {
                return new XElement(Nfe + "PIS",
                    new XElement(Nfe + "PISNT", El("CST", cst)));
            }

            return new XElement(Nfe + "PIS",
                new XElement(Nfe + "PISAliq",
                    El("CST", cst),
                    El("vBC", Dec(nota.ProdutoValorTotal)),
                    El("pPIS", Dec(nota.PisAliquota)),
                    El("vPIS", Dec(nota.PisValor))
                ));
        }

        private static XElement MontarCofins(NotaFiscalModel nota)
        {
            string cst = string.IsNullOrWhiteSpace(nota.CofinsCst) ? "01" : nota.CofinsCst.Trim();
            if (cst is "04" or "05" or "06" or "07" or "08" or "09")
            {
                return new XElement(Nfe + "COFINS",
                    new XElement(Nfe + "COFINSNT", El("CST", cst)));
            }

            return new XElement(Nfe + "COFINS",
                new XElement(Nfe + "COFINSAliq",
                    El("CST", cst),
                    El("vBC", Dec(nota.ProdutoValorTotal)),
                    El("pCOFINS", Dec(nota.CofinsAliquota)),
                    El("vCOFINS", Dec(nota.CofinsValor))
                ));
        }

        /// <summary>
        /// Estrutura oficial do item: gIBSCBS → vBC, gIBSUF, gIBSMun, vIBS, gCBS
        /// (sem wrapper gIBS no item — ver DFeTiposBasicos / API §4.10).
        /// </summary>
        private static XElement MontarIbsCbsItem(NotaFiscalModel nota)
        {
            return new XElement(Nfe + "IBSCBS",
                El("CST", string.IsNullOrWhiteSpace(nota.CstIbsCbs) ? "000" : nota.CstIbsCbs),
                El("cClassTrib", string.IsNullOrWhiteSpace(nota.ClassTrib) ? "000001" : nota.ClassTrib),
                new XElement(Nfe + "gIBSCBS",
                    El("vBC", Dec(nota.ProdutoValorTotal)),
                    new XElement(Nfe + "gIBSUF",
                        El("pIBSUF", Dec(nota.IbsAliquotaUf, "0.####")),
                        El("vIBSUF", Dec(nota.IbsValorUf))
                    ),
                    new XElement(Nfe + "gIBSMun",
                        El("pIBSMun", Dec(nota.IbsAliquotaMun, "0.####")),
                        El("vIBSMun", Dec(nota.IbsValorMun))
                    ),
                    El("vIBS", Dec(nota.IbsValor)),
                    new XElement(Nfe + "gCBS",
                        El("pCBS", Dec(nota.CbsAliquota, "0.####")),
                        El("vCBS", Dec(nota.CbsValor))
                    )
                )
            );
        }

        private static XElement MontarIbsCbsTot(NotaFiscalModel nota)
        {
            return new XElement(Nfe + "IBSCBSTot",
                El("vBCIBSCBS", Dec(nota.ProdutoValorTotal)),
                new XElement(Nfe + "gIBS",
                    new XElement(Nfe + "gIBSUF",
                        El("vDif", "0.00"),
                        El("vDevTrib", "0.00"),
                        El("vIBSUF", Dec(nota.IbsValorUf))
                    ),
                    new XElement(Nfe + "gIBSMun",
                        El("vDif", "0.00"),
                        El("vDevTrib", "0.00"),
                        El("vIBSMun", Dec(nota.IbsValorMun))
                    ),
                    El("vIBS", Dec(nota.IbsValor)),
                    El("vCredPres", "0.00"),
                    El("vCredPresCondSus", "0.00")
                ),
                new XElement(Nfe + "gCBS",
                    El("vDif", "0.00"),
                    El("vDevTrib", "0.00"),
                    El("vCBS", Dec(nota.CbsValor)),
                    El("vCredPres", "0.00"),
                    El("vCredPresCondSus", "0.00")
                )
            );
        }

        /// <summary>Normaliza indIEDest (1=Contribuinte, 2=Isento, 9=Não contribuinte) — compartilhado com <see cref="FiscalPayloadBuilder"/>.</summary>
        public static string NormalizarIndIEDest(string? ind, string? ie)
        {
            string v = (ind ?? "").Trim();
            if (v is "1" or "2" or "9") return v;
            if (string.Equals(ie?.Trim(), "ISENTO", StringComparison.OrdinalIgnoreCase)) return "2";
            return string.IsNullOrWhiteSpace(ie) ? "9" : "1";
        }

        private static XElement El(string name, string? value) =>
            new(Nfe + name, value ?? "");

        private static XElement? Opt(string name, string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : new XElement(Nfe + name, value.Trim());

        private static string Dec(decimal v, string format = "0.00") =>
            v.ToString(format, CultureInfo.InvariantCulture);

        private static string SomenteDigitos(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : new string(Array.FindAll(s.ToCharArray(), char.IsDigit));

        private static string SomenteDigitosOuIsento(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            if (string.Equals(s.Trim(), "ISENTO", StringComparison.OrdinalIgnoreCase)) return "ISENTO";
            return SomenteDigitos(s);
        }

        private static string UfToCodigo(string? uf) => (uf ?? "").Trim().ToUpperInvariant() switch
        {
            "AC" => "12", "AL" => "27", "AP" => "16", "AM" => "13", "BA" => "29",
            "CE" => "23", "DF" => "53", "ES" => "32", "GO" => "52", "MA" => "21",
            "MT" => "51", "MS" => "50", "MG" => "31", "PA" => "15", "PB" => "25",
            "PR" => "41", "PE" => "26", "PI" => "22", "RJ" => "33", "RN" => "24",
            "RS" => "43", "RO" => "11", "RR" => "14", "SC" => "42", "SP" => "35",
            "SE" => "28", "TO" => "17",
            _ => "41"
        };
    }
}
