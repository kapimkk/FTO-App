using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace FTO_App
{
    public class Venda
    {
        public long Id { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Contato { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public decimal Gastos { get; set; }
        public decimal VendaValor { get; set; }
        public decimal Lucros => VendaValor - Gastos;
        public string TipoServico { get; set; } = string.Empty;
        public string FormaPag { get; set; } = string.Empty;
        public string Pago { get; set; } = string.Empty;
        public string CPF_CNPJ { get; set; } = string.Empty;

        public string DataFormatada => Data.ToString("dd/MM/yyyy");
        public string GastosFormatado => Gastos.ToString("C2");
        public string VendaFormatada => VendaValor.ToString("C2");
        public string LucrosFormatado => Lucros.ToString("C2");
    }

    public class Database
    {
        // Usa o diretório base da aplicação para garantir que o banco seja criado no lugar certo
        private static string DB_NAME = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FTO.db");
        
        // Adicionado Pooling=True para performance e evitar locks
        private static string ConnectionString => $"Data Source={DB_NAME};Version=3;Pooling=True;Cache Size=2000;Page Size=4096;";

        public static void InitTables()
        {
            // O SQLite cria o arquivo automaticamente no Open() se não existir.
            // Não usamos CreateFile manualmente para evitar corrupção de 0 bytes.

            using (var conn = GetConnection())
            {
                // Ativa modo WAL para performance e estabilidade
                using (var cmdPragma = new SQLiteCommand(conn))
                {
                    cmdPragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                    cmdPragma.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Users (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            User TEXT NOT NULL UNIQUE,
                            Senha TEXT NOT NULL
                        );
                        CREATE TABLE IF NOT EXISTS Clientes (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Nome TEXT NOT NULL,
                            Contato TEXT,
                            Cpf_Cnpj TEXT
                        );
                        CREATE TABLE IF NOT EXISTS Vendas (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Cliente TEXT,
                            Contato TEXT,
                            Data TEXT,
                            Gastos DECIMAL(10,2),
                            Venda DECIMAL(10,2),
                            TipoServico TEXT,
                            FormaPag TEXT,
                            Pago TEXT,
                            CPF_CNPJ TEXT
                        );
                        CREATE INDEX IF NOT EXISTS idx_vendas_data ON Vendas(Data);
                        CREATE INDEX IF NOT EXISTS idx_vendas_cliente ON Vendas(Cliente);
                        CREATE INDEX IF NOT EXISTS idx_clientes_nome ON Clientes(Nome);
                    ";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static SQLiteConnection GetConnection()
        {
            var conn = new SQLiteConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static void ExecuteNonQuery(string sql, Dictionary<string, object> parameters)
        {
            using (var conn = GetConnection())
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                if (parameters != null)
                {
                    foreach (var param in parameters)
                        cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
                cmd.ExecuteNonQuery();
            }
        }
    }
}