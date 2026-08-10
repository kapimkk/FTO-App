using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace FTO_App.Services.Danfse
{
    /// <summary>
    /// Extrai o modelo do DANFSe a partir do XML autorizado da NFS-e (somente tags presentes).
    /// </summary>
    public static class NfseXmlDanfseParser
    {
        public static DanfseDocumentModel Parse(string xmlAutorizado)
        {
            if (string.IsNullOrWhiteSpace(xmlAutorizado))
                throw new DanfseXmlException("XML da NFS-e autorizado está vazio.");

            XDocument doc;
            try
            {
                doc = XDocument.Parse(xmlAutorizado.Trim(), LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                throw new DanfseXmlException($"XML da NFS-e inválido: {ex.Message}");
            }

            XElement? inf = FindByLocalName(doc.Root, "infNFSe")
                            ?? FindByLocalName(doc, "infNFSe");
            if (inf is null)
                throw new DanfseXmlException("XML sem elemento infNFSe — não é uma NFS-e autorizada válida.");

            XElement? dps = Child(inf, "DPS");
            XElement? infDps = dps is null ? null : (Child(dps, "infDPS") ?? dps);
            XElement? emit = Child(inf, "emit");
            XElement? prest = infDps is null ? null : Child(infDps, "prest");
            XElement? toma = infDps is null ? null : Child(infDps, "toma");
            XElement? interm = infDps is null ? null : Child(infDps, "interm");
            XElement? fornec = infDps is null ? null : Child(infDps, "fornec"); // destinatário em alguns layouts
            XElement? serv = infDps is null ? null : Child(infDps, "serv");
            XElement? valoresDps = infDps is null ? null : Child(infDps, "valores");
            XElement? valoresNfse = Child(inf, "valores");
            XElement? regTrib = prest is null ? null : Child(prest, "regTrib");

            var faltantes = new List<string>();

            string chave = ExtrairChave(inf);
            if (chave.Length != 50) faltantes.Add("chaveAcesso (50 dígitos / Id NFS…)");

            string nNfse = TextOf(inf, "nNFSe");
            if (string.IsNullOrWhiteSpace(nNfse)) faltantes.Add("nNFSe");

            string dhProc = TextOf(inf, "dhProc");
            string dCompet = infDps is null ? "" : TextOf(infDps, "dCompet");
            if (string.IsNullOrWhiteSpace(dCompet)) faltantes.Add("dCompet");

            string serie = infDps is null ? "" : TextOf(infDps, "serie");
            string nDps = infDps is null ? "" : TextOf(infDps, "nDPS");
            if (string.IsNullOrWhiteSpace(serie)) faltantes.Add("serie (DPS)");
            if (string.IsNullOrWhiteSpace(nDps)) faltantes.Add("nDPS");

            if (prest is null && emit is null) faltantes.Add("prest/emit (prestador)");
            if (serv is null) faltantes.Add("serv (serviço)");

            string vServ = FirstNonEmpty(
                valoresNfse is null ? null : TextOf(valoresNfse, "vLiq"),
                valoresDps is null ? null : TextOf(Child(valoresDps, "vServPrest"), "vServ"),
                valoresDps is null ? null : TextOf(valoresDps, "vServ"),
                valoresNfse is null ? null : TextOf(valoresNfse, "vServ"));
            if (string.IsNullOrWhiteSpace(vServ))
            {
                // vLiq is liquid; try vCalc total group
                vServ = FirstNonEmpty(
                    valoresNfse is null ? null : TextOf(valoresNfse, "vCalcTot"),
                    TextDeep(valoresDps, "vServ"));
            }
            if (string.IsNullOrWhiteSpace(vServ)) faltantes.Add("valores/vServ ou vLiq");

            if (faltantes.Count > 0)
            {
                throw new DanfseXmlException(
                    "XML da NFS-e incompleto para DANFSe (NT 008). Campos obrigatórios ausentes: " +
                    string.Join(", ", faltantes),
                    faltantes);
            }

            string tpAmb = infDps is null ? "1" : TextOf(infDps, "tpAmb");
            if (string.IsNullOrWhiteSpace(tpAmb)) tpAmb = "1";

            string cStat = TextOf(inf, "cStat");
            bool cancelada = DetectCancelada(doc, cStat);
            bool substituida = DetectSubstituida(infDps, doc);

            var prestador = ParsePessoa(prest) ?? ParseEmitenteComoPrestador(emit) ?? new DanfsePessoaModel();
            var tomador = ParsePessoa(toma);
            var intermediario = ParsePessoa(interm);
            var destinatario = ParsePessoa(fornec);
            bool destEhToma = destinatario is null && tomador is not null;

            XElement? trib = valoresDps is null ? null : Child(valoresDps, "trib");
            XElement? tribMun = trib is null ? null : Child(trib, "tribMun");
            XElement? tribFed = trib is null ? null : Child(trib, "tribFed");
            XElement? totTrib = trib is null ? null : Child(trib, "totTrib");
            XElement? pTotTrib = totTrib is null ? null : Child(totTrib, "pTotTrib");

            string tribIssqn = tribMun is null ? "" : TextOf(tribMun, "tribISSQN");
            bool semIssqn = string.IsNullOrWhiteSpace(tribIssqn) || tribIssqn == "4" || tribIssqn == "3";
            // 4 often = not subject depending on table; keep SemIssqn if no tribMun at all
            if (tribMun is null) semIssqn = true;

            XElement? ibsDps = infDps is null ? null : Child(infDps, "IBSCBS");
            XElement? ibsNfse = Child(inf, "IBSCBS");
            bool temIbs = ibsDps is not null || ibsNfse is not null;

            string vLiq = FirstNonEmpty(
                valoresNfse is null ? null : TextOf(valoresNfse, "vLiq"),
                vServ);

            string? fin = infDps is null ? null : TextDeep(infDps, "finNFSe");

            return new DanfseDocumentModel
            {
                ChaveAcesso = chave,
                NumeroNfse = nNfse,
                Competencia = FormatCompetencia(dCompet),
                DhProcNfse = FormatDh(dhProc),
                NumeroDps = nDps,
                SerieDps = serie,
                DhEmiDps = FormatDh(infDps is null ? "" : TextOf(infDps, "dhEmi")),
                EmitenteTipo = MapEmitente(infDps is null ? "" : TextOf(infDps, "tpEmit")),
                Situacao = MapSituacao(cStat, cancelada, substituida),
                CStat = cStat,
                Finalidade = string.IsNullOrWhiteSpace(fin) ? TextOf(inf, "finNFSe") : fin!,
                TpAmb = tpAmb.Trim(),
                AmbGer = TextOf(inf, "ambGer"),
                MunicipioEmitenteNome = TextOf(inf, "xLocEmi"),
                XLocEmi = TextOf(inf, "xLocEmi"),
                XLocPrestacao = TextOf(inf, "xLocPrestacao"),
                Prestador = prestador,
                Tomador = tomador,
                Destinatario = destinatario,
                Intermediario = intermediario,
                DestinatarioEhTomador = destEhToma,
                CodTribNac = serv is null ? "" : TextOf(Child(serv, "cServ") ?? serv, "cTribNac"),
                CodTribMun = serv is null ? "" : TextOf(Child(serv, "cServ") ?? serv, "cTribMun"),
                DescTribNac = TextOf(inf, "xTribNac"),
                DescTribMun = TextOf(inf, "xTribMun"),
                CodNbs = FirstNonEmpty(
                    serv is null ? null : TextOf(Child(serv, "cServ") ?? serv, "cNBS"),
                    TextOf(inf, "cNBS")),
                DescNbs = TextOf(inf, "xNBS"),
                CodLocPrestacao = FirstNonEmpty(
                    serv is null ? null : TextOf(Child(serv, "locPrest") ?? serv, "cLocPrestacao"),
                    TextOf(inf, "cLocIncid")),
                DescServico = serv is null ? "" : TextOf(Child(serv, "cServ") ?? serv, "xDescServ"),
                TribIssqn = string.IsNullOrWhiteSpace(tribIssqn) ? null : tribIssqn,
                LocIncidenciaIss = FirstNonEmpty(TextOf(inf, "xLocIncid"), TextOf(inf, "cLocIncid")),
                RegEspTrib = regTrib is null ? null : NullIfEmpty(TextOf(regTrib, "regEspTrib")),
                BcIssqn = tribMun is null ? null : NullIfEmpty(TextOf(tribMun, "vBC")),
                AliqIssqn = tribMun is null ? null : NullIfEmpty(TextOf(tribMun, "pAliq")),
                ValorIssqn = FirstNonEmpty(
                    valoresNfse is null ? null : TextOf(valoresNfse, "vISSQN"),
                    tribMun is null ? null : TextOf(tribMun, "vISSQN")),
                TpRetIssqn = tribMun is null ? null : NullIfEmpty(TextOf(tribMun, "tpRetISSQN")),
                SemIssqn = semIssqn && tribMun is null,
                ValorPis = FirstNonEmpty(
                    valoresNfse is null ? null : TextOf(valoresNfse, "vPIS"),
                    tribFed is null ? null : TextOf(tribFed, "vPIS")),
                ValorCofins = FirstNonEmpty(
                    valoresNfse is null ? null : TextOf(valoresNfse, "vCOFINS"),
                    tribFed is null ? null : TextOf(tribFed, "vCOFINS")),
                ValorIrrf = FirstNonEmpty(
                    valoresNfse is null ? null : TextOf(valoresNfse, "vIR"),
                    tribFed is null ? null : TextOf(tribFed, "vIRRF")),
                TpRetPisCofins = tribFed is null ? null : NullIfEmpty(TextOf(tribFed, "tpRetPISCOFINS")),
                TemIbsCbs = temIbs,
                CstIbsCbs = NullIfEmpty(TextDeep(ibsDps ?? ibsNfse, "CST")),
                ClassTrib = NullIfEmpty(TextDeep(ibsDps ?? ibsNfse, "cClassTrib")),
                ValorIbs = NullIfEmpty(TextDeep(ibsNfse ?? ibsDps, "vIBS")),
                ValorCbs = NullIfEmpty(TextDeep(ibsNfse ?? ibsDps, "vCBS")),
                ValorServico = FormatDec(vServ),
                ValorLiquido = FormatDec(vLiq),
                ValorTotalNfse = NullIfEmpty(FormatDec(valoresNfse is null ? "" : TextOf(valoresNfse, "vTotNF"))),
                InfComplementares = NullIfEmpty(TextOf(inf, "xOutInf")),
                PTotTribFed = pTotTrib is null ? null : NullIfEmpty(TextOf(pTotTrib, "pTotTribFed")),
                PTotTribEst = pTotTrib is null ? null : NullIfEmpty(TextOf(pTotTrib, "pTotTribEst")),
                PTotTribMun = pTotTrib is null ? null : NullIfEmpty(TextOf(pTotTrib, "pTotTribMun")),
                OpSimpNac = regTrib is null ? "" : TextOf(regTrib, "opSimpNac"),
                RegApTribSN = regTrib is null ? null : NullIfEmpty(TextOf(regTrib, "regApTribSN")),
                Cancelada = cancelada,
                Substituida = substituida
            };
        }

        private static string ExtrairChave(XElement inf)
        {
            string id = (string?)inf.Attribute("Id")
                        ?? (string?)inf.Attribute("id")
                        ?? "";

            if (id.StartsWith("NFS", StringComparison.OrdinalIgnoreCase))
            {
                string after = new string(id.Substring(3).Where(char.IsDigit).ToArray());
                if (after.Length >= 50) return after[..50];
            }

            string dig = new string(id.Where(char.IsDigit).ToArray());
            if (dig.Length >= 50) return dig[^50..];

            string ch = TextDeep(inf, "chaveAcesso") ?? TextDeep(inf, "chNFSe") ?? "";
            dig = new string(ch.Where(char.IsDigit).ToArray());
            return dig.Length >= 50 ? dig[..50] : dig;
        }

        private static DanfsePessoaModel? ParsePessoa(XElement? el)
        {
            if (el is null) return null;
            string cnpj = TextOf(el, "CNPJ");
            string cpf = TextOf(el, "CPF");
            string nif = TextOf(el, "NIF");
            string doc, tipo;
            if (!string.IsNullOrWhiteSpace(cnpj)) { doc = cnpj; tipo = "CNPJ"; }
            else if (!string.IsNullOrWhiteSpace(cpf)) { doc = cpf; tipo = "CPF"; }
            else if (!string.IsNullOrWhiteSpace(nif)) { doc = nif; tipo = "NIF"; }
            else { doc = ""; tipo = ""; }

            XElement? end = Child(el, "end") ?? Child(el, "endereco");
            XElement? endNac = end is null ? null : (Child(end, "endNac") ?? end);

            return new DanfsePessoaModel
            {
                Documento = doc,
                TipoDocumento = tipo,
                Nome = FirstNonEmpty(TextOf(el, "xNome"), TextOf(el, "xFant")),
                Im = TextOf(el, "IM"),
                Telefone = TextOf(el, "fone"),
                Email = TextOf(el, "email"),
                Logradouro = endNac is null ? TextOf(end, "xLgr") : TextOf(endNac, "xLgr"),
                Numero = endNac is null ? TextOf(end, "nro") : TextOf(endNac, "nro"),
                Complemento = endNac is null ? TextOf(end, "xCpl") : TextOf(endNac, "xCpl"),
                Bairro = endNac is null ? TextOf(end, "xBairro") : TextOf(endNac, "xBairro"),
                CodIbge = endNac is null ? TextOf(end, "cMun") : TextOf(endNac, "cMun"),
                Uf = endNac is null ? TextOf(end, "UF") : FirstNonEmpty(TextOf(endNac, "UF"), TextOf(endNac, "uf")),
                Cep = endNac is null ? TextOf(end, "CEP") : FirstNonEmpty(TextOf(endNac, "CEP"), TextOf(endNac, "cep")),
                Municipio = ""
            };
        }

        private static DanfsePessoaModel? ParseEmitenteComoPrestador(XElement? emit)
        {
            if (emit is null) return null;
            return new DanfsePessoaModel
            {
                Documento = FirstNonEmpty(TextOf(emit, "CNPJ"), TextOf(emit, "CPF")),
                TipoDocumento = !string.IsNullOrWhiteSpace(TextOf(emit, "CNPJ")) ? "CNPJ" : "CPF",
                Nome = TextOf(emit, "xNome"),
                Im = TextOf(emit, "IM"),
                Telefone = TextOf(emit, "fone"),
                Email = TextOf(emit, "email")
            };
        }

        private static bool DetectCancelada(XDocument doc, string cStat)
        {
            if (cStat is "101" or "102") return true;
            return doc.Descendants().Any(e =>
                e.Name.LocalName.Contains("Cancel", StringComparison.OrdinalIgnoreCase) ||
                (e.Name.LocalName == "xDesc" && (e.Value?.Contains("Cancelad", StringComparison.OrdinalIgnoreCase) ?? false)));
        }

        private static bool DetectSubstituida(XElement? infDps, XDocument doc)
        {
            if (infDps is not null && Child(infDps, "subst") is not null) return true;
            return doc.Descendants().Any(e =>
                e.Name.LocalName.Contains("Substit", StringComparison.OrdinalIgnoreCase));
        }

        private static string MapEmitente(string tpEmit) => tpEmit switch
        {
            "1" => "Prestador",
            "2" => "Tomador",
            "3" => "Intermediário",
            _ => tpEmit
        };

        private static string MapSituacao(string cStat, bool cancelada, bool substituida)
        {
            if (cancelada) return "Cancelada";
            if (substituida) return "Substituída";
            if (cStat == "100" || string.IsNullOrWhiteSpace(cStat)) return "Normal";
            return cStat;
        }

        private static string FormatCompetencia(string d)
        {
            if (DateTime.TryParse(d, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToString("MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
            return d;
        }

        private static string FormatDh(string dh)
        {
            if (string.IsNullOrWhiteSpace(dh)) return "";
            if (DateTimeOffset.TryParse(dh, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                return dto.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("pt-BR"));
            return dh;
        }

        private static string FormatDec(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
            return raw;
        }

        private static XElement? FindByLocalName(XNode? root, string local)
        {
            if (root is not XContainer container) return null;
            return container.Descendants().FirstOrDefault(e => e.Name.LocalName == local);
        }

        private static XElement? Child(XElement parent, string local) =>
            parent.Elements().FirstOrDefault(e => e.Name.LocalName == local);

        private static string TextOf(XElement? parent, string local)
        {
            if (parent is null) return "";
            return Child(parent, local)?.Value?.Trim() ?? "";
        }

        private static string? TextDeep(XElement? parent, string local)
        {
            if (parent is null) return null;
            return parent.Descendants().FirstOrDefault(e => e.Name.LocalName == local)?.Value?.Trim();
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            return "";
        }

        private static string? NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
