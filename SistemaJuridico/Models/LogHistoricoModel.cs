namespace SistemaJuridico.Models
{
    public class LogHistoricoModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProcessoId { get; set; } = "";

        public string DataHora { get; set; } = "";
        public string Usuario { get; set; } = "";
        public string TipoEvento { get; set; } = "";

        public string Descricao { get; set; } = "";
        public string DadosExtrasJson { get; set; } = "";
    }
}
