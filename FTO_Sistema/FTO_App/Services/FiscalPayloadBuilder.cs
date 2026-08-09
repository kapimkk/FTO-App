using System;
using System.Globalization;
using System.Text.Json.Nodes;
using FTO_App.Models;

namespace FTO_App.Services
{
    /// <summary>
    /// Monta o corpo JSON de emissão (POST /emitir) exigido pela API Fiscal PFCode para NF-e (mod 55)
    /// e NFC-e (mod 65), a partir do NotaFiscalModel + EmpresaConfig — mesma decisão fiscal já usada em
    /// <see cref="NfeXmlService"/> (CST×CSOSN por CRT, PIS/COFINS NT, grupo IBSCBS), mas serializada como
    /// JSON e não como XML (a API monta/assina/transmite o XML no servidor).
    ///
    /// IMPORTANTE — grupos "choice" do XSD (ICMS/PIS/COFINS/IBSCBS): a API resolve o tipo concreto pelo
    /// conteúdo de um wrapper (icmsDetails/pisDetails/cofinsDetails/tribDetails), lendo CST/CSOSN de
    /// dentro dele (ver Fiscal.Shared.Services.JsonToXsdResolverService). Não existe endpoint que aceite
    /// "ICMS00"/"ICMSSN102" como chave — sempre use o wrapper "*Details" com os campos linearizados.
    /// </summary>
    public static class FiscalPayloadBuilder
    {
        /// <summary>Mantido por compatibilidade — usar <see cref="FiscalHomologacaoTextos.XNomeDest"/>.</summary>
        public const string NomeDestHomologacao = FiscalHomologacaoTextos.XNomeDest;

        public static JsonObject BuildEmissao(NotaFiscalModel nota, EmpresaConfig emitente)
        {
            ArgumentNullException.ThrowIfNull(nota);
            ArgumentNullException.ThrowIfNull(emitente);

            bool isNfce = string.Equals((nota.Modelo ?? "55").Trim(), "65", StringComparison.Ordinal);
            string ambiente = FiscalApiClient.NormalizarTpAmb(nota.Ambiente);
            bool homolog = ambiente == "2";
            string crt = string.IsNullOrWhiteSpace(emitente.RegimeTributario) ? "1" : emitente.RegimeTributario.Trim();

            string cnpjEmit = SomenteDigitos(emitente.Cnpj);
            string docDest = SomenteDigitos(nota.DestCpfCnpj);
            bool temDest = !isNfce || !string.IsNullOrWhiteSpace(docDest);
            bool destPj = docDest.Length > 11;

            string idDest = string.IsNullOrWhiteSpace(nota.IdDest)
                ? NfeXmlService.InferirIdDest(nota.ProdutoCfop, emitente.Uf, nota.DestUf)
                : nota.IdDest.Trim();

            var infNFe = new JsonObject
            {
                ["versao"] = "4.00",
                ["ide"] = MontarIde(nota, emitente, isNfce, idDest, ambiente),
                ["emit"] = MontarEmit(emitente, cnpjEmit, crt)
            };

            if (temDest)
                infNFe["dest"] = MontarDest(nota, docDest, destPj, homolog);

            SincronizarTotais(nota);
            var det = new JsonArray { MontarDet(nota, crt, homolog) };
            infNFe["det"] = det;
            infNFe["total"] = MontarTotal(nota, crt);
            infNFe["transp"] = new JsonObject { ["modFrete"] = "9" };
            infNFe["pag"] = MontarPag(nota);

            if (!string.IsNullOrWhiteSpace(nota.InformacoesComplementares))
                infNFe["infAdic"] = new JsonObject { ["infCpl"] = nota.InformacoesComplementares.Trim() };

            return new JsonObject { ["infNFe"] = infNFe };
        }

        private static JsonObject MontarIde(NotaFiscalModel nota, EmpresaConfig emitente, bool isNfce, string idDest, string ambiente)
        {
            return new JsonObject
            {
                ["cUF"] = UfToCodigo(emitente.Uf),
                ["natOp"] = nota.NaturezaOperacao,
                ["mod"] = isNfce ? "65" : "55",
                ["serie"] = nota.Serie,
                ["nNF"] = nota.Numero.ToString(CultureInfo.InvariantCulture),
                ["dhEmi"] = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                ["tpNF"] = nota.TipoOperacao,
                ["idDest"] = idDest,
                ["cMunFG"] = emitente.CodigoIbge,
                ["tpImp"] = isNfce ? "4" : "1",
                ["tpEmis"] = "1",
                ["tpAmb"] = ambiente,
                ["finNFe"] = nota.Finalidade,
                ["indFinal"] = isNfce ? "1" : nota.ConsumidorFinal,
                ["indPres"] = isNfce ? "1" : nota.PresencaComprador,
                ["procEmi"] = "0",
                ["verProc"] = "FTO_1.0"
            };
        }

        private static JsonObject MontarEmit(EmpresaConfig emitente, string cnpjEmit, string crt)
        {
            var enderEmit = new JsonObject
            {
                ["xLgr"] = emitente.Endereco,
                ["nro"] = string.IsNullOrWhiteSpace(emitente.Numero) ? "S/N" : emitente.Numero,
                ["xBairro"] = emitente.Bairro,
                ["cMun"] = emitente.CodigoIbge,
                ["xMun"] = emitente.Cidade,
                ["UF"] = emitente.Uf,
                ["CEP"] = SomenteDigitos(emitente.Cep),
                ["cPais"] = "1058",
                ["xPais"] = "BRASIL"
            };
            if (!string.IsNullOrWhiteSpace(emitente.Complemento)) enderEmit["xCpl"] = emitente.Complemento.Trim();
            string fone = SomenteDigitos(emitente.Telefone);
            if (!string.IsNullOrEmpty(fone)) enderEmit["fone"] = fone;

            var emit = new JsonObject
            {
                ["CNPJ"] = cnpjEmit,
                ["xNome"] = string.IsNullOrWhiteSpace(emitente.RazaoSocial) ? emitente.Nome : emitente.RazaoSocial,
                ["enderEmit"] = enderEmit,
                ["IE"] = SomenteDigitosOuIsento(emitente.Ie),
                ["CRT"] = crt
            };
            string xFant = string.IsNullOrWhiteSpace(emitente.NomeFantasia) ? emitente.Subtitulo : emitente.NomeFantasia;
            if (!string.IsNullOrWhiteSpace(xFant)) emit["xFant"] = xFant.Trim();
            return emit;
        }

        private static JsonObject MontarDest(NotaFiscalModel nota, string docDest, bool destPj, bool homolog)
        {
            string destNome = homolog ? NomeDestHomologacao : nota.DestNome;
            var (indIEDest, ieDest) = NfeXmlService.ConciliarIndIeDest(nota.IndIEDest, nota.DestIe);
            nota.IndIEDest = indIEDest;
            nota.DestIe = ieDest;

            var enderDest = new JsonObject
            {
                ["xLgr"] = nota.DestLogradouro,
                ["nro"] = string.IsNullOrWhiteSpace(nota.DestNumero) ? "S/N" : nota.DestNumero,
                ["xBairro"] = nota.DestBairro,
                ["cMun"] = nota.DestCodigoIbge,
                ["xMun"] = nota.DestMunicipio,
                ["UF"] = nota.DestUf,
                ["CEP"] = SomenteDigitos(nota.DestCep)
            };
            if (!string.IsNullOrWhiteSpace(nota.DestComplemento)) enderDest["xCpl"] = nota.DestComplemento.Trim();

            var dest = new JsonObject { ["xNome"] = destNome };
            if (!string.IsNullOrEmpty(docDest))
                dest[destPj ? "CNPJ" : "CPF"] = docDest;
            dest["enderDest"] = enderDest;
            dest["indIEDest"] = indIEDest;
            // Rejeição 232: indIEDest=1 exige o elemento IE
            if (indIEDest == "1") dest["IE"] = ieDest;
            if (indIEDest == "2") dest["IE"] = "ISENTO";
            if (!string.IsNullOrWhiteSpace(nota.DestEmail)) dest["email"] = nota.DestEmail.Trim();
            return dest;
        }

        private static JsonObject MontarDet(NotaFiscalModel nota, string crt, bool homolog)
        {
            string gtin = string.IsNullOrWhiteSpace(nota.ProdutoGtin) || nota.ProdutoGtin == "SEM GTIN" ? "SEM GTIN" : nota.ProdutoGtin;
            string ncm = ReformaTributariaService.NormalizarNcm(nota.ProdutoNcm);
            var prod = new JsonObject
            {
                ["cProd"] = string.IsNullOrWhiteSpace(nota.ProdutoCodigo) ? "001" : nota.ProdutoCodigo,
                ["cEAN"] = gtin,
                ["xProd"] = NfeXmlService.AplicarHomologDescricao(nota.ProdutoDescricao, homolog),
                // NCM vazio falha no XSD (pattern) — validação prévia em NotaFiscalAcoesWindow
                ["NCM"] = ncm
            };
            string cest = SomenteDigitos(nota.ProdutoCest);
            if (!string.IsNullOrEmpty(cest)) prod["CEST"] = cest;
            prod["CFOP"] = nota.ProdutoCfop;
            prod["uCom"] = nota.ProdutoUnidade;
            prod["qCom"] = N(nota.ProdutoQuantidade, 4);
            prod["vUnCom"] = N(nota.ProdutoValorUnitario, 4);
            prod["vProd"] = N(nota.ProdutoValorTotal);
            prod["cEANTrib"] = gtin;
            prod["uTrib"] = nota.ProdutoUnidade;
            prod["qTrib"] = N(nota.ProdutoQuantidade, 4);
            prod["vUnTrib"] = N(nota.ProdutoValorUnitario, 4);
            prod["indTot"] = "1";

            var imposto = new JsonObject
            {
                ["ICMS"] = new JsonObject { ["icmsDetails"] = MontarIcmsDetails(nota, crt) },
                ["PIS"] = new JsonObject { ["pisDetails"] = MontarPisDetails(nota) },
                ["COFINS"] = new JsonObject { ["cofinsDetails"] = MontarCofinsDetails(nota) },
                ["IBSCBS"] = MontarIbsCbsItem(nota)
            };

            return new JsonObject
            {
                ["nItem"] = "1",
                ["prod"] = prod,
                ["imposto"] = imposto
            };
        }

        private static JsonObject MontarIcmsDetails(NotaFiscalModel nota, string crt)
        {
            string orig = string.IsNullOrWhiteSpace(nota.IcmsOrigem) ? "0" : nota.IcmsOrigem.Trim();

            if (crt == "3")
            {
                string cst = string.IsNullOrWhiteSpace(nota.IcmsCst) ? "00" : nota.IcmsCst.Trim();
                var o = new JsonObject { ["orig"] = orig, ["CST"] = cst };
                switch (cst)
                {
                    case "40": case "41": case "50":
                        break;
                    default:
                        // Rejeição 564: vICMS deve fechar Base × Alíquota (tolerância de centavos)
                        decimal vBc = nota.ProdutoValorTotal;
                        decimal pIcms = nota.IcmsAliquota;
                        decimal vIcms = Math.Round(vBc * pIcms / 100m, 2);
                        o["modBC"] = "3";
                        o["vBC"] = N(vBc);
                        o["pICMS"] = N(pIcms, 4);
                        o["vICMS"] = N(vIcms);
                        nota.IcmsValor = vIcms;
                        break;
                }
                return o;
            }

            string csosn = string.IsNullOrWhiteSpace(nota.Csosn) ? "102" : nota.Csosn.Trim();
            var r = new JsonObject { ["orig"] = orig, ["CSOSN"] = csosn };
            if (csosn == "101")
            {
                r["pCredSN"] = N(nota.IcmsAliquota, 4);
                r["vCredICMSSN"] = N(nota.IcmsValor);
            }
            return r;
        }

        private static JsonObject MontarPisDetails(NotaFiscalModel nota)
        {
            string cst = string.IsNullOrWhiteSpace(nota.PisCst) ? "01" : nota.PisCst.Trim();
            var o = new JsonObject { ["CST"] = cst };
            if (cst is "04" or "05" or "06" or "07" or "08" or "09") return o;
            o["vBC"] = N(nota.ProdutoValorTotal);
            o["pPIS"] = N(nota.PisAliquota, 4);
            o["vPIS"] = N(nota.PisValor);
            return o;
        }

        private static JsonObject MontarCofinsDetails(NotaFiscalModel nota)
        {
            string cst = string.IsNullOrWhiteSpace(nota.CofinsCst) ? "01" : nota.CofinsCst.Trim();
            var o = new JsonObject { ["CST"] = cst };
            if (cst is "04" or "05" or "06" or "07" or "08" or "09") return o;
            o["vBC"] = N(nota.ProdutoValorTotal);
            o["pCOFINS"] = N(nota.CofinsAliquota, 4);
            o["vCOFINS"] = N(nota.CofinsValor);
            return o;
        }

        /// <summary>
        /// Grupo IBSCBS do item — obrigatório desde 2026 (cStat 1115 se ausente).
        /// Alíquotas forçadas pela NT 2025.002 no ano da emissão (rejeição 1026 se pIBSUF ≠ 0,1% em 2026).
        /// cClassTrib normalizado para 6 dígitos (XSD TcClassTrib rejeita "0").
        /// </summary>
        private static JsonObject MontarIbsCbsItem(NotaFiscalModel nota)
        {
            var r = ReformaTributariaService.CalcularParaEmissao(nota.ProdutoValorTotal, nota);
            return new JsonObject
            {
                ["CST"] = r.Cst,
                ["cClassTrib"] = r.ClassTrib,
                ["tribDetails"] = new JsonObject
                {
                    ["vBC"] = N(r.BaseCalculo),
                    ["gIBSUF"] = new JsonObject { ["pIBSUF"] = N(r.AliquotaIbsUf, 4), ["vIBSUF"] = N(r.ValorIbsUf) },
                    ["gIBSMun"] = new JsonObject { ["pIBSMun"] = N(r.AliquotaIbsMun, 4), ["vIBSMun"] = N(r.ValorIbsMun) },
                    ["vIBS"] = N(r.ValorIbs),
                    ["gCBS"] = new JsonObject { ["pCBS"] = N(r.AliquotaCbs, 4), ["vCBS"] = N(r.ValorCbs) }
                }
            };
        }

        /// <summary>
        /// Alinha ValorProdutos/ValorTotalNota e impostos com o item.
        /// Corrige rascunho com ProdutoValorTotal preenchido e ValorProdutos=0
        /// (MapRow antigo omitia o campo) → SEFAZ: "vProd informado: 0 / calculado: 1.0".
        /// </summary>
        private static void SincronizarTotais(NotaFiscalModel nota)
        {
            decimal item = nota.ProdutoValorTotal;
            if (item <= 0 && nota.ProdutoQuantidade > 0 && nota.ProdutoValorUnitario > 0)
                item = Math.Round(nota.ProdutoQuantidade * nota.ProdutoValorUnitario, 2);

            if (item > 0)
            {
                nota.ProdutoValorTotal = item;
                if (nota.ValorProdutos <= 0) nota.ValorProdutos = item;
                if (nota.ValorTotalNota <= 0)
                    nota.ValorTotalNota = nota.ValorProdutos + nota.ValorFrete - nota.ValorDesconto;
            }

            if (nota.IcmsAliquota > 0 && nota.IcmsValor <= 0)
                nota.IcmsValor = Math.Round(nota.ProdutoValorTotal * nota.IcmsAliquota / 100m, 2);
            if (nota.PisAliquota > 0 && nota.PisValor <= 0)
                nota.PisValor = Math.Round(nota.ProdutoValorTotal * nota.PisAliquota / 100m, 2);
            if (nota.CofinsAliquota > 0 && nota.CofinsValor <= 0)
                nota.CofinsValor = Math.Round(nota.ProdutoValorTotal * nota.CofinsAliquota / 100m, 2);
        }

        private static JsonObject MontarTotal(NotaFiscalModel nota, string crt)
        {
            SincronizarTotais(nota);
            var r = ReformaTributariaService.CalcularParaEmissao(nota.ProdutoValorTotal, nota);

            // Simples/MEI: ICMSTot.vBC/vICMS zerados (destaque só via CSOSN no item)
            bool regimeNormal = crt == "3";
            decimal vBcTot = regimeNormal && nota.IcmsValor > 0 ? nota.ProdutoValorTotal : 0m;
            decimal vIcmsTot = regimeNormal ? nota.IcmsValor : 0m;
            decimal vProd = nota.ValorProdutos > 0 ? nota.ValorProdutos : nota.ProdutoValorTotal;
            decimal vNf = nota.ValorTotalNota > 0 ? nota.ValorTotalNota : vProd;

            var icmsTot = new JsonObject
            {
                ["vBC"] = N(vBcTot),
                ["vICMS"] = N(vIcmsTot),
                ["vICMSDeson"] = N(0m),
                ["vFCP"] = N(0m),
                ["vBCST"] = N(0m),
                ["vST"] = N(0m),
                ["vFCPST"] = N(0m),
                ["vFCPSTRet"] = N(0m),
                ["vProd"] = N(vProd),
                ["vFrete"] = N(nota.ValorFrete),
                ["vSeg"] = N(0m),
                ["vDesc"] = N(nota.ValorDesconto),
                ["vII"] = N(0m),
                ["vIPI"] = N(0m),
                ["vIPIDevol"] = N(0m),
                ["vPIS"] = N(nota.PisValor),
                ["vCOFINS"] = N(nota.CofinsValor),
                ["vOutro"] = N(0m),
                ["vNF"] = N(vNf)
            };

            var ibsCbsTot = new JsonObject
            {
                ["vBCIBSCBS"] = N(r.BaseCalculo),
                ["gIBS"] = new JsonObject
                {
                    ["gIBSUF"] = new JsonObject { ["vDif"] = N(0m), ["vDevTrib"] = N(0m), ["vIBSUF"] = N(r.ValorIbsUf) },
                    ["gIBSMun"] = new JsonObject { ["vDif"] = N(0m), ["vDevTrib"] = N(0m), ["vIBSMun"] = N(r.ValorIbsMun) },
                    ["vIBS"] = N(r.ValorIbs),
                    ["vCredPres"] = N(0m),
                    ["vCredPresCondSus"] = N(0m)
                },
                ["gCBS"] = new JsonObject
                {
                    ["vDif"] = N(0m),
                    ["vDevTrib"] = N(0m),
                    ["vCBS"] = N(r.ValorCbs),
                    ["vCredPres"] = N(0m),
                    ["vCredPresCondSus"] = N(0m)
                }
            };

            return new JsonObject { ["ICMSTot"] = icmsTot, ["IBSCBSTot"] = ibsCbsTot };
        }

        private static JsonObject MontarPag(NotaFiscalModel nota)
        {
            decimal vPag = nota.ValorTotalNota > 0 ? nota.ValorTotalNota : nota.ProdutoValorTotal;
            string tPag = string.IsNullOrWhiteSpace(nota.FormaPagamento) ? "01" : nota.FormaPagamento.Trim();

            var detPag = new JsonObject
            {
                ["indPag"] = "0",
                ["tPag"] = tPag,
                ["vPag"] = N(vPag)
            };

            // NT 2024/2025: cartão (03/04) e PIX dinâmico (17) exigem grupo card (rejeição 391).
            // Sem TEF: tpIntegra=2 (pagamento não integrado ao sistema).
            if (tPag is "03" or "04" or "17")
            {
                detPag["card"] = new JsonObject
                {
                    ["tpIntegra"] = "2"
                };
            }

            return new JsonObject { ["detPag"] = new JsonArray { detPag } };
        }

        private static JsonValue N(decimal v, int casas = 2) => JsonValue.Create(Math.Round(v, casas));

        private static string SomenteDigitos(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : new string(Array.FindAll(s.ToCharArray(), char.IsDigit));

        private static string SomenteDigitosOuIsento(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            if (string.Equals(s.Trim(), "ISENTO", StringComparison.OrdinalIgnoreCase)) return "ISENTO";
            return SomenteDigitos(s);
        }

               /// <summary>Código IBGE da UF (cUF) — reaproveitado pela tela de inutilização de numeração.</summary>
               public static string UfToCodigo(string? uf) => (uf ?? "").Trim().ToUpperInvariant() switch
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
