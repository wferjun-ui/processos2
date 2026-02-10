namespace SistemaJuridico.Models
{
    public class VerificacaoModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProcessoId { get; set; } = "";
        public string StatusProcesso { get; set; } = "";
        public string Responsavel { get; set; } = "";

        public bool DiligenciaRealizada { get; set; }
        public string DiligenciaDescricao { get; set; } = "";

        public bool DiligenciaPendente { get; set; }
        public string PendenciaDescricao { get; set; } = "";

        public string ProximoPrazo { get; set; } = "";
        public string DataNotificacao { get; set; } = "";

        public string AlteracoesTexto { get; set; } = "";
        public string SnapshotItensJson { get; set; } = "";
    }
}
