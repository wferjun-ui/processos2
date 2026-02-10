using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace SistemaJuridico.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string dbFolder)
        {
            if (string.IsNullOrWhiteSpace(dbFolder)) 
                throw new ArgumentException("Caminho do banco não pode ser vazio");

            Directory.CreateDirectory(dbFolder);
            var dbPath = Path.Combine(dbFolder, "juridico_v5.db");
            _connectionString = $"Data Source={dbPath}";
        }

        public SqliteConnection GetConnection() => new SqliteConnection(_connectionString);

        public void Initialize()
        {
            using var conn = GetConnection();
            conn.Open();
            
            // Ativa modo WAL para evitar bloqueios de arquivo
            conn.Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. CRIAÇÃO DAS TABELAS
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

                    CREATE TABLE IF NOT EXISTS itens_saude (
                        id TEXT PRIMARY KEY, processo_id TEXT, tipo TEXT, nome TEXT, 
                        qtd TEXT, frequencia TEXT, local TEXT, data_prescricao TEXT,
                        is_desnecessario INTEGER DEFAULT 0, tem_bloqueio INTEGER DEFAULT 0,
                        FOREIGN KEY(processo_id) REFERENCES processos(id) ON DELETE CASCADE);

                    CREATE TABLE IF NOT EXISTS verificacoes (
                        id TEXT PRIMARY KEY, processo_id TEXT, data_hora TEXT, status_processo TEXT, 
                        responsavel TEXT, diligencia_realizada INTEGER, diligencia_descricao TEXT,
                        diligencia_pendente INTEGER, pendencias_descricao TEXT, proximo_prazo_padrao TEXT, 
                        data_notificacao TEXT, alteracoes_texto TEXT, itens_snapshot_json TEXT,
                        FOREIGN KEY(processo_id) REFERENCES processos(id) ON DELETE CASCADE);

                    CREATE TABLE IF NOT EXISTS contas (
                        id TEXT PRIMARY KEY, processo_id TEXT, data_movimentacao TEXT,
                        tipo_lancamento TEXT, historico TEXT, mov_processo TEXT, num_nf_alvara TEXT,
                        valor_alvara REAL, valor_conta REAL, terapia_medicamento_nome TEXT,
                        quantidade TEXT, mes_referencia TEXT, ano_referencia TEXT, observacoes TEXT,
                        responsavel TEXT, status_conta TEXT DEFAULT 'lancado', 
                        FOREIGN KEY(processo_id) REFERENCES processos(id) ON DELETE CASCADE);
                ", transaction: transaction);

                // 2. RESET FORÇADO DO ADMIN (ATÔMICO NA MESMA TRANSAÇÃO)
                conn.Execute("DELETE FROM usuarios WHERE lower(username) = 'admin'", transaction: transaction);

                var salt = GenerateSalt();
                var hash = HashPassword("admin", salt);

                conn.Execute(@"
                    INSERT INTO usuarios (id, username, password_hash, salt, is_admin, email) 
                    VALUES (@id, 'admin', @h, @s, 1, 'admin@sistema.local')",
                    new { id = Guid.NewGuid().ToString(), h = hash, s = salt }, 
                    transaction: transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // --- AUTENTICAÇÃO ---

        public (bool Success, bool IsAdmin, string Username) Login(string username, string password)
        {
            using var conn = GetConnection();
            var user = conn.QueryFirstOrDefault("SELECT * FROM usuarios WHERE lower(username) = lower(@u)", new { u = username });

            if (user == null) return (false, false, "");

            string salt = user.salt;
            string storedHash = user.password_hash;
            string inputHash = HashPassword(password, salt);

            if (storedHash == inputHash)
            {
                bool isAdmin = (user.is_admin == 1);
                return (true, isAdmin, user.username);
            }
            return (false, false, "");
        }

        public void RegistrarUsuario(string username, string password, string email, bool isAdmin)
        {
            var salt = GenerateSalt();
            var hash = HashPassword(password, salt);
            
            using var conn = GetConnection();
            conn.Execute(@"
                INSERT INTO usuarios (id, username, password_hash, salt, is_admin, email) 
                VALUES (@id, @u, @h, @s, @a, @e)",
                new { 
                    id = Guid.NewGuid().ToString(), 
                    u = username, 
                    h = hash, 
                    s = salt, 
                    a = isAdmin ? 1 : 0, 
                    e = email 
                });
        }

        private string GenerateSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLower();
        }

        private string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var combined = Encoding.UTF8.GetBytes(password + salt);
            return Convert.ToHexString(sha256.ComputeHash(combined)).ToLower();
        }

        // --- MÉTODOS DE NEGÓCIO ---

        public void SalvarNovoProcesso(string numero, string paciente, string juiz, string reu, string classificacao, string usuarioLogado)
        {
            using var conn = GetConnection();
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                var procId = Guid.NewGuid().ToString();
                var hoje = DateTime.Now;
                
                var dataPrazo = hoje.AddDays(14); 
                if (dataPrazo.DayOfWeek == DayOfWeek.Saturday) dataPrazo = dataPrazo.AddDays(2);
                if (dataPrazo.DayOfWeek == DayOfWeek.Sunday) dataPrazo = dataPrazo.AddDays(1);
                
                var prazoStr = dataPrazo.ToString("dd/MM/yyyy");

                conn.Execute(@"INSERT INTO processos (id, numero, paciente, juiz, classificacao, status_fase, ultima_atualizacao, cache_proximo_prazo)
                    VALUES (@id, @num, @pac, @juiz, @class, 'Conhecimento', @dt, @prazo)",
                    new { id = procId, num = numero, pac = paciente, juiz, class = classificacao, dt = hoje.ToString("dd/MM/yyyy"), prazo = prazoStr }, trans);

                if (!string.IsNullOrEmpty(reu))
                {
                    conn.Execute("INSERT INTO reus (id, processo_id, nome) VALUES (@id, @pid, @nome)",
                        new { id = Guid.NewGuid().ToString(), pid = procId, nome = reu }, trans);
                }

                conn.Execute(@"INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, proximo_prazo_padrao, alteracoes_texto)
                    VALUES (@id, @pid, @dh, 'Cadastro Inicial', @resp, @prazo, 'Processo Criado')",
                    new { id = Guid.NewGuid().ToString(), pid = procId, dh = hoje.ToString("s"), resp = usuarioLogado, prazo = prazoStr }, trans);

                trans.Commit();
            }
            catch { trans.Rollback(); throw; }
        }
    }
}
