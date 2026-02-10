namespace SistemaJuridico.Models
{
    public class ContaModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProcessoId { get; set; } = "";
        public string Data { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Historico { get; set; } = "";
        public decimal ValorAlvara { get; set; }
        public decimal ValorConta { get; set; }
        public string Responsavel { get; set; } = "";
        public string Status { get; set; } = "rascunho";
    }
}
