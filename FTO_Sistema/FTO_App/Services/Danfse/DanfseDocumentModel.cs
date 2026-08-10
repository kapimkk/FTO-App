namespace FTO_App.Services.Danfse
{
    /// <summary>Dados do DANFSe extraídos exclusivamente do XML autorizado (NT 008/2026).</summary>
    public sealed class DanfseDocumentModel
    {
        public string ChaveAcesso { get; init; } = "";
        public string NumeroNfse { get; init; } = "";
        public string Competencia { get; init; } = "";
        public string DhProcNfse { get; init; } = "";
        public string NumeroDps { get; init; } = "";
        public string SerieDps { get; init; } = "";
        public string DhEmiDps { get; init; } = "";
        public string EmitenteTipo { get; init; } = "";
        public string Situacao { get; init; } = "";
        public string CStat { get; init; } = "";
        public string Finalidade { get; init; } = "";
        public string TpAmb { get; init; } = "1";
        public string AmbGer { get; init; } = "";
        public string MunicipioEmitenteNome { get; init; } = "";
        public string XLocEmi { get; init; } = "";
        public string XLocPrestacao { get; init; } = "";

        public DanfsePessoaModel Prestador { get; init; } = new();
        public DanfsePessoaModel? Tomador { get; init; }
        public DanfsePessoaModel? Destinatario { get; init; }
        public DanfsePessoaModel? Intermediario { get; init; }
        public bool DestinatarioEhTomador { get; init; }

        public string CodTribNac { get; init; } = "";
        public string CodTribMun { get; init; } = "";
        public string DescTribNac { get; init; } = "";
        public string DescTribMun { get; init; } = "";
        public string CodNbs { get; init; } = "";
        public string DescNbs { get; init; } = "";
        public string CodLocPrestacao { get; init; } = "";
        public string DescServico { get; init; } = "";

        public string? TribIssqn { get; init; }
        public string? LocIncidenciaIss { get; init; }
        public string? RegEspTrib { get; init; }
        public string? BcIssqn { get; init; }
        public string? AliqIssqn { get; init; }
        public string? ValorIssqn { get; init; }
        public string? TpRetIssqn { get; init; }
        public bool SemIssqn { get; init; }

        public string? ValorPis { get; init; }
        public string? ValorCofins { get; init; }
        public string? ValorIrrf { get; init; }
        public string? TpRetPisCofins { get; init; }

        public bool TemIbsCbs { get; init; }
        public string? CstIbsCbs { get; init; }
        public string? ClassTrib { get; init; }
        public string? ValorIbs { get; init; }
        public string? ValorCbs { get; init; }

        public string ValorServico { get; init; } = "";
        public string ValorLiquido { get; init; } = "";
        public string? ValorTotalNfse { get; init; }

        public string? InfComplementares { get; init; }
        public string? PTotTribFed { get; init; }
        public string? PTotTribEst { get; init; }
        public string? PTotTribMun { get; init; }

        public string OpSimpNac { get; init; } = "";
        public string? RegApTribSN { get; init; }

        public bool Cancelada { get; init; }
        public bool Substituida { get; init; }

        public bool EhHomologacao => TpAmb == "2";
    }

    public sealed class DanfsePessoaModel
    {
        public string Documento { get; init; } = "";
        public string TipoDocumento { get; init; } = ""; // CNPJ/CPF/NIF
        public string Nome { get; init; } = "";
        public string Im { get; init; } = "";
        public string Telefone { get; init; } = "";
        public string Email { get; init; } = "";
        public string Logradouro { get; init; } = "";
        public string Numero { get; init; } = "";
        public string Complemento { get; init; } = "";
        public string Bairro { get; init; } = "";
        public string Municipio { get; init; } = "";
        public string Uf { get; init; } = "";
        public string Cep { get; init; } = "";
        public string CodIbge { get; init; } = "";
    }
}
