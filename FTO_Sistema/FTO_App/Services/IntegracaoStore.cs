using FTO_App.Models;
using System;
using System.Collections.Generic;
using Npgsql;
using System.Data;

namespace FTO_App.Services
{
    public static class IntegracaoStore
    {
        public static List<IntegracaoModel> Listar(string? filtro = null)
        {
            var list = new List<IntegracaoModel>();
            string where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(filtro))
                where += " AND (Nome LIKE @q OR Tipo LIKE @q OR BaseUrl LIKE @q)";

            using var conn = Database.GetConnection();
            using var cmd = Database.Cmd(conn, $"SELECT * FROM Integracoes {where} ORDER BY Nome");
            if (!string.IsNullOrWhiteSpace(filtro))
                cmd.Parameters.AddWithValue("@q", $"%{filtro.Trim()}%");

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new IntegracaoModel
                {
                    Id = Convert.ToInt64(Database.FieldOrDbNull(r, "Id")),
                    Nome = Database.FieldOrDbNull(r, "Nome")?.ToString() ?? "",
                    Tipo = Database.FieldOrDbNull(r, "Tipo")?.ToString() ?? "NFe",
                    BaseUrl = Database.FieldOrDbNull(r, "BaseUrl")?.ToString() ?? "",
                    ApiKey = SecretProtector.Unprotect(Database.FieldOrDbNull(r, "ApiKey")?.ToString()),
                    Observacao = Database.FieldOrDbNull(r, "Observacao")?.ToString() ?? "",
                    Ativo = Database.FieldOrDbNull(r, "Ativo") != DBNull.Value ? Convert.ToInt32(Database.FieldOrDbNull(r, "Ativo")) : 1
                });
            }
            return list;
        }

        public static IntegracaoModel? GetByTipo(string tipo)
        {
            using var conn = Database.GetConnection();
            using var cmd = Database.Cmd(conn, 
                "SELECT * FROM Integracoes WHERE Tipo=@t AND Ativo=1 ORDER BY Id DESC LIMIT 1");
            cmd.Parameters.AddWithValue("@t", tipo);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new IntegracaoModel
            {
                Id = Convert.ToInt64(Database.FieldOrDbNull(r, "Id")),
                Nome = Database.FieldOrDbNull(r, "Nome")?.ToString() ?? "",
                Tipo = Database.FieldOrDbNull(r, "Tipo")?.ToString() ?? "NFe",
                BaseUrl = Database.FieldOrDbNull(r, "BaseUrl")?.ToString() ?? "",
                ApiKey = SecretProtector.Unprotect(Database.FieldOrDbNull(r, "ApiKey")?.ToString()),
                Observacao = Database.FieldOrDbNull(r, "Observacao")?.ToString() ?? "",
                Ativo = Database.FieldOrDbNull(r, "Ativo") != DBNull.Value ? Convert.ToInt32(Database.FieldOrDbNull(r, "Ativo")) : 1
            };
        }

        public static void Save(IntegracaoModel m)
        {
            var p = new Dictionary<string, object>
            {
                ["@n"] = m.Nome.Trim(),
                ["@t"] = m.Tipo.Trim(),
                ["@u"] = m.BaseUrl.Trim(),
                ["@k"] = SecretProtector.Protect(m.ApiKey?.Trim()),
                ["@o"] = string.IsNullOrWhiteSpace(m.Observacao) ? (object)DBNull.Value : m.Observacao.Trim(),
                ["@a"] = m.Ativo
            };

            if (m.Id > 0)
            {
                p["@id"] = m.Id;
                Database.ExecuteNonQuery(
                    "UPDATE Integracoes SET Nome=@n, Tipo=@t, BaseUrl=@u, ApiKey=@k, Observacao=@o, Ativo=@a WHERE Id=@id", p);
            }
            else
            {
                Database.ExecuteNonQuery(
                    "INSERT INTO Integracoes (Nome, Tipo, BaseUrl, ApiKey, Observacao, Ativo) VALUES (@n,@t,@u,@k,@o,@a)", p);
            }
        }

        public static void Delete(long id) =>
            Database.ExecuteNonQuery("DELETE FROM Integracoes WHERE Id=@id",
                new Dictionary<string, object> { ["@id"] = id });
    }
}
