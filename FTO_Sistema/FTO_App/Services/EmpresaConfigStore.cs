using System;
using System.Collections.Generic;
using System.IO;
using FTO_App.Models;

namespace FTO_App.Services
{
    /// <summary>
    /// Carrega configurações da empresa a partir de .env na pasta do executável (FTO_App).
    /// </summary>
    public static class EmpresaConfigStore
    {
        private static readonly string EnvPath = Path.Combine(AppContext.BaseDirectory, ".env");
        private static readonly string EnvExamplePath = Path.Combine(AppContext.BaseDirectory, ".env.example");

        public static EmpresaConfig Current { get; private set; } = new EmpresaConfig();

        public static string CaminhoEnvAtivo => EnvPath;

        public static void Load()
        {
            if (!File.Exists(EnvPath) && File.Exists(EnvExamplePath))
                File.Copy(EnvExamplePath, EnvPath, overwrite: false);

            if (!File.Exists(EnvPath))
                throw new FileNotFoundException(
                    "Arquivo .env não encontrado na pasta do aplicativo.\n\n" +
                    $"Esperado em: {EnvPath}\n\n" +
                    "Copie .env.example para .env e preencha os dados da empresa.");

            Current = ParseEnv(EnvPath);
        }

        private static EmpresaConfig ParseEnv(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                int idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;

                string key = line[..idx].Trim();
                string value = line[(idx + 1)..].Trim().Trim('"');
                map[key] = value;
            }

            return new EmpresaConfig
            {
                Nome = map.GetValueOrDefault("EMPRESA_NOME", ""),
                Subtitulo = map.GetValueOrDefault("EMPRESA_SUBTITULO", ""),
                Endereco = map.GetValueOrDefault("EMPRESA_ENDERECO", ""),
                Cidade = map.GetValueOrDefault("EMPRESA_CIDADE", ""),
                Telefone = map.GetValueOrDefault("EMPRESA_TELEFONE", ""),
                Cnpj = map.GetValueOrDefault("EMPRESA_CNPJ", ""),
                Ie = map.GetValueOrDefault("EMPRESA_IE", ""),
                CupomTitulo = map.GetValueOrDefault("CUPOM_TITULO", "CUPOM NAO FISCAL"),
                CupomRodape = map.GetValueOrDefault("CUPOM_RODAPE", "")
            };
        }
    }
}
