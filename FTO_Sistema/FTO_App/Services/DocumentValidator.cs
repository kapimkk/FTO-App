using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FTO_App.Services
{
    public static class DocumentValidator
    {
        public static string OnlyDigits(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "" : Regex.Replace(value, @"\D", "");

        /// <summary>Retorna "F" (CPF) ou "J" (CNPJ) conforme quantidade de dígitos; null se incompleto.</summary>
        public static string? DetectTipoPessoa(string? doc)
        {
            string d = OnlyDigits(doc);
            if (d.Length == 11) return "F";
            if (d.Length == 14) return "J";
            return null;
        }

        public static string Format(string? doc)
        {
            string d = OnlyDigits(doc);
            if (d.Length == 11)
                return $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}";
            if (d.Length == 14)
                return $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}";
            return doc?.Trim() ?? "";
        }

        public static bool TryValidate(string? doc, out string tipoPessoa, out string formatado, out string erro)
        {
            tipoPessoa = "F";
            formatado = "";
            erro = "";

            string d = OnlyDigits(doc);
            if (d.Length == 0)
            {
                erro = "Informe o CPF ou CNPJ.";
                return false;
            }

            if (d.Length == 11)
            {
                tipoPessoa = "F";
                if (!IsValidCpf(d))
                {
                    erro = "CPF inválido.";
                    return false;
                }
                formatado = Format(d);
                return true;
            }

            if (d.Length == 14)
            {
                tipoPessoa = "J";
                if (!IsValidCnpj(d))
                {
                    erro = "CNPJ inválido.";
                    return false;
                }
                formatado = Format(d);
                return true;
            }

            erro = "Documento deve ter 11 (CPF) ou 14 (CNPJ) dígitos.";
            return false;
        }

        public static bool IsValidCpf(string cpf)
        {
            cpf = OnlyDigits(cpf);
            if (cpf.Length != 11 || cpf.Distinct().Count() == 1) return false;

            int[] m1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] m2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

            string temp = cpf[..9];
            int sum = 0;
            for (int i = 0; i < 9; i++) sum += (temp[i] - '0') * m1[i];
            int r = sum % 11;
            char d1 = (char)((r < 2 ? 0 : 11 - r) + '0');
            if (cpf[9] != d1) return false;

            temp += d1;
            sum = 0;
            for (int i = 0; i < 10; i++) sum += (temp[i] - '0') * m2[i];
            r = sum % 11;
            char d2 = (char)((r < 2 ? 0 : 11 - r) + '0');
            return cpf[10] == d2;
        }

        public static bool IsValidCnpj(string cnpj)
        {
            cnpj = OnlyDigits(cnpj);
            if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1) return false;

            int[] m1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] m2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

            string temp = cnpj[..12];
            int sum = 0;
            for (int i = 0; i < 12; i++) sum += (temp[i] - '0') * m1[i];
            int r = sum % 11;
            char d1 = (char)((r < 2 ? 0 : 11 - r) + '0');
            if (cnpj[12] != d1) return false;

            temp += d1;
            sum = 0;
            for (int i = 0; i < 13; i++) sum += (temp[i] - '0') * m2[i];
            r = sum % 11;
            char d2 = (char)((r < 2 ? 0 : 11 - r) + '0');
            return cnpj[13] == d2;
        }
    }
}
