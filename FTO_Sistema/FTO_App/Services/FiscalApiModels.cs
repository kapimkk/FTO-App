using System.Collections.Generic;

namespace FTO_App.Services
{
    /// <summary>Protocolo de autorização devolvido pela SEFAZ (grupo protNFe/infProt).</summary>
    public class FiscalProtocoloDto
    {
        public string? NProt { get; set; }
        public string? DigVal { get; set; }
        public string? DhRecbto { get; set; }
        public string? CStat { get; set; }
        public string? XMotivo { get; set; }
        public string? ChNFe { get; set; }
    }

    /// <summary>Auditoria pós-emissão via consulta de protocolo (NFeConsultaProtocolo4).</summary>
    public class FiscalValidacaoConsultaSefazDto
    {
        public bool ConfirmadoNaSefaz { get; set; }
        public string? CStat { get; set; }
        public string? XMotivo { get; set; }
        public string? NProt { get; set; }
        public string? EndpointUrl { get; set; }
    }

    /// <summary>Item de problema de validação local (codigo + mensagem).</summary>
    public class FiscalValidationErrorDto
    {
        public string? Codigo { get; set; }
        public string? Mensagem { get; set; }
    }

    /// <summary>Resposta única de emissão de NF-e/NFC-e (EmissaoNotaResponse da API).</summary>
    public class FiscalEmissaoNotaResponse
    {
        public bool Aprovado { get; set; }
        public string? ChaveAcesso { get; set; }
        public string? NProt { get; set; }
        public string? DhRecbto { get; set; }
        public string? CStat { get; set; }
        public string? XMotivo { get; set; }
        public string? MensagemTraduzida { get; set; }
        public string? CStatLote { get; set; }
        public string? QrCodeUrl { get; set; }
        public string? XmlAssinado { get; set; }
        public string? XmlAutorizado { get; set; }
        public string? CaminhoXmlNotas { get; set; }
        public FiscalProtocoloDto? Protocolo { get; set; }
        public FiscalValidacaoConsultaSefazDto? ValidacaoConsultaSefaz { get; set; }
        public bool TransmitidoSefaz { get; set; }
        public string? Erro { get; set; }
        public List<FiscalValidationErrorDto>? Problemas { get; set; }
    }

    /// <summary>Resposta de eventos (cancelamento/CC-e) — EventoNotaResponse da API.</summary>
    public class FiscalEventoNotaResponse
    {
        public bool Aprovado { get; set; }
        public string? ChaveAcesso { get; set; }
        public string? NProt { get; set; }
        public string? DhRegEvento { get; set; }
        public string? NSeqEvento { get; set; }
        public string? CStat { get; set; }
        public string? XMotivo { get; set; }
        public string? MensagemTraduzida { get; set; }
        public string? CStatLote { get; set; }
        public string? XmlEnviado { get; set; }
        public string? Erro { get; set; }
        public List<FiscalValidationErrorDto>? Problemas { get; set; }
    }

    /// <summary>Resposta da inutilização de faixa de numeração — InutilizacaoResponse da API.</summary>
    public class FiscalInutilizacaoResponse
    {
        public bool Aprovado { get; set; }
        public string? NProt { get; set; }
        public string? CStat { get; set; }
        public string? XMotivo { get; set; }
        public string? MensagemTraduzida { get; set; }
        public string? Faixa { get; set; }
        public string? XmlEnviado { get; set; }
    }

    /// <summary>Um evento (cancelamento, CC-e etc.) confirmado pela SEFAZ para uma chave.</summary>
    public class FiscalEventoRegistradoDto
    {
        public string? TpEvento { get; set; }
        public string? XEvento { get; set; }
        public string? CStat { get; set; }
        public string? XMotivo { get; set; }
        public string? NProt { get; set; }
        public string? NSeqEvento { get; set; }
        public string? DhRegEvento { get; set; }
    }

    /// <summary>Status normalizado de uma nota — NotaStatusResponse da API. Situacao chega como int (enum sem conversor JSON no servidor).</summary>
    public class FiscalNotaStatusResponse
    {
        public string ChaveAcesso { get; set; } = string.Empty;
        public string? TpAmb { get; set; }
        public string? CStat { get; set; }
        public string? XMotivo { get; set; }
        public string? NProt { get; set; }
        public string? DataAutorizacao { get; set; }
        public int Situacao { get; set; }
        public bool ConfirmadoNaSefaz { get; set; }
        public List<FiscalEventoRegistradoDto>? EventosRegistrados { get; set; }

        public string SituacaoDescricao => Situacao switch
        {
            0 => "Autorizada",
            1 => "Cancelada",
            2 => "Denegada",
            3 => "Rejeitada",
            4 => "Inexistente",
            _ => "Indeterminada"
        };
    }

    /// <summary>
    /// Resultado padronizado de qualquer chamada à API Fiscal: nunca lança exceção para a camada de UI —
    /// sempre devolve sucesso/falha com código e mensagem concretos (HTTP, código do erro e texto).
    /// </summary>
    public class FiscalApiResult<T> where T : class
    {
        public bool Sucesso { get; init; }
        public T? Dados { get; init; }
        public int? HttpStatus { get; init; }
        public string? CodigoErro { get; init; }
        public string? Mensagem { get; init; }
        public string? DetalhesTecnicos { get; init; }

        public static FiscalApiResult<T> Ok(T dados, int httpStatus) =>
            new() { Sucesso = true, Dados = dados, HttpStatus = httpStatus };

        public static FiscalApiResult<T> Falha(int? httpStatus, string codigo, string mensagem, string? detalhes = null) =>
            new() { Sucesso = false, HttpStatus = httpStatus, CodigoErro = codigo, Mensagem = mensagem, DetalhesTecnicos = detalhes };

        /// <summary>Texto pronto para exibir ao usuário (MessageBox), com HTTP + código + mensagem.</summary>
        public string ResumoErro()
        {
            string http = HttpStatus.HasValue ? $"HTTP {HttpStatus}" : "Sem resposta";
            return $"[{http}] {CodigoErro}: {Mensagem}";
        }
    }
}
