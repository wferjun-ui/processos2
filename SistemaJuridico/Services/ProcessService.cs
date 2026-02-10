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

        // ==============================
        // BUSCAR PROCESSO
        // ==============================

        public ProcessoModel? GetById(string id)
        {
            using var conn = _db.GetConnection();

            var p = conn.QueryFirstOrDefault(
                @"SELECT
                    id as Id,
                    numero as Numero,
                    is_antigo as IsAntigo,
                    paciente as Paciente,
                    juiz as Juiz,
                    genitor_rep_nome as GenitorNome,
                    genitor_rep_tipo as GenitorTipo,
                    classificacao as Classificacao,
                    status_fase as StatusFase,
                    ultima_atualizacao as UltimaAtualizacao,
                    cache_proximo_prazo as CacheProximoPrazo,
                    observacao_fixa as ObservacaoFixa
                  FROM processos
                  WHERE id = @id",
                new { id });

            return p;
        }

        // ==============================
        // CRIAR PROCESSO
        // ==============================

        public string CriarProcesso(ProcessoModel model)
        {
            using var conn = _db.GetConnection();

            var id = Guid.NewGuid().ToString();

            var (prazo, _) = ProcessLogic.CalculateDueDates(
                DateTime.Now.ToString("dd/MM/yyyy"));

            conn.Execute(@"
                INSERT INTO processos
                (id, numero, is_antigo, paciente, juiz,
                 genitor_rep_nome, genitor_rep_tipo,
                 classificacao, status_fase,
                 ultima_atualizacao, cache_proximo_prazo)
                VALUES
                (@Id,@Numero,@IsAntigo,@Paciente,@Juiz,
                 @GenitorNome,@GenitorTipo,
                 @Classificacao,'Conhecimento',
                 @Ultima,@Prazo)",
            new
            {
                Id = id,
                model.Numero,
                IsAntigo = model.IsAntigo ? 1 : 0,
                model.Paciente,
                model.Juiz,
                model.GenitorNome,
                model.GenitorTipo,
                model.Classificacao,
                Ultima = DateTime.Now.ToString("dd/MM/yyyy"),
                Prazo = prazo
            });

            return id;
        }

        // ==============================
        // ATUALIZAR PROCESSO
        // ==============================

        public void AtualizarProcesso(ProcessoModel model)
        {
            using var conn = _db.GetConnection();

            conn.Execute(@"
                UPDATE processos SET
                    numero=@Numero,
                    is_antigo=@IsAntigo,
                    paciente=@Paciente,
                    juiz=@Juiz,
                    genitor_rep_nome=@GenitorNome,
                    genitor_rep_tipo=@GenitorTipo,
                    classificacao=@Classificacao
                WHERE id=@Id",
            new
            {
                model.Id,
                model.Numero,
                IsAntigo = model.IsAntigo ? 1 : 0,
                model.Paciente,
                model.Juiz,
                model.GenitorNome,
                model.GenitorTipo,
                model.Classificacao
            });
        }

        // ==============================
        // ATUALIZAR FASE + PRAZO
        // ==============================

        public void AtualizarFaseEPrazo(
            string processoId,
            string fase,
            string observacao,
            string dataProxima)
        {
            using var conn = _db.GetConnection();

            var (prazo, _) = ProcessLogic.CalculateDueDates(null, dataProxima);

            conn.Execute(@"
                UPDATE processos SET
                    status_fase=@fase,
                    observacao_fixa=@obs,
                    cache_proximo_prazo=@prazo,
                    ultima_atualizacao=@ultima
                WHERE id=@id",
            new
            {
                id = processoId,
                fase,
                obs = observacao,
                prazo,
                ultima = DateTime.Now.ToString("dd/MM/yyyy")
            });
        }

        // ==============================
        // EXCLUIR PROCESSO
        // ==============================

        public void ExcluirProcesso(string id)
        {
            using var conn = _db.GetConnection();

            conn.Execute(
                "DELETE FROM processos WHERE id=@id",
                new { id });
        }
    }
}
