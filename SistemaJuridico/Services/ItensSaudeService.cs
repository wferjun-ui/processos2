using Dapper;
using SistemaJuridico.Models;

namespace SistemaJuridico.Services
{
    public class ItensSaudeService
    {
        private readonly DatabaseService _db;

        public ItensSaudeService(DatabaseService db)
        {
            _db = db;
        }

        // ======================
        // LISTAR ITENS DO PROCESSO
        // ======================

        public List<ItemSaudeModel> GetItens(string processoId)
        {
            using var conn = _db.GetConnection();

            return conn.Query<ItemSaudeModel>(
                @"SELECT
                    tipo as Tipo,
                    nome as Nome,
                    qtd as Qtd,
                    frequencia as Frequencia,
                    local as Local,
                    data as Data
                  FROM itens_saude
                  WHERE processo_id = @id
                  ORDER BY tipo, nome",
                new { id = processoId }).ToList();
        }

        // ======================
        // SALVAR ITENS
        // ======================

        public void SalvarItens(string processoId, List<ItemSaudeModel> itens)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            using var trans = conn.BeginTransaction();

            // remove antigos
            conn.Execute(
                "DELETE FROM itens_saude WHERE processo_id=@id",
                new { id = processoId },
                trans);

            // insere novos
            foreach (var i in itens)
            {
                conn.Execute(@"
                    INSERT INTO itens_saude
                    (id, processo_id, tipo, nome, qtd,
                     frequencia, local, data, cumprido, removido)
                    VALUES
                    (@Id,@Pid,@Tipo,@Nome,@Qtd,
                     @Freq,@Local,@Data,0,0)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    Pid = processoId,
                    i.Tipo,
                    i.Nome,
                    i.Qtd,
                    Freq = i.Frequencia,
                    i.Local,
                    i.Data
                }, trans);
            }

            trans.Commit();
        }

        // ======================
        // ADICIONAR ITEM INDIVIDUAL
        // ======================

        public void AddItem(string processoId, ItemSaudeModel item)
        {
            using var conn = _db.GetConnection();

            conn.Execute(@"
                INSERT INTO itens_saude
                (id, processo_id, tipo, nome, qtd,
                 frequencia, local, data, cumprido, removido)
                VALUES
                (@Id,@Pid,@Tipo,@Nome,@Qtd,
                 @Freq,@Local,@Data,0,0)",
            new
            {
                Id = Guid.NewGuid().ToString(),
                Pid = processoId,
                item.Tipo,
                item.Nome,
                item.Qtd,
                Freq = item.Frequencia,
                item.Local,
                item.Data
            });
        }

        // ======================
        // REMOVER ITEM
        // ======================

        public void RemoverItem(string processoId, string nome)
        {
            using var conn = _db.GetConnection();

            conn.Execute(
                "DELETE FROM itens_saude WHERE processo_id=@id AND nome=@nome",
                new { id = processoId, nome });
        }
    }
}
