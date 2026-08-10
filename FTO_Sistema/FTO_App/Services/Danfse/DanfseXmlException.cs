using System;
using System.Collections.Generic;

namespace FTO_App.Services.Danfse
{
    /// <summary>XML da NFS-e incompleto ou inválido para gerar DANFSe (NT 008).</summary>
    public sealed class DanfseXmlException : Exception
    {
        public IReadOnlyList<string> CamposFaltantes { get; }

        public DanfseXmlException(string message, IReadOnlyList<string>? camposFaltantes = null)
            : base(message)
        {
            CamposFaltantes = camposFaltantes ?? Array.Empty<string>();
        }
    }
}
