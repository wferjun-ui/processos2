using Dapper;
using SistemaJuridico.Models;
using System.Text.Json;

namespace SistemaJuridico.Services
{
    public class LogHistoricoService
    {
        private readonly DatabaseService _db;

        public LogHistoricoService(DatabaseService db)
        {
            _db = db;
        }

        // ======================
        // REGISTRAR EVENTO
        // ======================

        public void RegistrarEvento(
            string processoId,
            string usuario,
            string tipoEvento,
            string descricao,
            object? dadosExtras = null)
        {
            using var conn = _db.GetConnection();

            conn.Execute(@"
                INSERT INTO logs_historico
                (id, processo_id, data_hora, usuario,
                 tipo_evento, descricao, dados_extras_json)
                VALUES
                (@Id,@Pid,@Dh,@Usr,@Tipo,@Desc,@Extras)",
            new
            {
                Id = Guid.NewGuid().ToString(),
                Pid = processoId,
                Dh = DateTime.Now.ToString("s"),
                Usr = usuario,
                Tipo = tipoEvento,
                Desc = descricao,
                Extras = dadosExtras == null
                    ? ""
                    : JsonSerializer.Serialize(dadosExtras)
            });
        }

        // ======================
        // CONSULTAR HISTÓRICO
        // ======================

        public List<LogHistoricoModel> GetHistorico(string processoId)
        {
            using var conn = _db.GetConnection();

            return conn.Query<LogHistoricoModel>(
                @"SELECT
                    id as Id,
                    processo_id as ProcessoId,
                    data_hora as DataHora,
                    usuario as Usuario,
                    tipo_evento as TipoEvento,
                    descricao as Descricao,
                    dados_extras_json as DadosExtrasJson
                  FROM logs_historico
                  WHERE processo_id = @id
                  ORDER BY data_hora DESC",
                new { id = processoId }).ToList();
        }
    }
}
