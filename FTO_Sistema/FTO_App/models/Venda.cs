using System;

namespace FTO_App.Models
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
}