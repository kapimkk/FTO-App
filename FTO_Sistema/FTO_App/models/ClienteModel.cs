namespace FTO_App.Models
{
    public class ClienteModel
    {
        public long Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Contato { get; set; } = string.Empty;
        public string CpfCnpj { get; set; } = string.Empty;
    }
}