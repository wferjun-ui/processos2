using Dapper;
using SistemaJuridico.Models;

namespace SistemaJuridico.Services
{
    public class ProcessService
    {
        private readonly DatabaseService _db;

        public ProcessService(DatabaseService db)
        {
            _db = db;
        }

        public void SaveProcess(ProcessoModel model)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var trans = conn.BeginTransaction();

            var (prazo, _) = ProcessLogic.CalculateDueDates(DateTime.Now.ToString("dd/MM/yyyy"));

            conn.Execute(@"
                INSERT INTO processos 
                (id, numero, is_antigo, paciente, juiz, genitor_rep_nome, genitor_rep_tipo, classificacao, status_fase, ultima_atualizacao, cache_proximo_prazo)
                VALUES
                (@Id,@Numero,@IsAntigo,@Paciente,@Juiz,@GenitorNome,@GenitorTipo,@Classificacao,'Conhecimento',@UA,@Prazo)
            ", new
            {
                model.Id,
                model.Numero,
                IsAntigo = model.IsAntigo ? 1 : 0,
                model.Paciente,
                model.Juiz,
                model.GenitorNome,
                model.GenitorTipo,
                model.Classificacao,
                UA = DateTime.Now.ToString("dd/MM/yyyy"),
                Prazo = prazo
            }, trans);

            foreach (var r in model.Reus)
            {
                conn.Execute("INSERT INTO reus VALUES (@Id,@Pid,@Nome)", new
                {
                    Id = Guid.NewGuid().ToString(),
                    Pid = model.Id,
                    Nome = r
                }, trans);
            }

            foreach (var i in model.ItensSaude)
            {
                conn.Execute(@"
                    INSERT INTO itens_saude
                    VALUES (@Id,@Pid,@Tipo,@Nome,@Qtd,@Freq,@Local,@Data,0,0)
                ", new
                {
                    Id = Guid.NewGuid().ToString(),
                    Pid = model.Id,
                    i.Tipo,
                    i.Nome,
                    i.Qtd,
                    Freq = i.Frequencia,
                    i.Local,
                    Data = i.Data
                }, trans);
            }

            trans.Commit();
            _db.PerformBackup();
        }
    }
}
