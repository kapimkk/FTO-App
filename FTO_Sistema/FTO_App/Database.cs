using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using Npgsql;

namespace FTO_App
{
    /// <summary>
    /// Acesso ao PostgreSQL. Connection string via .env (PG* ou DATABASE_URL).
    /// </summary>
    public static class Database
    {
        private static string? _connectionString;

        public static string ConnectionString
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_connectionString))
                    return _connectionString;

                _connectionString = BuildConnectionString();
                return _connectionString;
            }
        }

        public static string SqliteLegacyPath
        {
            get
            {
                string? path = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(path)) path = Directory.GetCurrentDirectory();
                return Path.Combine(path ?? ".", "FTO.db");
            }
        }

        private static string BuildConnectionString()
        {
            string envPath = Path.Combine(AppContext.BaseDirectory, ".env");
            var map = ReadEnvMap(envPath);

            if (map.TryGetValue("DATABASE_URL", out string? url) && !string.IsNullOrWhiteSpace(url))
                return url.Trim();

            string host = map.GetValueOrDefault("PGHOST", "localhost");
            string port = map.GetValueOrDefault("PGPORT", "5432");
            string db = map.GetValueOrDefault("PGDATABASE", "fto");
            string user = map.GetValueOrDefault("PGUSER", "postgres");
            string pass = Services.SecretProtector.Unprotect(map.GetValueOrDefault("PGPASSWORD", ""));

            if (string.IsNullOrWhiteSpace(pass) &&
                string.IsNullOrWhiteSpace(map.GetValueOrDefault("DATABASE_URL", "")))
            {
                throw new InvalidOperationException(
                    "PostgreSQL não configurado.\n\n" +
                    "No pgAdmin, crie o banco \"fto\" e preencha no arquivo .env:\n\n" +
                    "PGHOST=localhost\nPGPORT=5432\nPGDATABASE=fto\nPGUSER=postgres\nPGPASSWORD=sua_senha\n\n" +
                    $"Arquivo esperado: {envPath}");
            }

            // Criptografa PGPASSWORD no .env sem apagar EMPRESA_* (migração vem depois)
            string rawPass = map.GetValueOrDefault("PGPASSWORD", "");
            if (!string.IsNullOrEmpty(rawPass) && !Services.SecretProtector.IsProtected(rawPass) && File.Exists(envPath))
            {
                try { Services.EnvFileHelper.ProtectPasswordInPlace(envPath); }
                catch { /* não bloqueia startup */ }
            }

            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = int.TryParse(port, out int p) ? p : 5432,
                Database = db,
                Username = user,
                Password = pass,
                Pooling = true,
                PersistSecurityInfo = false
            };
            return csb.ConnectionString;
        }

        private static Dictionary<string, string> ReadEnvMap(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return map;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                int idx = line.IndexOf('=');
                if (idx <= 0) continue;
                map[line[..idx].Trim()] = line[(idx + 1)..].Trim().Trim('"');
            }
            return map;
        }

        public static void ReloadConnectionString() => _connectionString = null;

        public static void InitTables()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS users (
                    id SERIAL PRIMARY KEY,
                    username TEXT NOT NULL UNIQUE,
                    senha TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS clientes (
                    id SERIAL PRIMARY KEY,
                    nome TEXT NOT NULL,
                    contato TEXT,
                    cpf_cnpj TEXT,
                    tipopessoa TEXT DEFAULT 'F',
                    razaosocial TEXT,
                    nomefantasia TEXT,
                    email TEXT,
                    ie TEXT,
                    im TEXT,
                    indicadorie TEXT DEFAULT '9',
                    cep TEXT,
                    logradouro TEXT,
                    numero TEXT,
                    complemento TEXT,
                    bairro TEXT,
                    municipio TEXT,
                    uf TEXT,
                    codigoibge TEXT,
                    pais TEXT DEFAULT 'Brasil',
                    codigopais TEXT DEFAULT '1058',
                    ativo INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS vendas (
                    id SERIAL PRIMARY KEY,
                    cliente TEXT,
                    contato TEXT,
                    data DATE,
                    gastos NUMERIC(14,2),
                    venda NUMERIC(14,2),
                    tiposervico TEXT,
                    formapag TEXT,
                    pago TEXT,
                    cpf_cnpj TEXT,
                    produtoid INTEGER,
                    quantidadeproduto INTEGER DEFAULT 0,
                    tipolancamento TEXT
                );
                CREATE TABLE IF NOT EXISTS produtos (
                    id SERIAL PRIMARY KEY,
                    codigobarras TEXT,
                    nome TEXT NOT NULL,
                    descricao TEXT,
                    precovenda NUMERIC(14,2) DEFAULT 0,
                    custoproduto NUMERIC(14,2) DEFAULT 0,
                    quantidade INTEGER DEFAULT 0,
                    categoria TEXT,
                    unidade TEXT DEFAULT 'UN',
                    ativo INTEGER DEFAULT 1,
                    datacadastro DATE,
                    ncm TEXT DEFAULT '',
                    cest TEXT DEFAULT '',
                    cfop TEXT DEFAULT '',
                    origem TEXT DEFAULT '0',
                    csosn TEXT DEFAULT '',
                    csticms TEXT DEFAULT '',
                    icmsaliquota NUMERIC(8,4) DEFAULT 0,
                    piscst TEXT DEFAULT '',
                    pisaliquota NUMERIC(8,4) DEFAULT 0,
                    cofinscst TEXT DEFAULT '',
                    cofinsaliquota NUMERIC(8,4) DEFAULT 0,
                    codigobeneficio TEXT DEFAULT '',
                    infadicionais TEXT DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS notasfiscais (
                    id SERIAL PRIMARY KEY,
                    naturezaoperacao TEXT,
                    modelo TEXT,
                    serie TEXT,
                    numero BIGINT,
                    dataemissao TEXT,
                    tipooperacao TEXT,
                    finalidade TEXT,
                    consumidorfinal TEXT,
                    presencacomprador TEXT,
                    ambiente TEXT,
                    clienteid INTEGER,
                    destnome TEXT,
                    destcpfcnpj TEXT,
                    destie TEXT,
                    destemail TEXT,
                    destlogradouro TEXT,
                    destnumero TEXT,
                    destcomplemento TEXT,
                    destbairro TEXT,
                    destmunicipio TEXT,
                    destuf TEXT,
                    destcep TEXT,
                    destcodigoibge TEXT,
                    produtocodigo TEXT,
                    produtodescricao TEXT,
                    produtoncm TEXT,
                    produtocfop TEXT,
                    produtounidade TEXT,
                    produtoquantidade DOUBLE PRECISION,
                    produtovalorunitario DOUBLE PRECISION,
                    produtovalortotal DOUBLE PRECISION,
                    icmsorigem TEXT,
                    icmscst TEXT,
                    icmsaliquota DOUBLE PRECISION,
                    icmsvalor DOUBLE PRECISION,
                    piscst TEXT,
                    pisaliquota DOUBLE PRECISION,
                    pisvalor DOUBLE PRECISION,
                    cofinscst TEXT,
                    cofinsaliquota DOUBLE PRECISION,
                    cofinsvalor DOUBLE PRECISION,
                    valorprodutos DOUBLE PRECISION,
                    valorfrete DOUBLE PRECISION,
                    valordesconto DOUBLE PRECISION,
                    valortotalnota DOUBLE PRECISION,
                    formapagamento TEXT,
                    informacoescomplementares TEXT,
                    status TEXT,
                    caminhoxml TEXT
                );
                CREATE TABLE IF NOT EXISTS integracoes (
                    id SERIAL PRIMARY KEY,
                    nome TEXT NOT NULL,
                    tipo TEXT NOT NULL,
                    baseurl TEXT,
                    apikey TEXT,
                    observacao TEXT,
                    ativo INTEGER DEFAULT 1
                );
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
                );
                CREATE INDEX IF NOT EXISTS idx_vendas_data ON vendas(data);
                CREATE INDEX IF NOT EXISTS idx_vendas_cliente ON vendas(cliente);
                CREATE INDEX IF NOT EXISTS idx_clientes_nome ON clientes(nome);
                CREATE INDEX IF NOT EXISTS idx_produtos_barcode ON produtos(codigobarras);
                CREATE INDEX IF NOT EXISTS idx_produtos_nome ON produtos(nome);
                CREATE INDEX IF NOT EXISTS idx_nfe_numero ON notasfiscais(numero);
            ";
            cmd.ExecuteNonQuery();

            EnsureProdutoFiscalColumns(conn);
            EnsureEmpresaReformaColumns(conn);
            EnsureNotaReformaColumns(conn);
            EnsureEmpresaFiscalApiColumns(conn);
            EnsureNotaFiscalApiColumns(conn);
            EnsureNotasServicoTable(conn);
            NormalizeClientesTipoPessoa(conn);
            BackfillVendasCpf(conn);
        }

        /// <summary>Configuração de conexão com a API Fiscal PFCode (URLs + API Key + CSC de Produção, todos criptografados com DPAPI quando sensíveis). O CSC de Homologação usa as colunas legadas cscid/csctoken.</summary>
        private static void EnsureEmpresaFiscalApiColumns(NpgsqlConnection conn)
        {
            string[] cols =
            {
                "fiscalapiurlnfe TEXT DEFAULT 'http://localhost:5001'",
                "fiscalapiurlnfce TEXT DEFAULT 'http://localhost:5002'",
                "fiscalapiurlnfse TEXT DEFAULT 'http://localhost:5003'",
                "serienfse TEXT DEFAULT '1'",
                "ultimonumeronfse TEXT DEFAULT '0'",
                "fiscalapikey TEXT DEFAULT ''",
                "cscidproducao TEXT DEFAULT ''",
                "csctokenproducao TEXT DEFAULT ''"
            };
            foreach (string def in cols)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE empresa_config ADD COLUMN IF NOT EXISTS {def}";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Tabela de rascunhos/emissões NFS-e (DPS — Padrão Nacional / SEFIN).</summary>
        private static void EnsureNotasServicoTable(NpgsqlConnection conn)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS notasservico (
                        id SERIAL PRIMARY KEY,
                        ambiente TEXT DEFAULT '2',
                        serie TEXT DEFAULT '1',
                        numerodps BIGINT DEFAULT 1,
                        datacompetencia TEXT DEFAULT '',
                        codigoibgeemissao TEXT DEFAULT '',
                        tomadornome TEXT DEFAULT '',
                        tomadorcpfcnpj TEXT DEFAULT '',
                        tomadoremail TEXT DEFAULT '',
                        tomadorfone TEXT DEFAULT '',
                        tomadorlgr TEXT DEFAULT '',
                        tomadornro TEXT DEFAULT '',
                        tomadorbairro TEXT DEFAULT '',
                        tomadormun TEXT DEFAULT '',
                        tomadoruf TEXT DEFAULT '',
                        tomadorcep TEXT DEFAULT '',
                        tomadoribge TEXT DEFAULT '',
                        codtribnac TEXT DEFAULT '010101',
                        codtribmun TEXT DEFAULT '',
                        descricaoservico TEXT DEFAULT '',
                        codnbs TEXT DEFAULT '',
                        codibgeprestacao TEXT DEFAULT '',
                        valorservico NUMERIC(14,2) DEFAULT 0,
                        tribissqn TEXT DEFAULT '1',
                        tpretissqn TEXT DEFAULT '1',
                        aliquotaiss NUMERIC(8,4) DEFAULT 5,
                        opsimpnac TEXT DEFAULT '1',
                        regaptribsn TEXT DEFAULT '1',
                        regesptrib TEXT DEFAULT '0',
                        incluiribscbs INTEGER DEFAULT 0,
                        cstibscbs TEXT DEFAULT '000',
                        classtrib TEXT DEFAULT '000001',
                        codindop TEXT DEFAULT '000001',
                        status TEXT DEFAULT 'Rascunho',
                        chaveacesso TEXT DEFAULT '',
                        iddps TEXT DEFAULT '',
                        dataprocessamento TEXT DEFAULT '',
                        cstat TEXT DEFAULT '',
                        xmotivo TEXT DEFAULT '',
                        xmlenviado TEXT DEFAULT '',
                        xmlautorizado TEXT DEFAULT '',
                        datacadastro TIMESTAMPTZ DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS idx_nfse_numero ON notasservico(numerodps);";
                cmd.ExecuteNonQuery();
            }

            // CREATE IF NOT EXISTS não adiciona colunas em bases já criadas — espelha notasfiscais
            string[] cols =
            {
                "ambiente TEXT DEFAULT '2'",
                "serie TEXT DEFAULT '1'",
                "numerodps BIGINT DEFAULT 1",
                "datacompetencia TEXT DEFAULT ''",
                "codigoibgeemissao TEXT DEFAULT ''",
                "tomadornome TEXT DEFAULT ''",
                "tomadorcpfcnpj TEXT DEFAULT ''",
                "tomadoremail TEXT DEFAULT ''",
                "tomadorfone TEXT DEFAULT ''",
                "tomadorlgr TEXT DEFAULT ''",
                "tomadornro TEXT DEFAULT ''",
                "tomadorbairro TEXT DEFAULT ''",
                "tomadormun TEXT DEFAULT ''",
                "tomadoruf TEXT DEFAULT ''",
                "tomadorcep TEXT DEFAULT ''",
                "tomadoribge TEXT DEFAULT ''",
                "codtribnac TEXT DEFAULT '010101'",
                "codtribmun TEXT DEFAULT ''",
                "descricaoservico TEXT DEFAULT ''",
                "codnbs TEXT DEFAULT ''",
                "codibgeprestacao TEXT DEFAULT ''",
                "valorservico NUMERIC(14,2) DEFAULT 0",
                "tribissqn TEXT DEFAULT '1'",
                "tpretissqn TEXT DEFAULT '1'",
                "aliquotaiss NUMERIC(8,4) DEFAULT 5",
                "opsimpnac TEXT DEFAULT '1'",
                "regaptribsn TEXT DEFAULT '1'",
                "regesptrib TEXT DEFAULT '0'",
                "incluiribscbs INTEGER DEFAULT 0",
                "cstibscbs TEXT DEFAULT '000'",
                "classtrib TEXT DEFAULT '000001'",
                "codindop TEXT DEFAULT '000001'",
                "status TEXT DEFAULT 'Rascunho'",
                "chaveacesso TEXT DEFAULT ''",
                "iddps TEXT DEFAULT ''",
                "dataprocessamento TEXT DEFAULT ''",
                "cstat TEXT DEFAULT ''",
                "xmotivo TEXT DEFAULT ''",
                "xmlenviado TEXT DEFAULT ''",
                "xmlautorizado TEXT DEFAULT ''",
                "datacadastro TIMESTAMPTZ DEFAULT NOW()"
            };
            foreach (string def in cols)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE notasservico ADD COLUMN IF NOT EXISTS {def}";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE UNIQUE INDEX IF NOT EXISTS ux_nfse_serie_numero
                    ON notasservico (serie, numerodps);";
                try { cmd.ExecuteNonQuery(); }
                catch (Exception ex)
                {
                    // Índice único falha se já houver duplicatas — não derruba o app
                    System.Diagnostics.Debug.WriteLine($"ux_nfse_serie_numero: {ex.Message}");
                }
            }
        }

        /// <summary>Resultado da última emissão/consulta na API Fiscal, gravado na própria nota.</summary>
        private static void EnsureNotaFiscalApiColumns(NpgsqlConnection conn)
        {
            string[] cols =
            {
                "chaveacesso TEXT DEFAULT ''",
                "nprot TEXT DEFAULT ''",
                "dhrecbto TEXT DEFAULT ''",
                "cstat TEXT DEFAULT ''",
                "xmotivo TEXT DEFAULT ''",
                "mensagemtraduzida TEXT DEFAULT ''",
                "qrcodeurl TEXT DEFAULT ''",
                "xmlautorizado TEXT DEFAULT ''"
            };
            foreach (string def in cols)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE notasfiscais ADD COLUMN IF NOT EXISTS {def}";
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureEmpresaReformaColumns(NpgsqlConnection conn)
        {
            string[] cols =
            {
                "ibscbspreset TEXT DEFAULT 'teste2026'",
                "ibscbscalculoauto INTEGER DEFAULT 1",
                "ibscbsdestaque INTEGER DEFAULT 1",
                "cbsaliquota NUMERIC(8,4) DEFAULT 0.9",
                "ibsaliquota NUMERIC(8,4) DEFAULT 0.1",
                "ibsaliquotauf NUMERIC(8,4) DEFAULT 0.1",
                "ibsaliquotamun NUMERIC(8,4) DEFAULT 0"
            };
            foreach (string def in cols)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE empresa_config ADD COLUMN IF NOT EXISTS {def}";
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureNotaReformaColumns(NpgsqlConnection conn)
        {
            string[] cols =
            {
                "cstibscbs TEXT DEFAULT '000'",
                "classtrib TEXT DEFAULT '000001'",
                "cbsaliquota DOUBLE PRECISION DEFAULT 0",
                "cbsvalor DOUBLE PRECISION DEFAULT 0",
                "ibsaliquota DOUBLE PRECISION DEFAULT 0",
                "ibsvalor DOUBLE PRECISION DEFAULT 0",
                "ibsaliquotauf DOUBLE PRECISION DEFAULT 0",
                "ibsvaloruf DOUBLE PRECISION DEFAULT 0",
                "ibsaliquotamun DOUBLE PRECISION DEFAULT 0",
                "ibsvalormun DOUBLE PRECISION DEFAULT 0",
                "iddest TEXT DEFAULT '1'",
                "indiedest TEXT DEFAULT '9'",
                "csosn TEXT DEFAULT '102'",
                "produtocest TEXT DEFAULT ''",
                "produtogtin TEXT DEFAULT 'SEM GTIN'"
            };
            foreach (string def in cols)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE notasfiscais ADD COLUMN IF NOT EXISTS {def}";
                cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureProdutoFiscalColumns(NpgsqlConnection conn)
        {
            string[] cols =
            {
                "ncm TEXT DEFAULT ''",
                "cest TEXT DEFAULT ''",
                "cfop TEXT DEFAULT ''",
                "origem TEXT DEFAULT '0'",
                "csosn TEXT DEFAULT ''",
                "csticms TEXT DEFAULT ''",
                "icmsaliquota NUMERIC(8,4) DEFAULT 0",
                "piscst TEXT DEFAULT ''",
                "pisaliquota NUMERIC(8,4) DEFAULT 0",
                "cofinscst TEXT DEFAULT ''",
                "cofinsaliquota NUMERIC(8,4) DEFAULT 0",
                "codigobeneficio TEXT DEFAULT ''",
                "infadicionais TEXT DEFAULT ''",
                "cstibscbs TEXT DEFAULT '000'",
                "classtrib TEXT DEFAULT '000001'",
                "cbsaliquota NUMERIC(8,4) DEFAULT 0",
                "ibsaliquota NUMERIC(8,4) DEFAULT 0",
                "ibscbsreducao NUMERIC(8,4) DEFAULT 0"
            };
            foreach (string def in cols)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE produtos ADD COLUMN IF NOT EXISTS {def}";
                cmd.ExecuteNonQuery();
            }
        }

        private static void NormalizeClientesTipoPessoa(NpgsqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE clientes SET tipopessoa = 'J'
                WHERE length(regexp_replace(COALESCE(cpf_cnpj,''), '[^0-9]', '', 'g')) = 14;
                UPDATE clientes SET tipopessoa = 'F'
                WHERE length(regexp_replace(COALESCE(cpf_cnpj,''), '[^0-9]', '', 'g')) = 11;
            ";
            cmd.ExecuteNonQuery();
        }

        private static void BackfillVendasCpf(NpgsqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE vendas v
                SET cpf_cnpj = c.cpf_cnpj
                FROM clientes c
                WHERE (v.cpf_cnpj IS NULL OR TRIM(v.cpf_cnpj) = '')
                  AND c.cpf_cnpj IS NOT NULL AND TRIM(c.cpf_cnpj) <> ''
                  AND LOWER(TRIM(c.nome)) = LOWER(TRIM(v.cliente));
            ";
            cmd.ExecuteNonQuery();
        }

        public static NpgsqlConnection GetConnection()
        {
            var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static void ExecuteNonQuery(string sql, Dictionary<string, object>? parameters)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(NormalizeSql(sql), conn);
            AddParams(cmd, parameters);
            cmd.ExecuteNonQuery();
        }

        public static long ExecuteInsertReturnId(string sql, Dictionary<string, object>? parameters)
        {
            string normalized = NormalizeSql(sql);
            if (!normalized.Contains("RETURNING", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.TrimEnd().TrimEnd(';') + " RETURNING id";

            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(normalized, conn);
            AddParams(cmd, parameters);
            object? id = cmd.ExecuteScalar();
            return Convert.ToInt64(id);
        }

        private static void AddParams(NpgsqlCommand cmd, Dictionary<string, object>? parameters)
        {
            if (parameters == null) return;
            foreach (var p in parameters)
                cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        }

        /// <summary>
        /// Converte SQL legado (SQLite / PascalCase) para PostgreSQL lowercase.
        /// </summary>
        public static string NormalizeSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return sql;

            string s = sql;
            s = s.Replace("IFNULL(", "COALESCE(", StringComparison.OrdinalIgnoreCase);
            s = s.Replace("last_insert_rowid()", "lastval()", StringComparison.OrdinalIgnoreCase);

            // Tabelas
            s = ReplaceWord(s, "Users", "users");
            s = ReplaceWord(s, "Clientes", "clientes");
            s = ReplaceWord(s, "Vendas", "vendas");
            s = ReplaceWord(s, "Produtos", "produtos");
            s = ReplaceWord(s, "NotasFiscais", "notasfiscais");
            s = ReplaceWord(s, "NotasServico", "notasservico");
            s = ReplaceWord(s, "Integracoes", "integracoes");

            // Colunas Users
            s = ReplaceWord(s, "Senha", "senha");
            // User column → username (evitar palavra reservada)
            s = System.Text.RegularExpressions.Regex.Replace(
                s, @"\bUser\b", "username", System.Text.RegularExpressions.RegexOptions.None);

            // Colunas comuns (ordem: nomes longos primeiro)
            string[] cols =
            {
                "QuantidadeProduto","TipoLancamento","ProdutoId","CodigoBarras","PrecoVenda","CustoProduto",
                "DataCadastro","TipoPessoa","RazaoSocial","NomeFantasia","IndicadorIe","CodigoIbge","CodigoPais",
                "TipoServico","FormaPag","CPF_CNPJ","Cpf_Cnpj","NaturezaOperacao","DataEmissao","TipoOperacao",
                "ConsumidorFinal","PresencaComprador","ClienteId","DestNome","DestCpfCnpj","DestIe","DestEmail",
                "DestLogradouro","DestNumero","DestComplemento","DestBairro","DestMunicipio","DestUf","DestCep",
                "DestCodigoIbge","ProdutoCodigo","ProdutoDescricao","ProdutoNcm","ProdutoCfop","ProdutoUnidade",
                "ProdutoQuantidade","ProdutoValorUnitario","ProdutoValorTotal","IcmsOrigem","IcmsCst","IcmsAliquota",
                "IcmsValor","PisCst","PisAliquota","PisValor","CofinsCst","CofinsAliquota","CofinsValor",
                "ValorProdutos","ValorFrete","ValorDesconto","ValorTotalNota","FormaPagamento",
                "InformacoesComplementares","CaminhoXml","BaseUrl","ApiKey","Observacao",
                "CodigoBeneficio","InfAdicionais","CstIcms","Csosn","Cest","Cfop","Ncm","Origem",
                "CstIbsCbs","ClassTrib","CbsAliquota","CbsValor","IbsAliquota","IbsValor",
                "IbsAliquotaUf","IbsValorUf","IbsAliquotaMun","IbsValorMun","IbsCbsReducao",
                "Contato","Cliente","Gastos","Venda","Pago","Nome","Email","Ativo","Descricao","Categoria",
                "Unidade","Quantidade","Numero","Serie","Modelo","Status","Ambiente","Finalidade",
                "Logradouro","Complemento","Bairro","Municipio","Cep","Pais","Ie","Im","Data","Id"
            };
            foreach (string c in cols)
                s = ReplaceWord(s, c, c.ToLowerInvariant());

            // COLLATE NOCASE → removido (usar ILIKE nas queries de login)
            s = System.Text.RegularExpressions.Regex.Replace(
                s, @"\s+COLLATE\s+NOCASE", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return s;
        }

        private static string ReplaceWord(string input, string from, string to)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                input,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(from)}\b",
                to);
        }

        public static NpgsqlCommand Cmd(NpgsqlConnection conn, string sql)
            => new NpgsqlCommand(NormalizeSql(sql), conn);

        public static object? Field(IDataRecord r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
            {
                if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                    return r.IsDBNull(i) ? null : r.GetValue(i);
            }
            return null;
        }

        public static string FieldStr(IDataRecord r, string name, string def = "")
        {
            object? v = Field(r, name);
            return v == null || v == DBNull.Value ? def : v.ToString() ?? def;
        }

        /// <summary>Compatível com código legado: trata null/DBNull como vazio.</summary>
        public static object FieldOrDbNull(IDataRecord r, string name)
            => Field(r, name) ?? DBNull.Value;
    }
}
