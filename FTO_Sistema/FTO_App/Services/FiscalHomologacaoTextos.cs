namespace FTO_App.Services
{
    /// <summary>
    /// Textos fixos exigidos pela SEFAZ quando a nota é emitida em ambiente de homologação
    /// (tpAmb=2) — fonte única usada por <see cref="NfeXmlService"/> (XML local) e
    /// <see cref="FiscalPayloadBuilder"/> (JSON da API), evitando que as duas regras divirjam.
    ///
    /// Regras oficiais (MOC 7.0 / Rejeição 373 e 598): as palavras "homologação" devem ser
    /// escritas sem cedilha (ç) e sem til (~), e o texto deve ser exatamente igual ao exigido —
    /// qualquer variação (nome real do produto/cliente, sufixo extra, etc.) causa rejeição.
    /// </summary>
    public static class FiscalHomologacaoTextos
    {
        /// <summary>dest.xNome — Razão Social do destinatário em homologação (Rejeição 598).</summary>
        public const string XNomeDest = "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";

        /// <summary>det.prod.xProd — Descrição do primeiro item em homologação (Rejeição 373).</summary>
        public const string XProd = "NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";
    }
}
