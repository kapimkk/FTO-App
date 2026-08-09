using System;

namespace FTO_App.Models
{
    public class ClienteModel
    {
        public long Id { get; set; }
        public string TipoPessoa { get; set; } = "F"; // F=Física, J=Jurídica
        public string Nome { get; set; } = string.Empty;
        public string RazaoSocial { get; set; } = string.Empty;
        public string NomeFantasia { get; set; } = string.Empty;
        public string Contato { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CpfCnpj { get; set; } = string.Empty;
        public string Ie { get; set; } = string.Empty;
        public string Im { get; set; } = string.Empty;
        public string IndicadorIe { get; set; } = "9"; // 1=Contribuinte, 2=Isento, 9=Não contribuinte
        public string Cep { get; set; } = string.Empty;
        public string Logradouro { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Complemento { get; set; } = string.Empty;
        public string Bairro { get; set; } = string.Empty;
        public string Municipio { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
        public string CodigoIbge { get; set; } = string.Empty;
        public string Pais { get; set; } = "Brasil";
        public string CodigoPais { get; set; } = "1058";
        public int Ativo { get; set; } = 1;

        public string TipoPessoaExibicao
        {
            get
            {
                string? detectado = Services.DocumentValidator.DetectTipoPessoa(CpfCnpj);
                if (detectado == "J") return "Jurídica";
                if (detectado == "F") return "Física";
                return TipoPessoa == "J" ? "Jurídica" : "Física";
            }
        }
        public string StatusAtivo => Ativo == 1 ? "Ativo" : "Inativo";
        public string EnderecoCompleto =>
            $"{Logradouro}, {Numero} - {Bairro} - {Municipio}/{Uf}".Trim(' ', ',', '-');
    }
}
