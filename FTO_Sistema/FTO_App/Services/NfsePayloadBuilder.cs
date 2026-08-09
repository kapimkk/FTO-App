using System;
using System.Globalization;
using System.Text.Json.Nodes;
using FTO_App.Models;

namespace FTO_App.Services
{
    /// <summary>
    /// Monta o JSON de emissão DPS (POST /api/v1/nfse/emitir) conforme GUIA_INTEGRACAO.md §6
    /// e DpsEmissaoRequest da Fiscal.NFSe.API (Padrão Nacional / SEFIN).
    /// </summary>
    public static class NfsePayloadBuilder
    {
        public static JsonObject BuildEmissao(NotaServicoModel nota, EmpresaConfig empresa)
        {
            ArgumentNullException.ThrowIfNull(nota);
            ArgumentNullException.ThrowIfNull(empresa);

            string ambiente = FiscalApiClient.NormalizarTpAmb(nota.Ambiente);
            // Importante: o host SEFIN usado pela Fiscal.NFSe.API é escolhido por
            // SefinNacionalSettings.AmbienteRestrito (não pelo tpAmb). Com AmbienteRestrito=true
            // (padrão), o destino é sefin.producaorestrita — que exige tpAmb=2 na DPS.
            // Enviar tpAmb=1 nesse host → rejeição E0006.
            string cnpjPrest = Digitos(empresa.Cnpj);
            string cMun = Digitos(string.IsNullOrWhiteSpace(nota.CodigoIbgeEmissao)
                ? empresa.CodigoIbge : nota.CodigoIbgeEmissao);
            string cLocPrest = Digitos(string.IsNullOrWhiteSpace(nota.CodIbgePrestacao)
                ? cMun : nota.CodIbgePrestacao);

            // Regime do prestador na DPS (não usa mais cadastro no Portal)
            string opSimp = MapearOpSimpNac(empresa.RegimeTributario, nota.OpSimpNac);

            var prestador = new JsonObject
            {
                ["cnpj"] = cnpjPrest,
                ["endereco"] = new JsonObject
                {
                    ["cMun"] = Digitos(empresa.CodigoIbge),
                    ["uf"] = (empresa.Uf ?? "").Trim().ToUpperInvariant(),
                    ["cep"] = Digitos(empresa.Cep),
                    ["xLgr"] = empresa.Endereco?.Trim() ?? "",
                    ["nro"] = string.IsNullOrWhiteSpace(empresa.Numero) ? "S/N" : empresa.Numero.Trim(),
                    ["xBairro"] = empresa.Bairro?.Trim() ?? ""
                },
                ["regimeTributario"] = new JsonObject
                {
                    ["opSimpNac"] = opSimp,
                    ["regEspTrib"] = string.IsNullOrWhiteSpace(nota.RegEspTrib) ? "0" : nota.RegEspTrib.Trim()
                }
            };
            // tpEmit=1 (Prestador): NÃO enviar xNome do prestador (SEFIN E0121)
            // IM só se cadastrada de verdade (SEFIN E0120 rejeita IM inventada)
            if (!string.IsNullOrWhiteSpace(empresa.Im))
                prestador["inscricaoMunicipal"] = empresa.Im.Trim();
            if (!string.IsNullOrWhiteSpace(empresa.Email))
                prestador["email"] = empresa.Email.Trim();
            if (!string.IsNullOrWhiteSpace(empresa.Telefone))
                prestador["fone"] = Digitos(empresa.Telefone);

            var root = new JsonObject
            {
                ["tpAmb"] = ambiente,
                ["serie"] = string.IsNullOrWhiteSpace(nota.Serie) ? "1" : nota.Serie.Trim(),
                ["nDps"] = nota.NumeroDps.ToString(CultureInfo.InvariantCulture),
                ["dCompet"] = nota.DataCompetencia.ToString("yyyy-MM-dd"),
                ["tpEmit"] = "1",
                ["cLocEmi"] = cMun,
                ["prestador"] = prestador,
                ["servico"] = MontarServico(nota, cLocPrest),
                ["valores"] = MontarValores(nota, opSimp)
            };

            var tomador = MontarTomador(nota);
            if (tomador != null)
                root["tomador"] = tomador;

            if (nota.IncluirIbsCbs)
            {
                string nbs = Digitos(nota.CodNbs);
                if (nbs.Length != 9)
                    throw new InvalidOperationException(
                        "Com IBS/CBS, o código NBS (9 dígitos) é obrigatório (SEFIN E0322).");

                ((JsonObject)root["servico"]!)["cNBS"] = nbs;
                root["ibsCbs"] = new JsonObject
                {
                    ["finNFSe"] = "0",
                    ["cIndOp"] = Digitos(nota.CodIndOp).PadLeft(6, '0'),
                    ["indDest"] = "0",
                    ["trib"] = new JsonObject
                    {
                        ["cst"] = Digitos(nota.CstIbsCbs).PadLeft(3, '0'),
                        ["cClassTrib"] = Digitos(nota.ClassTrib).PadLeft(6, '0')
                    }
                };
            }

            return root;
        }

        private static JsonObject MontarServico(NotaServicoModel nota, string cLocPrest)
        {
            string cTrib = Digitos(nota.CodTribNac);
            if (cTrib.Length != 6)
                throw new InvalidOperationException(
                    "Código de tributação nacional (cTribNac) deve ter 6 dígitos (ex.: 010101).");

            var serv = new JsonObject
            {
                ["cTribNac"] = cTrib,
                ["xDescServ"] = (nota.DescricaoServico ?? "").Trim(),
                ["cLocPrestacao"] = cLocPrest
            };
            if (!string.IsNullOrWhiteSpace(nota.CodTribMun))
                serv["cTribMun"] = Digitos(nota.CodTribMun);
            return serv;
        }

        private static JsonObject MontarValores(NotaServicoModel nota, string opSimpNac)
        {
            var valores = new JsonObject
            {
                ["vServ"] = Math.Round(nota.ValorServico, 2),
                ["tribIssqn"] = new JsonObject
                {
                    ["tribISSQN"] = string.IsNullOrWhiteSpace(nota.TribIssqn) ? "1" : nota.TribIssqn.Trim(),
                    ["tpRetISSQN"] = string.IsNullOrWhiteSpace(nota.TpRetIssqn) ? "1" : nota.TpRetIssqn.Trim(),
                    ["pAliq"] = Math.Round(nota.AliquotaIss, 2)
                }
            };

            // Não-optante SN: informar pTotTrib aproximado (Lei 12.741) — evita E0713
            if (opSimpNac == "1")
            {
                valores["pTotTrib"] = new JsonObject
                {
                    ["pTotTribFed"] = 13.45m,
                    ["pTotTribEst"] = 0m,
                    ["pTotTribMun"] = Math.Round(nota.AliquotaIss, 2)
                };
            }

            return valores;
        }

        private static JsonObject? MontarTomador(NotaServicoModel nota)
        {
            string doc = Digitos(nota.TomadorCpfCnpj);
            if (doc.Length is not (11 or 14))
                return null;

            var tom = new JsonObject
            {
                ["xNome"] = string.IsNullOrWhiteSpace(nota.TomadorNome)
                    ? "CONSUMIDOR"
                    : nota.TomadorNome.Trim()
            };
            if (doc.Length == 11) tom["cpf"] = doc;
            else tom["cnpj"] = doc;

            if (!string.IsNullOrWhiteSpace(nota.TomadorEmail))
                tom["email"] = nota.TomadorEmail.Trim();
            if (!string.IsNullOrWhiteSpace(nota.TomadorFone))
                tom["fone"] = Digitos(nota.TomadorFone);

            string cMun = Digitos(nota.TomadorIbge);
            if (cMun.Length == 7 && !string.IsNullOrWhiteSpace(nota.TomadorLgr))
            {
                tom["endereco"] = new JsonObject
                {
                    ["cMun"] = cMun,
                    ["uf"] = (nota.TomadorUf ?? "").Trim().ToUpperInvariant(),
                    ["cep"] = Digitos(nota.TomadorCep),
                    ["xLgr"] = nota.TomadorLgr.Trim(),
                    ["nro"] = string.IsNullOrWhiteSpace(nota.TomadorNro) ? "S/N" : nota.TomadorNro.Trim(),
                    ["xBairro"] = nota.TomadorBairro?.Trim() ?? ""
                };
            }

            return tom;
        }

        /// <summary>CRT empresa → opSimpNac DPS: 1=Não optante, 3=ME/EPP (Simples).</summary>
        private static string MapearOpSimpNac(string? crtEmpresa, string? overrideUi)
        {
            if (!string.IsNullOrWhiteSpace(overrideUi) && overrideUi is "1" or "2" or "3")
                return overrideUi;
            return (crtEmpresa ?? "1").Trim() switch
            {
                "1" or "2" => "3", // Simples / excesso → ME/EPP na DPS
                _ => "1"
            };
        }

        private static string Digitos(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : new string(Array.FindAll(s.ToCharArray(), char.IsDigit));
    }
}
