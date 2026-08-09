using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FTO_App.Models;
using Npgsql;

namespace FTO_App.Services
{
    /// <summary>
    /// Configuração da empresa e fiscal — persistida no PostgreSQL (não no .env).
    /// Campos sensíveis (CSC Token) ficam criptografados com DPAPI.
    /// </summary>
    public static class EmpresaConfigStore
    {
        private static readonly string EnvPath = Path.Combine(AppContext.BaseDirectory, ".env");

        public static EmpresaConfig Current { get; private set; } = new EmpresaConfig();

        public static string CaminhoEnvAtivo => EnvPath;

        public static void Load()
        {
            EnsureTable();
            Current = LoadFromDb() ?? new EmpresaConfig();

            // Migração única: se banco vazio, importa do .env antigo (EMPRESA_*/NFE_*)
            if (IsEmpty(Current) && File.Exists(EnvPath))
            {
                var fromEnv = TryParseLegacyEnv(EnvPath);
                if (fromEnv != null && !IsEmpty(fromEnv))
                {
                    Save(fromEnv);
                    Current = fromEnv;
                    EnvFileHelper.StripBusinessKeys(EnvPath);
                }
            }
        }

        public static void Save(EmpresaConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            EnsureTable();

            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO empresa_config (
                    id, nome, subtitulo, razaosocial, nomefantasia, endereco, numero, complemento, bairro,
                    cidade, uf, cep, codigoibge, telefone, email, cnpj, ie, im, cnae, regimetributario,
                    ambientenfe, serienfe, ultimonumeronfe, cscid, csctoken, certificatopath, logopath,
                    cupomtitulo, cupomrodape, atualizadoem
                ) VALUES (
                    1, @nome, @sub, @rs, @nf, @end, @num, @comp, @bai, @cid, @uf, @cep, @ibge, @tel, @em, @cnpj, @ie, @im, @cnae, @reg,
                    @amb, @ser, @ult, @cscid, @csctok, @cert, @logo, @ctit, @crod, NOW()
                )
                ON CONFLICT (id) DO UPDATE SET
                    nome = EXCLUDED.nome, subtitulo = EXCLUDED.subtitulo, razaosocial = EXCLUDED.razaosocial,
                    nomefantasia = EXCLUDED.nomefantasia, endereco = EXCLUDED.endereco, numero = EXCLUDED.numero,
                    complemento = EXCLUDED.complemento, bairro = EXCLUDED.bairro, cidade = EXCLUDED.cidade,
                    uf = EXCLUDED.uf, cep = EXCLUDED.cep, codigoibge = EXCLUDED.codigoibge, telefone = EXCLUDED.telefone,
                    email = EXCLUDED.email, cnpj = EXCLUDED.cnpj, ie = EXCLUDED.ie, im = EXCLUDED.im, cnae = EXCLUDED.cnae,
                    regimetributario = EXCLUDED.regimetributario, ambientenfe = EXCLUDED.ambientenfe,
                    serienfe = EXCLUDED.serienfe, ultimonumeronfe = EXCLUDED.ultimonumeronfe,
                    cscid = EXCLUDED.cscid, csctoken = EXCLUDED.csctoken, certificatopath = EXCLUDED.certificatopath,
                    logopath = EXCLUDED.logopath, cupomtitulo = EXCLUDED.cupomtitulo, cupomrodape = EXCLUDED.cupomrodape,
                    atualizadoem = NOW();";

            cmd.Parameters.AddWithValue("@nome", config.Nome ?? "");
            cmd.Parameters.AddWithValue("@sub", config.Subtitulo ?? "");
            cmd.Parameters.AddWithValue("@rs", config.RazaoSocial ?? "");
            cmd.Parameters.AddWithValue("@nf", config.NomeFantasia ?? "");
            cmd.Parameters.AddWithValue("@end", config.Endereco ?? "");
            cmd.Parameters.AddWithValue("@num", config.Numero ?? "");
            cmd.Parameters.AddWithValue("@comp", config.Complemento ?? "");
            cmd.Parameters.AddWithValue("@bai", config.Bairro ?? "");
            cmd.Parameters.AddWithValue("@cid", config.Cidade ?? "");
            cmd.Parameters.AddWithValue("@uf", config.Uf ?? "");
            cmd.Parameters.AddWithValue("@cep", config.Cep ?? "");
            cmd.Parameters.AddWithValue("@ibge", config.CodigoIbge ?? "");
            cmd.Parameters.AddWithValue("@tel", config.Telefone ?? "");
            cmd.Parameters.AddWithValue("@em", config.Email ?? "");
            cmd.Parameters.AddWithValue("@cnpj", config.Cnpj ?? "");
            cmd.Parameters.AddWithValue("@ie", config.Ie ?? "");
            cmd.Parameters.AddWithValue("@im", config.Im ?? "");
            cmd.Parameters.AddWithValue("@cnae", config.Cnae ?? "");
            cmd.Parameters.AddWithValue("@reg", config.RegimeTributario ?? "1");
            cmd.Parameters.AddWithValue("@amb", config.AmbienteNfe ?? "2");
            cmd.Parameters.AddWithValue("@ser", config.SerieNfe ?? "1");
            cmd.Parameters.AddWithValue("@ult", config.UltimoNumeroNfe ?? "0");
            cmd.Parameters.AddWithValue("@cscid", config.CscIdHomologacao ?? "");
            cmd.Parameters.AddWithValue("@csctok", SecretProtector.Protect(config.CscTokenHomologacao));
            cmd.Parameters.AddWithValue("@cert", config.CertificadoPath ?? "");
            cmd.Parameters.AddWithValue("@logo", config.LogoPath ?? "");
            cmd.Parameters.AddWithValue("@ctit", string.IsNullOrWhiteSpace(config.CupomTitulo) ? "Comprovante de Vendas" : config.CupomTitulo);
            cmd.Parameters.AddWithValue("@crod", config.CupomRodape ?? "");
            cmd.ExecuteNonQuery();

            using (var cmdR = conn.CreateCommand())
            {
                cmdR.CommandText = @"
                    UPDATE empresa_config SET
                        ibscbspreset=@preset, ibscbscalculoauto=@auto, ibscbsdestaque=@dest,
                        cbsaliquota=@cbs, ibsaliquota=@ibs, ibsaliquotauf=@ibsuf, ibsaliquotamun=@ibsmun
                    WHERE id=1;";
                cmdR.Parameters.AddWithValue("@preset", config.IbsCbsPreset ?? "teste2026");
                cmdR.Parameters.AddWithValue("@auto", config.IbsCbsCalculoAutomatico ? 1 : 0);
                cmdR.Parameters.AddWithValue("@dest", config.IbsCbsDestaqueObrigatorio ? 1 : 0);
                cmdR.Parameters.AddWithValue("@cbs", config.CbsAliquota);
                cmdR.Parameters.AddWithValue("@ibs", config.IbsAliquota);
                cmdR.Parameters.AddWithValue("@ibsuf", config.IbsAliquotaUf);
                cmdR.Parameters.AddWithValue("@ibsmun", config.IbsAliquotaMun);
                cmdR.ExecuteNonQuery();
            }

            using (var cmdF = conn.CreateCommand())
            {
                cmdF.CommandText = @"
                    UPDATE empresa_config SET
                        fiscalapiurlnfe=@urlnfe, fiscalapiurlnfce=@urlnfce, fiscalapikey=@key
                    WHERE id=1;";
                cmdF.Parameters.AddWithValue("@urlnfe", string.IsNullOrWhiteSpace(config.FiscalApiUrlNfe) ? "http://localhost:5001" : config.FiscalApiUrlNfe.Trim());
                cmdF.Parameters.AddWithValue("@urlnfce", string.IsNullOrWhiteSpace(config.FiscalApiUrlNfce) ? "http://localhost:5002" : config.FiscalApiUrlNfce.Trim());
                cmdF.Parameters.AddWithValue("@key", SecretProtector.Protect(config.FiscalApiKey));
                cmdF.ExecuteNonQuery();
            }

            using (var cmdCsc = conn.CreateCommand())
            {
                cmdCsc.CommandText = @"
                    UPDATE empresa_config SET
                        cscidproducao=@cscidp, csctokenproducao=@csctokp
                    WHERE id=1;";
                cmdCsc.Parameters.AddWithValue("@cscidp", config.CscIdProducao ?? "");
                cmdCsc.Parameters.AddWithValue("@csctokp", SecretProtector.Protect(config.CscTokenProducao));
                cmdCsc.ExecuteNonQuery();
            }

            Current = Clone(config);
        }

        private static void EnsureTable()
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS empresa_config (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    nome TEXT DEFAULT '',
                    subtitulo TEXT DEFAULT '',
                    razaosocial TEXT DEFAULT '',
                    nomefantasia TEXT DEFAULT '',
                    endereco TEXT DEFAULT '',
                    numero TEXT DEFAULT '',
                    complemento TEXT DEFAULT '',
                    bairro TEXT DEFAULT '',
                    cidade TEXT DEFAULT '',
                    uf TEXT DEFAULT '',
                    cep TEXT DEFAULT '',
                    codigoibge TEXT DEFAULT '',
                    telefone TEXT DEFAULT '',
                    email TEXT DEFAULT '',
                    cnpj TEXT DEFAULT '',
                    ie TEXT DEFAULT '',
                    im TEXT DEFAULT '',
                    cnae TEXT DEFAULT '',
                    regimetributario TEXT DEFAULT '1',
                    ambientenfe TEXT DEFAULT '2',
                    serienfe TEXT DEFAULT '1',
                    ultimonumeronfe TEXT DEFAULT '0',
                    cscid TEXT DEFAULT '',
                    csctoken TEXT DEFAULT '',
                    certificatopath TEXT DEFAULT '',
                    logopath TEXT DEFAULT '',
                    cupomtitulo TEXT DEFAULT 'CUPOM NAO FISCAL',
                    cupomrodape TEXT DEFAULT '',
                    atualizadoem TIMESTAMPTZ DEFAULT NOW()
                );";
            cmd.ExecuteNonQuery();
        }

        private static EmpresaConfig? LoadFromDb()
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM empresa_config WHERE id = 1";
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new EmpresaConfig
            {
                Nome = Str(r, "nome"),
                Subtitulo = Str(r, "subtitulo"),
                RazaoSocial = Str(r, "razaosocial"),
                NomeFantasia = Str(r, "nomefantasia"),
                Endereco = Str(r, "endereco"),
                Numero = Str(r, "numero"),
                Complemento = Str(r, "complemento"),
                Bairro = Str(r, "bairro"),
                Cidade = Str(r, "cidade"),
                Uf = Str(r, "uf"),
                Cep = Str(r, "cep"),
                CodigoIbge = Str(r, "codigoibge"),
                Telefone = Str(r, "telefone"),
                Email = Str(r, "email"),
                Cnpj = Str(r, "cnpj"),
                Ie = Str(r, "ie"),
                Im = Str(r, "im"),
                Cnae = Str(r, "cnae"),
                RegimeTributario = Str(r, "regimetributario", "1"),
                AmbienteNfe = Str(r, "ambientenfe", "2"),
                SerieNfe = Str(r, "serienfe", "1"),
                UltimoNumeroNfe = Str(r, "ultimonumeronfe", "0"),
                CscIdHomologacao = Str(r, "cscid"),
                CscTokenHomologacao = SecretProtector.Unprotect(Str(r, "csctoken")),
                CscIdProducao = Str(r, "cscidproducao"),
                CscTokenProducao = SecretProtector.Unprotect(Str(r, "csctokenproducao")),
                CertificadoPath = Str(r, "certificatopath"),
                LogoPath = Str(r, "logopath"),
                CupomTitulo = Str(r, "cupomtitulo", "Comprovante de Vendas"),
                CupomRodape = Str(r, "cupomrodape"),
                IbsCbsPreset = Str(r, "ibscbspreset", "teste2026"),
                IbsCbsCalculoAutomatico = DbInt(r, "ibscbscalculoauto", 1) == 1,
                IbsCbsDestaqueObrigatorio = DbInt(r, "ibscbsdestaque", 1) == 1,
                CbsAliquota = DbDec(r, "cbsaliquota", 0.9m),
                IbsAliquota = DbDec(r, "ibsaliquota", 0.1m),
                IbsAliquotaUf = DbDec(r, "ibsaliquotauf", 0.1m),
                IbsAliquotaMun = DbDec(r, "ibsaliquotamun", 0m),
                FiscalApiUrlNfe = Str(r, "fiscalapiurlnfe", "http://localhost:5001"),
                FiscalApiUrlNfce = Str(r, "fiscalapiurlnfce", "http://localhost:5002"),
                FiscalApiKey = SecretProtector.Unprotect(Str(r, "fiscalapikey"))
            };
        }

        private static int DbInt(NpgsqlDataReader r, string col, int def)
        {
            try
            {
                object? v = Database.Field(r, col);
                if (v == null || v == DBNull.Value) return def;
                return Convert.ToInt32(v);
            }
            catch { return def; }
        }

        private static decimal DbDec(NpgsqlDataReader r, string col, decimal def)
        {
            try
            {
                object? v = Database.Field(r, col);
                if (v == null || v == DBNull.Value) return def;
                return Convert.ToDecimal(v);
            }
            catch { return def; }
        }

        private static string Str(NpgsqlDataReader r, string col, string def = "")
        {
            try
            {
                object? v = Database.Field(r, col);
                return v == null || v == DBNull.Value ? def : v.ToString() ?? def;
            }
            catch { return def; }
        }

        private static bool IsEmpty(EmpresaConfig c) =>
            string.IsNullOrWhiteSpace(c.Nome) &&
            string.IsNullOrWhiteSpace(c.Cnpj) &&
            string.IsNullOrWhiteSpace(c.RazaoSocial);

        private static EmpresaConfig Clone(EmpresaConfig c) => new()
        {
            Nome = c.Nome,
            Subtitulo = c.Subtitulo,
            RazaoSocial = c.RazaoSocial,
            NomeFantasia = c.NomeFantasia,
            Endereco = c.Endereco,
            Numero = c.Numero,
            Complemento = c.Complemento,
            Bairro = c.Bairro,
            Cidade = c.Cidade,
            Uf = c.Uf,
            Cep = c.Cep,
            CodigoIbge = c.CodigoIbge,
            Telefone = c.Telefone,
            Email = c.Email,
            Cnpj = c.Cnpj,
            Ie = c.Ie,
            Im = c.Im,
            Cnae = c.Cnae,
            RegimeTributario = c.RegimeTributario,
            AmbienteNfe = c.AmbienteNfe,
            SerieNfe = c.SerieNfe,
            UltimoNumeroNfe = c.UltimoNumeroNfe,
            CscIdHomologacao = c.CscIdHomologacao,
            CscTokenHomologacao = c.CscTokenHomologacao,
            CscIdProducao = c.CscIdProducao,
            CscTokenProducao = c.CscTokenProducao,
            CertificadoPath = c.CertificadoPath,
            LogoPath = c.LogoPath,
            CupomTitulo = c.CupomTitulo,
            CupomRodape = c.CupomRodape,
            IbsCbsPreset = c.IbsCbsPreset,
            IbsCbsCalculoAutomatico = c.IbsCbsCalculoAutomatico,
            IbsCbsDestaqueObrigatorio = c.IbsCbsDestaqueObrigatorio,
            CbsAliquota = c.CbsAliquota,
            IbsAliquota = c.IbsAliquota,
            IbsAliquotaUf = c.IbsAliquotaUf,
            IbsAliquotaMun = c.IbsAliquotaMun,
            FiscalApiUrlNfe = c.FiscalApiUrlNfe,
            FiscalApiUrlNfce = c.FiscalApiUrlNfce,
            FiscalApiKey = c.FiscalApiKey
        };

        private static EmpresaConfig? TryParseLegacyEnv(string path)
        {
            try
            {
                var map = EnvFileHelper.ReadMap(path);
                if (map.Count == 0) return null;
                if (!map.ContainsKey("EMPRESA_NOME") && !map.ContainsKey("EMPRESA_CNPJ"))
                    return null;

                string cidadeRaw = map.GetValueOrDefault("EMPRESA_CIDADE", "");
                string cidade = cidadeRaw;
                string uf = map.GetValueOrDefault("EMPRESA_UF", "");
                string cep = map.GetValueOrDefault("EMPRESA_CEP", "");

                // Ex.: "Curitiba/PR - CEP 81070-000"
                if (cidadeRaw.Contains('/') && string.IsNullOrWhiteSpace(uf))
                {
                    var parts = cidadeRaw.Split('/', 2);
                    cidade = parts[0].Trim();
                    string rest = parts.Length > 1 ? parts[1] : "";
                    if (rest.Length >= 2) uf = rest[..2].Trim();
                    int cepIdx = rest.IndexOf("CEP", StringComparison.OrdinalIgnoreCase);
                    if (cepIdx >= 0 && string.IsNullOrWhiteSpace(cep))
                        cep = rest[(cepIdx + 3)..].Trim().Trim('-', ' ');
                }

                return new EmpresaConfig
                {
                    Nome = map.GetValueOrDefault("EMPRESA_NOME", ""),
                    Subtitulo = map.GetValueOrDefault("EMPRESA_SUBTITULO", ""),
                    RazaoSocial = map.GetValueOrDefault("EMPRESA_RAZAO_SOCIAL", map.GetValueOrDefault("EMPRESA_NOME", "")),
                    NomeFantasia = map.GetValueOrDefault("EMPRESA_NOME_FANTASIA", map.GetValueOrDefault("EMPRESA_SUBTITULO", "")),
                    Endereco = map.GetValueOrDefault("EMPRESA_ENDERECO", ""),
                    Numero = map.GetValueOrDefault("EMPRESA_NUMERO", ""),
                    Complemento = map.GetValueOrDefault("EMPRESA_COMPLEMENTO", ""),
                    Bairro = map.GetValueOrDefault("EMPRESA_BAIRRO", ""),
                    Cidade = cidade,
                    Uf = uf,
                    Cep = cep,
                    CodigoIbge = map.GetValueOrDefault("EMPRESA_CODIGO_IBGE", ""),
                    Telefone = map.GetValueOrDefault("EMPRESA_TELEFONE", ""),
                    Email = map.GetValueOrDefault("EMPRESA_EMAIL", ""),
                    Cnpj = map.GetValueOrDefault("EMPRESA_CNPJ", ""),
                    Ie = map.GetValueOrDefault("EMPRESA_IE", ""),
                    Im = map.GetValueOrDefault("EMPRESA_IM", ""),
                    Cnae = map.GetValueOrDefault("EMPRESA_CNAE", ""),
                    RegimeTributario = map.GetValueOrDefault("EMPRESA_REGIME_TRIBUTARIO", "1"),
                    AmbienteNfe = map.GetValueOrDefault("NFE_AMBIENTE", "2"),
                    SerieNfe = map.GetValueOrDefault("NFE_SERIE", "1"),
                    UltimoNumeroNfe = map.GetValueOrDefault("NFE_ULTIMO_NUMERO", "0"),
                    CscIdHomologacao = map.GetValueOrDefault("NFE_CSC_ID", ""),
                    CscTokenHomologacao = map.GetValueOrDefault("NFE_CSC_TOKEN", ""),
                    CertificadoPath = map.GetValueOrDefault("NFE_CERTIFICADO_PATH", ""),
                    LogoPath = map.GetValueOrDefault("EMPRESA_LOGO_PATH", ""),
                    CupomTitulo = map.GetValueOrDefault("CUPOM_TITULO", "Comprovante de Vendas"),
                    CupomRodape = map.GetValueOrDefault("CUPOM_RODAPE", "")
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
