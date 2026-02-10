using Dapper;
using SistemaJuridico.Models;

namespace SistemaJuridico.Services
{
    public class ContaService
    {
        private readonly DatabaseService _db;

        public ContaService(DatabaseService db)
        {
            _db = db;
        }

        public List<ContaModel> GetContas(string processoId)
        {
            using var conn = _db.GetConnection();

            return conn.Query<ContaModel>(
                @"SELECT 
                    id as Id,
                    processo_id as ProcessoId,
                    data_movimentacao as Data,
                    tipo_lancamento as Tipo,
                    historico as Historico,
                    valor_alvara as ValorAlvara,
                    valor_conta as ValorConta,
                    responsavel as Responsavel,
                    status_conta as Status
                  FROM contas 
                  WHERE processo_id = @id
                  ORDER BY data_movimentacao",
                new { id = processoId }).ToList();
        }

        public void SaveConta(ContaModel model)
        {
            using var conn = _db.GetConnection();

            conn.Execute(@"
                INSERT INTO contas
                (id, processo_id, data_movimentacao, tipo_lancamento,
                 historico, valor_alvara, valor_conta,
                 responsavel, status_conta)
                VALUES
                (@Id,@ProcessoId,@Data,@Tipo,
                 @Historico,@ValorAlvara,@ValorConta,
                 @Responsavel,@Status)
            ", model);
        }

        public void LancarTudo(string processoId)
        {
            using var conn = _db.GetConnection();

            conn.Execute(
                "UPDATE contas SET status_conta='lancado' WHERE processo_id=@id",
                new { id = processoId });
        }
    }
}
