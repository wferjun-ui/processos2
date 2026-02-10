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
        private readonly string _dbPath;

        public DatabaseService(string dbFolder)
        {
            if (string.IsNullOrWhiteSpace(dbFolder)) 
                throw new ArgumentException("Caminho do banco não pode ser vazio");

            Directory.CreateDirectory(dbFolder);
            _dbPath = Path.Combine(dbFolder, "juridico_v5.db");
            _connectionString = $"Data Source={_dbPath}";
        }

        public SqliteConnection GetConnection() => new SqliteConnection(_connectionString);

        public void Initialize()
        {
            using var conn = GetConnection();
            conn.Open();
            
            // Otimização WAL
            conn.Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");

            // 1. Criação das Tabelas (Schema completo do Python)
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
            ");

            // 2. Reset Agressivo de Admin (Garante acesso inicial)
            ResetAdminUser(conn);

            // 3. Backup Automático na Inicialização
            PerformBackup();
        }

        private void ResetAdminUser(SqliteConnection conn)
        {
            // Apaga para recriar com hash limpo
            conn.Execute("DELETE FROM usuarios WHERE username = 'admin'");
            RegistrarUsuario("admin", "admin", "admin@sistema.local", true);
        }

        public void PerformBackup()
        {
            try
            {
                var backupFolder = Path.Combine(Path.GetDirectoryName(_dbPath)!, "Backups");
                Directory.CreateDirectory(backupFolder);
                
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(backupFolder, $"backup_auto_{timestamp}.db");
                
                // Copia o arquivo (SQLite permite cópia mesmo em uso se estiver em WAL, mas idealmente fazemos via API backup se conexão estivesse aberta)
                // Como aqui é safe copy de arquivo:
                File.Copy(_dbPath, backupPath, true);

                // Limpeza de backups antigos (> 10)
                var files = Directory.GetFiles(backupFolder, "*.db")
                                     .OrderBy(f => new FileInfo(f).CreationTime)
                                     .ToList();
                
                while (files.Count > 10)
                {
                    File.Delete(files[0]);
                    files.RemoveAt(0);
                }
            }
            catch { /* Ignorar erros de backup silenciosamente para não travar boot */ }
        }

        public (bool Success, bool IsAdmin, string Username) Login(string username, string password)
        {
            using var conn = GetConnection();
            var user = conn.QueryFirstOrDefault("SELECT * FROM usuarios WHERE lower(username) = lower(@u)", new { u = username });

            if (user == null) return (false, false, "");

            string salt = user.salt;
            string storedHash = user.password_hash;
            string inputHash = HashPassword(password, salt);

            if (storedHash == inputHash)
                return (true, user.is_admin == 1, user.username);
            
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
                    id = Guid.NewGuid().ToString(), u = username, h = hash, s = salt, a = isAdmin ? 1 : 0, e = email 
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

        // Método Genérico para Salvar Processo (Novo Cadastro)
        public void SalvarNovoProcesso(string numero, string paciente, string juiz, string reu, string classificacao, string usuarioLogado)
        {
            using var conn = GetConnection();
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                var procId = Guid.NewGuid().ToString();
                var hoje = DateTime.Now;
                
                // Cálculo de Prazo Simples (14 dias)
                var dataPrazo = hoje.AddDays(14); 
                if (dataPrazo.DayOfWeek == DayOfWeek.Saturday) dataPrazo = dataPrazo.AddDays(2);
                if (dataPrazo.DayOfWeek == DayOfWeek.Sunday) dataPrazo = dataPrazo.AddDays(1);
                var prazoStr = dataPrazo.ToString("dd/MM/yyyy");

                // FIX: Uso de @classificacao em vez de @class
                conn.Execute(@"INSERT INTO processos (id, numero, paciente, juiz, classificacao, status_fase, ultima_atualizacao, cache_proximo_prazo)
                    VALUES (@id, @num, @pac, @juiz, @classificacao, 'Conhecimento', @dt, @prazo)",
                    new { id = procId, num = numero, pac = paciente, juiz, classificacao, dt = hoje.ToString("dd/MM/yyyy"), prazo = prazoStr }, trans);

                if (!string.IsNullOrEmpty(reu))
                    conn.Execute("INSERT INTO reus (id, processo_id, nome) VALUES (@id, @pid, @nome)",
                        new { id = Guid.NewGuid().ToString(), pid = procId, nome = reu }, trans);

                conn.Execute(@"INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, proximo_prazo_padrao, alteracoes_texto)
                    VALUES (@id, @pid, @dh, 'Cadastro Inicial', @resp, @prazo, 'Processo Criado')",
                    new { id = Guid.NewGuid().ToString(), pid = procId, dh = hoje.ToString("s"), resp = usuarioLogado, prazo = prazoStr }, trans);

                trans.Commit();
            }
            catch { trans.Rollback(); throw; }
        }
    }
}
