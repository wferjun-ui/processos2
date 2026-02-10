using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;

namespace SistemaJuridico.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SistemaJuridico");
            Directory.CreateDirectory(folder);
            var dbPath = Path.Combine(folder, "juridico_v5.db");
            _connectionString = $"Data Source={dbPath}";
        }

        public SqliteConnection GetConnection() => new SqliteConnection(_connectionString);

        public void Initialize()
        {
            using var conn = GetConnection();
            conn.Open();
            conn.Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");

            // Esquema completo baseado no Python (source: 74-82)
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS usuarios (
                    id TEXT PRIMARY KEY, username TEXT UNIQUE, password_hash TEXT, salt TEXT, is_admin INTEGER DEFAULT 0, email TEXT);

                CREATE TABLE IF NOT EXISTS processos (
                    id TEXT PRIMARY KEY, numero TEXT, is_antigo INTEGER, paciente TEXT, 
                    juiz TEXT, genitor_rep_nome TEXT, classificacao TEXT, status_fase TEXT, 
                    ultima_atualizacao TEXT, observacao_fixa TEXT, cache_proximo_prazo TEXT);

                CREATE TABLE IF NOT EXISTS reus (
                    id TEXT PRIMARY KEY, processo_id TEXT, nome TEXT,
                    FOREIGN KEY(processo_id) REFERENCES processos(id) ON DELETE CASCADE);

                CREATE TABLE IF NOT EXISTS verificacoes (
                    id TEXT PRIMARY KEY, processo_id TEXT, data_hora TEXT, status_processo TEXT, 
                    responsavel TEXT, proximo_prazo_padrao TEXT, data_notificacao TEXT, alteracoes_texto TEXT,
                    FOREIGN KEY(processo_id) REFERENCES processos(id) ON DELETE CASCADE);
            ");
        }

        // Transação para salvar Processo + Réu + Verificação Inicial (source: 331-337)
        public void SalvarNovoProcesso(string numero, string paciente, string juiz, string reu, string classificacao, string usuarioLogado)
        {
            using var conn = GetConnection();
            conn.Open();
            using var trans = conn.BeginTransaction();

            try
            {
                var procId = Guid.NewGuid().ToString();
                var hoje = DateTime.Now;
                
                // 1. Calcula prazo inicial (14 dias + ajuste) - Lógica portada de (source: 48)
                var dataPrazo = hoje.AddDays(14);
                // Ajuste simples de dia útil (se cair no sábado/domingo, joga para segunda - simplificado)
                if (dataPrazo.DayOfWeek == DayOfWeek.Saturday) dataPrazo = dataPrazo.AddDays(2);
                if (dataPrazo.DayOfWeek == DayOfWeek.Sunday) dataPrazo = dataPrazo.AddDays(1);
                
                var prazoStr = dataPrazo.ToString("dd/MM/yyyy");

                // 2. Insere Processo
                conn.Execute(@"
                    INSERT INTO processos (id, numero, paciente, juiz, classificacao, status_fase, ultima_atualizacao, cache_proximo_prazo)
                    VALUES (@id, @num, @pac, @juiz, @class, 'Conhecimento', @dt, @prazo)",
                    new { id = procId, num = numero, pac = paciente, juiz, class = classificacao, dt = hoje.ToString("dd/MM/yyyy"), prazo = prazoStr }, trans);

                // 3. Insere Réu Inicial
                if (!string.IsNullOrEmpty(reu))
                {
                    conn.Execute("INSERT INTO reus (id, processo_id, nome) VALUES (@id, @pid, @nome)",
                        new { id = Guid.NewGuid().ToString(), pid = procId, nome = reu }, trans);
                }

                // 4. Cria Verificação Inicial (Histórico)
                conn.Execute(@"
                    INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, proximo_prazo_padrao, alteracoes_texto)
                    VALUES (@id, @pid, @dh, 'Cadastro Inicial', @resp, @prazo, 'Processo Criado')",
                    new { id = Guid.NewGuid().ToString(), pid = procId, dh = hoje.ToString("s"), resp = usuarioLogado, prazo = prazoStr }, trans);

                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }
    }
}
