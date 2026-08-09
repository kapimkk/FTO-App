using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Text;
using Npgsql;

namespace FTO_App.Services
{
    /// <summary>
    /// Copia dados do FTO.db (SQLite legado) para o PostgreSQL configurado no .env.
    /// Não apaga o SQLite — pode repetir com truncate opcional.
    /// </summary>
    public static class SqliteToPostgresMigrator
    {
        public sealed class Result
        {
            public bool Success { get; init; }
            public string Message { get; init; } = "";
            public Dictionary<string, int> Counts { get; init; } = new();
        }

        public static Result Migrate(string? sqlitePath = null, bool truncateFirst = true)
        {
            string path = sqlitePath ?? Database.SqliteLegacyPath;
            if (!File.Exists(path))
            {
                return new Result
                {
                    Success = false,
                    Message = $"Arquivo SQLite não encontrado:\n{path}\n\nColoque o FTO.db antigo na pasta do aplicativo e tente de novo."
                };
            }

            var counts = new Dictionary<string, int>();
            var log = new StringBuilder();

            try
            {
                Database.ReloadConnectionString();
                Database.InitTables();

                using var pg = Database.GetConnection();
                using var sqlite = new SQLiteConnection($"Data Source={path};Version=3;Read Only=True;");
                sqlite.Open();

                if (truncateFirst)
                {
                    using var trunc = pg.CreateCommand();
                    trunc.CommandText = @"
                        TRUNCATE TABLE notasfiscais, vendas, produtos, integracoes, clientes, users RESTART IDENTITY CASCADE;";
                    trunc.ExecuteNonQuery();
                    log.AppendLine("Tabelas PostgreSQL limpas (TRUNCATE).");
                }

                counts["users"] = CopyUsers(sqlite, pg);
                counts["clientes"] = CopyClientes(sqlite, pg);
                counts["produtos"] = CopyProdutos(sqlite, pg);
                counts["vendas"] = CopyVendas(sqlite, pg);
                counts["notasfiscais"] = CopyNotas(sqlite, pg);
                counts["integracoes"] = CopyIntegracoes(sqlite, pg);

                // Recalcula sequences
                ResetSequence(pg, "users");
                ResetSequence(pg, "clientes");
                ResetSequence(pg, "produtos");
                ResetSequence(pg, "vendas");
                ResetSequence(pg, "notasfiscais");
                ResetSequence(pg, "integracoes");

                // Backfill CPF nas vendas a partir dos clientes
                using (var cmd = pg.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE vendas v SET cpf_cnpj = c.cpf_cnpj
                        FROM clientes c
                        WHERE (v.cpf_cnpj IS NULL OR TRIM(v.cpf_cnpj) = '')
                          AND c.cpf_cnpj IS NOT NULL AND TRIM(c.cpf_cnpj) <> ''
                          AND LOWER(TRIM(c.nome)) = LOWER(TRIM(v.cliente));";
                    int n = cmd.ExecuteNonQuery();
                    log.AppendLine($"CPF/CNPJ preenchido em {n} venda(s) a partir de clientes.");
                }

                foreach (var kv in counts)
                    log.AppendLine($"{kv.Key}: {kv.Value} registro(s)");

                return new Result
                {
                    Success = true,
                    Message = "Migração concluída com sucesso.\n\n" + log,
                    Counts = counts
                };
            }
            catch (Exception ex)
            {
                return new Result
                {
                    Success = false,
                    Message = $"Falha na migração:\n{ex.Message}",
                    Counts = counts
                };
            }
        }

        private static void ResetSequence(NpgsqlConnection pg, string table)
        {
            using var cmd = pg.CreateCommand();
            cmd.CommandText = $@"
                SELECT setval(pg_get_serial_sequence('{table}', 'id'),
                    COALESCE((SELECT MAX(id) FROM {table}), 1), true);";
            cmd.ExecuteNonQuery();
        }

        private static int CopyUsers(SQLiteConnection sqlite, NpgsqlConnection pg)
        {
            int n = 0;
            using var r = new SQLiteCommand("SELECT Id, User, Senha FROM Users", sqlite).ExecuteReader();
            while (r.Read())
            {
                using var cmd = pg.CreateCommand();
                cmd.CommandText = "INSERT INTO users (id, username, senha) VALUES (@id,@u,@s) ON CONFLICT (id) DO NOTHING";
                cmd.Parameters.AddWithValue("@id", Convert.ToInt64(r["Id"]));
                cmd.Parameters.AddWithValue("@u", r["User"]?.ToString() ?? "");
                string senha = r["Senha"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(senha) && PasswordHasher.NeedsUpgrade(senha))
                    senha = PasswordHasher.Hash(senha);
                cmd.Parameters.AddWithValue("@s", senha);
                cmd.ExecuteNonQuery();
                n++;
            }
            return n;
        }

        private static int CopyClientes(SQLiteConnection sqlite, NpgsqlConnection pg)
        {
            int n = 0;
            using var r = new SQLiteCommand("SELECT * FROM Clientes", sqlite).ExecuteReader();
            while (r.Read())
            {
                using var cmd = pg.CreateCommand();
                cmd.CommandText = @"INSERT INTO clientes
                    (id, nome, contato, cpf_cnpj, tipopessoa, razaosocial, nomefantasia, email, ie, im, indicadorie,
                     cep, logradouro, numero, complemento, bairro, municipio, uf, codigoibge, pais, codigopais, ativo)
                    VALUES (@id,@nome,@co,@cpf,@tp,@rs,@nf,@em,@ie,@im,@ii,@cep,@lg,@nu,@cm,@ba,@mu,@uf,@ib,@pa,@cp,@at)
                    ON CONFLICT (id) DO NOTHING";
                cmd.Parameters.AddWithValue("@id", Convert.ToInt64(r["Id"]));
                cmd.Parameters.AddWithValue("@nome", Col(r, "Nome"));
                cmd.Parameters.AddWithValue("@co", DbNull(Col(r, "Contato")));
                cmd.Parameters.AddWithValue("@cpf", DbNull(Col(r, "Cpf_Cnpj")));
                cmd.Parameters.AddWithValue("@tp", Col(r, "TipoPessoa", "F"));
                cmd.Parameters.AddWithValue("@rs", DbNull(Col(r, "RazaoSocial")));
                cmd.Parameters.AddWithValue("@nf", DbNull(Col(r, "NomeFantasia")));
                cmd.Parameters.AddWithValue("@em", DbNull(Col(r, "Email")));
                cmd.Parameters.AddWithValue("@ie", DbNull(Col(r, "Ie")));
                cmd.Parameters.AddWithValue("@im", DbNull(Col(r, "Im")));
                cmd.Parameters.AddWithValue("@ii", Col(r, "IndicadorIe", "9"));
                cmd.Parameters.AddWithValue("@cep", DbNull(Col(r, "Cep")));
                cmd.Parameters.AddWithValue("@lg", DbNull(Col(r, "Logradouro")));
                cmd.Parameters.AddWithValue("@nu", DbNull(Col(r, "Numero")));
                cmd.Parameters.AddWithValue("@cm", DbNull(Col(r, "Complemento")));
                cmd.Parameters.AddWithValue("@ba", DbNull(Col(r, "Bairro")));
                cmd.Parameters.AddWithValue("@mu", DbNull(Col(r, "Municipio")));
                cmd.Parameters.AddWithValue("@uf", DbNull(Col(r, "Uf")));
                cmd.Parameters.AddWithValue("@ib", DbNull(Col(r, "CodigoIbge")));
                cmd.Parameters.AddWithValue("@pa", Col(r, "Pais", "Brasil"));
                cmd.Parameters.AddWithValue("@cp", Col(r, "CodigoPais", "1058"));
                cmd.Parameters.AddWithValue("@at", ColInt(r, "Ativo", 1));
                cmd.ExecuteNonQuery();
                n++;
            }
            return n;
        }

        private static int CopyProdutos(SQLiteConnection sqlite, NpgsqlConnection pg)
        {
            int n = 0;
            using var r = new SQLiteCommand("SELECT * FROM Produtos", sqlite).ExecuteReader();
            while (r.Read())
            {
                using var cmd = pg.CreateCommand();
                cmd.CommandText = @"INSERT INTO produtos
                    (id, codigobarras, nome, descricao, precovenda, custoproduto, quantidade, categoria, unidade, ativo, datacadastro)
                    VALUES (@id,@cb,@no,@de,@pv,@cu,@qt,@ca,@un,@at,@dc)
                    ON CONFLICT (id) DO NOTHING";
                cmd.Parameters.AddWithValue("@id", Convert.ToInt64(r["Id"]));
                cmd.Parameters.AddWithValue("@cb", DbNull(Col(r, "CodigoBarras")));
                cmd.Parameters.AddWithValue("@no", Col(r, "Nome"));
                cmd.Parameters.AddWithValue("@de", DbNull(Col(r, "Descricao")));
                cmd.Parameters.AddWithValue("@pv", ColDec(r, "PrecoVenda"));
                cmd.Parameters.AddWithValue("@cu", ColDec(r, "CustoProduto"));
                cmd.Parameters.AddWithValue("@qt", ColInt(r, "Quantidade", 0));
                cmd.Parameters.AddWithValue("@ca", DbNull(Col(r, "Categoria")));
                cmd.Parameters.AddWithValue("@un", Col(r, "Unidade", "UN"));
                cmd.Parameters.AddWithValue("@at", ColInt(r, "Ativo", 1));
                cmd.Parameters.AddWithValue("@dc", ParseDateOrNull(Col(r, "DataCadastro")) ?? (object)DBNull.Value);
                cmd.ExecuteNonQuery();
                n++;
            }
            return n;
        }

        private static int CopyVendas(SQLiteConnection sqlite, NpgsqlConnection pg)
        {
            int n = 0;
            using var r = new SQLiteCommand("SELECT * FROM Vendas", sqlite).ExecuteReader();
            while (r.Read())
            {
                using var cmd = pg.CreateCommand();
                cmd.CommandText = @"INSERT INTO vendas
                    (id, cliente, contato, data, gastos, venda, tiposervico, formapag, pago, cpf_cnpj, produtoid, quantidadeproduto, tipolancamento)
                    VALUES (@id,@cl,@co,@dt,@ga,@ve,@sv,@fp,@pg,@cpf,@pid,@pq,@tl)
                    ON CONFLICT (id) DO NOTHING";
                cmd.Parameters.AddWithValue("@id", Convert.ToInt64(r["Id"]));
                cmd.Parameters.AddWithValue("@cl", DbNull(Col(r, "Cliente")));
                cmd.Parameters.AddWithValue("@co", DbNull(Col(r, "Contato")));
                cmd.Parameters.AddWithValue("@dt", ParseDateOrNull(Col(r, "Data")) ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ga", ColDec(r, "Gastos"));
                cmd.Parameters.AddWithValue("@ve", ColDec(r, "Venda"));
                cmd.Parameters.AddWithValue("@sv", DbNull(Col(r, "TipoServico")));
                cmd.Parameters.AddWithValue("@fp", DbNull(Col(r, "FormaPag")));
                cmd.Parameters.AddWithValue("@pg", DbNull(Col(r, "Pago")));
                cmd.Parameters.AddWithValue("@cpf", DbNull(Col(r, "CPF_CNPJ")));
                object? pid = ColObj(r, "ProdutoId");
                cmd.Parameters.AddWithValue("@pid", pid == null || pid == DBNull.Value ? DBNull.Value : Convert.ToInt64(pid));
                cmd.Parameters.AddWithValue("@pq", ColInt(r, "QuantidadeProduto", 0));
                cmd.Parameters.AddWithValue("@tl", DbNull(Col(r, "TipoLancamento")));
                cmd.ExecuteNonQuery();
                n++;
            }
            return n;
        }

        private static int CopyNotas(SQLiteConnection sqlite, NpgsqlConnection pg)
        {
            int n = 0;
            try
            {
                using var r = new SQLiteCommand("SELECT * FROM NotasFiscais", sqlite).ExecuteReader();
                while (r.Read())
                {
                    using var cmd = pg.CreateCommand();
                    cmd.CommandText = @"INSERT INTO notasfiscais
                        (id, naturezaoperacao, modelo, serie, numero, dataemissao, tipooperacao, finalidade, consumidorfinal,
                         presencacomprador, ambiente, clienteid, destnome, destcpfcnpj, destie, destemail, destlogradouro,
                         destnumero, destcomplemento, destbairro, destmunicipio, destuf, destcep, destcodigoibge,
                         produtocodigo, produtodescricao, produtoncm, produtocfop, produtounidade, produtoquantidade,
                         produtovalorunitario, produtovalortotal, icmsorigem, icmscst, icmsaliquota, icmsvalor,
                         piscst, pisaliquota, pisvalor, cofinscst, cofinsaliquota, cofinsvalor,
                         valorprodutos, valorfrete, valordesconto, valortotalnota, formapagamento, informacoescomplementares, status, caminhoxml)
                        VALUES (
                         @id,@nat,@mod,@ser,@num,@dem,@top,@fin,@cf,@pres,@amb,@cid,@dn,@dd,@die,@demail,@dl,@dnr,@dcm,@dba,@dmu,@duf,@dce,@dib,
                         @pcod,@pd,@pn,@pf,@pu,@pq,@pvu,@pvt,@io,@ic,@ia,@iv,@psc,@psa,@psv,@csc,@csa,@csv,@vp,@vf,@vd,@vn,@fp,@inf,@st,@xml)
                        ON CONFLICT (id) DO NOTHING";
                    cmd.Parameters.AddWithValue("@id", Convert.ToInt64(r["Id"]));
                    AddStr(cmd, "@nat", r, "NaturezaOperacao");
                    AddStr(cmd, "@mod", r, "Modelo");
                    AddStr(cmd, "@ser", r, "Serie");
                    cmd.Parameters.AddWithValue("@num", ColLong(r, "Numero"));
                    AddStr(cmd, "@dem", r, "DataEmissao");
                    AddStr(cmd, "@top", r, "TipoOperacao");
                    AddStr(cmd, "@fin", r, "Finalidade");
                    AddStr(cmd, "@cf", r, "ConsumidorFinal");
                    AddStr(cmd, "@pres", r, "PresencaComprador");
                    AddStr(cmd, "@amb", r, "Ambiente");
                    object? cid = ColObj(r, "ClienteId");
                    cmd.Parameters.AddWithValue("@cid", cid == null || cid == DBNull.Value ? DBNull.Value : Convert.ToInt64(cid));
                    AddStr(cmd, "@dn", r, "DestNome");
                    AddStr(cmd, "@dd", r, "DestCpfCnpj");
                    AddStr(cmd, "@die", r, "DestIe");
                    AddStr(cmd, "@demail", r, "DestEmail");
                    AddStr(cmd, "@dl", r, "DestLogradouro");
                    AddStr(cmd, "@dnr", r, "DestNumero");
                    AddStr(cmd, "@dcm", r, "DestComplemento");
                    AddStr(cmd, "@dba", r, "DestBairro");
                    AddStr(cmd, "@dmu", r, "DestMunicipio");
                    AddStr(cmd, "@duf", r, "DestUf");
                    AddStr(cmd, "@dce", r, "DestCep");
                    AddStr(cmd, "@dib", r, "DestCodigoIbge");
                    AddStr(cmd, "@pcod", r, "ProdutoCodigo");
                    AddStr(cmd, "@pd", r, "ProdutoDescricao");
                    AddStr(cmd, "@pn", r, "ProdutoNcm");
                    AddStr(cmd, "@pf", r, "ProdutoCfop");
                    AddStr(cmd, "@pu", r, "ProdutoUnidade");
                    cmd.Parameters.AddWithValue("@pq", ColDec(r, "ProdutoQuantidade"));
                    cmd.Parameters.AddWithValue("@pvu", ColDec(r, "ProdutoValorUnitario"));
                    cmd.Parameters.AddWithValue("@pvt", ColDec(r, "ProdutoValorTotal"));
                    AddStr(cmd, "@io", r, "IcmsOrigem");
                    AddStr(cmd, "@ic", r, "IcmsCst");
                    cmd.Parameters.AddWithValue("@ia", ColDec(r, "IcmsAliquota"));
                    cmd.Parameters.AddWithValue("@iv", ColDec(r, "IcmsValor"));
                    AddStr(cmd, "@psc", r, "PisCst");
                    cmd.Parameters.AddWithValue("@psa", ColDec(r, "PisAliquota"));
                    cmd.Parameters.AddWithValue("@psv", ColDec(r, "PisValor"));
                    AddStr(cmd, "@csc", r, "CofinsCst");
                    cmd.Parameters.AddWithValue("@csa", ColDec(r, "CofinsAliquota"));
                    cmd.Parameters.AddWithValue("@csv", ColDec(r, "CofinsValor"));
                    cmd.Parameters.AddWithValue("@vp", ColDec(r, "ValorProdutos"));
                    cmd.Parameters.AddWithValue("@vf", ColDec(r, "ValorFrete"));
                    cmd.Parameters.AddWithValue("@vd", ColDec(r, "ValorDesconto"));
                    cmd.Parameters.AddWithValue("@vn", ColDec(r, "ValorTotalNota"));
                    AddStr(cmd, "@fp", r, "FormaPagamento");
                    AddStr(cmd, "@inf", r, "InformacoesComplementares");
                    AddStr(cmd, "@st", r, "Status");
                    AddStr(cmd, "@xml", r, "CaminhoXml");
                    cmd.ExecuteNonQuery();
                    n++;
                }
            }
            catch { /* tabela pode não existir no SQLite antigo */ }
            return n;
        }

        private static int CopyIntegracoes(SQLiteConnection sqlite, NpgsqlConnection pg)
        {
            int n = 0;
            try
            {
                using var r = new SQLiteCommand("SELECT * FROM Integracoes", sqlite).ExecuteReader();
                while (r.Read())
                {
                    using var cmd = pg.CreateCommand();
                    cmd.CommandText = @"INSERT INTO integracoes (id, nome, tipo, baseurl, apikey, observacao, ativo)
                        VALUES (@id,@n,@t,@u,@k,@o,@a) ON CONFLICT (id) DO NOTHING";
                    cmd.Parameters.AddWithValue("@id", Convert.ToInt64(r["Id"]));
                    cmd.Parameters.AddWithValue("@n", Col(r, "Nome"));
                    cmd.Parameters.AddWithValue("@t", Col(r, "Tipo", "NFe"));
                    cmd.Parameters.AddWithValue("@u", DbNull(Col(r, "BaseUrl")));
                    string apiKey = Col(r, "ApiKey");
                    cmd.Parameters.AddWithValue("@k", DbNull(
                        string.IsNullOrEmpty(apiKey) ? apiKey : SecretProtector.Protect(apiKey)));
                    cmd.Parameters.AddWithValue("@o", DbNull(Col(r, "Observacao")));
                    cmd.Parameters.AddWithValue("@a", ColInt(r, "Ativo", 1));
                    cmd.ExecuteNonQuery();
                    n++;
                }
            }
            catch { }
            return n;
        }

        private static void AddStr(NpgsqlCommand cmd, string p, SQLiteDataReader r, string col)
            => cmd.Parameters.AddWithValue(p, DbNull(Col(r, col)));

        private static string Col(SQLiteDataReader r, string col, string def = "")
        {
            try
            {
                var v = r[col];
                return v == null || v == DBNull.Value ? def : v.ToString() ?? def;
            }
            catch { return def; }
        }

        private static object? ColObj(SQLiteDataReader r, string col)
        {
            try { return r[col]; } catch { return null; }
        }

        private static int ColInt(SQLiteDataReader r, string col, int def)
        {
            try
            {
                var v = r[col];
                if (v == null || v == DBNull.Value) return def;
                return Convert.ToInt32(v);
            }
            catch { return def; }
        }

        private static long ColLong(SQLiteDataReader r, string col)
        {
            try
            {
                var v = r[col];
                if (v == null || v == DBNull.Value) return 0;
                return Convert.ToInt64(v);
            }
            catch { return 0; }
        }

        private static decimal ColDec(SQLiteDataReader r, string col)
        {
            try
            {
                var v = r[col];
                if (v == null || v == DBNull.Value) return 0;
                // Mesma regra do app: "160.00" = Invariant (não pt-BR, senão vira 16000).
                return MoneyInputHelper.ParseDb(v);
            }
            catch { return 0; }
        }

        private static object DbNull(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s;

        private static DateTime? ParseDateOrNull(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var d)) return d.Date;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2)) return d2.Date;
            if (DateTime.TryParseExact(s, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "yyyy-MM-dd HH:mm:ss" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d3)) return d3.Date;
            return null;
        }
    }
}
