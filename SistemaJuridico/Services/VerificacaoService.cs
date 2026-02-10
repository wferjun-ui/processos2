using Dapper;
using SistemaJuridico.Models;
using System.Text.Json;

namespace SistemaJuridico.Services
{
    public class VerificacaoService
    {
        private readonly DatabaseService _db;

        public VerificacaoService(DatabaseService db)
        {
            _db = db;
        }

        public void SalvarVerificacao(
            VerificacaoModel model,
            string faseProcessual,
            string obsFixa)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var trans = conn.BeginTransaction();

            conn.Execute(@"
                UPDATE processos
                SET status_fase=@fase,
                    observacao_fixa=@obs,
                    cache_proximo_prazo=@prazo,
                    ultima_atualizacao=@ua
                WHERE id=@id
            ",
            new
            {
                fase = faseProcessual,
                obs = obsFixa,
                prazo = model.ProximoPrazo,
                ua = DateTime.Now.ToString("dd/MM/yyyy"),
                id = model.ProcessoId
            }, trans);

            conn.Execute(@"
                INSERT INTO verificacoes
                (id, processo_id, data_hora, status_processo, responsavel,
                 diligencia_realizada, diligencia_descricao,
                 diligencia_pendente, pendencias_descricao,
                 proximo_prazo_padrao, data_notificacao,
                 alteracoes_texto, itens_snapshot_json)
                VALUES
                (@Id,@Pid,@Dh,@Status,@Resp,
                 @Dr,@Dd,@Dp,@Pd,@Pp,@Dn,@At,@Snap)
            ",
            new
            {
                model.Id,
                Pid = model.ProcessoId,
                Dh = DateTime.Now.ToString("s"),
                Status = model.StatusProcesso,
                Resp = model.Responsavel,
                Dr = model.DiligenciaRealizada ? 1 : 0,
                Dd = model.DiligenciaDescricao,
                Dp = model.DiligenciaPendente ? 1 : 0,
                Pd = model.PendenciaDescricao,
                Pp = model.ProximoPrazo,
                Dn = model.DataNotificacao,
                At = model.AlteracoesTexto,
                Snap = model.SnapshotItensJson
            }, trans);

            trans.Commit();
            _db.PerformBackup();
        }

        public string GerarSnapshotItens(object itens)
        {
            return JsonSerializer.Serialize(itens);
        }
    }
}
