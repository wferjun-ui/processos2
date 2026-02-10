namespace SistemaJuridico.Models
{
    public class ProcessoModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Numero { get; set; } = "";
        public bool IsAntigo { get; set; }
        public string Paciente { get; set; } = "";
        public string Juiz { get; set; } = "";
        public string GenitorNome { get; set; } = "";
        public string GenitorTipo { get; set; } = "";
        public string Classificacao { get; set; } = "Cível";

        public List<string> Reus { get; set; } = new();
        public List<ItemSaudeModel> ItensSaude { get; set; } = new();
    }

    public class ItemSaudeModel
    {
        public string Tipo { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Qtd { get; set; } = "";
        public string Frequencia { get; set; } = "";
        public string Local { get; set; } = "";
        public string Data { get; set; } = "";
    }
}
