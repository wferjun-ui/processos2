using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SistemaJuridico.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly string _backupFolder;

        public DatabaseService(string dbFolder)
        {
            if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);
            _dbPath = Path.Combine(dbFolder, "juridico_v5.db");
            _backupFolder = Path.Combine(dbFolder, "Backups");
            _connectionString = $"Data Source={_dbPath}";
            Initialize();
        }

        public SqliteConnection GetConnection() => new SqliteConnection(_connectionString);

        public void Initialize()
        {
            using var conn = GetConnection();
            conn.Open();
            // Otimizações do Python (WAL Mode)
            conn.Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;");

            // Schema Idêntico ao Python
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS usuarios (
                    id TEXT PRIMARY KEY, username TEXT UNIQUE, password_hash TEXT, 
                    salt TEXT, is_admin INTEGER DEFAULT 0, email TEXT UNIQUE);

                CREATE TABLE IF NOT EXISTS processos (
                    id TEXT PRIMARY KEY, numero TEXT, is_antigo INTEGER, paciente TEXT, 
                    juiz TEXT, genitor_rep_nome TEXT, genitor_rep_tipo TEXT, 
                    classificacao TEXT, status_fase TEXT, ultima_atualizacao TEXT,
                    observacao_fixa TEXT, cache_proximo_prazo TEXT, cache_status_fase TEXT);

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
                    diligencia_pendente INTEGER, pendencias_descricao TEXT, prazo_diligencia TEXT,
                    proximo_prazo_padrao TEXT, data_notificacao TEXT, alteracoes_texto TEXT,
                    itens_snapshot_json TEXT, FOREIGN KEY(processo_id) REFERENCES processos(id) ON DELETE CASCADE);

                CREATE TABLE IF NOT EXISTS contas (
                    id TEXT PRIMARY KEY, processo_id TEXT, data_movimentacao TEXT,
                    tipo_lancamento TEXT, historico TEXT, mov_processo TEXT, num_nf_alvara TEXT,
                    valor_alvara REAL, valor_conta REAL, terapia_medicamento_nome TEXT,
                    quantidade TEXT, mes_referencia TEXT, ano_referencia TEXT, observacoes TEXT,
                    responsavel TEXT, status_conta TEXT DEFAULT 'lancado', 
                    FOREIGN KEY(processo_id) REFERENCES processos(id) ON DELETE CASCADE);
                
                CREATE TABLE IF NOT EXISTS sugestoes_tratamento (
                    nome TEXT PRIMARY KEY, tipo TEXT);
            ");

            SeedAdmin(conn);
        }

        private void SeedAdmin(SqliteConnection conn)
        {
            var count = conn.ExecuteScalar<int>("SELECT count(*) FROM usuarios");
            if (count == 0)
            {
                var salt = GenerateSalt();
                var hash = HashPassword("admin", salt);
                conn.Execute("INSERT INTO usuarios (id, username, password_hash, salt, is_admin, email) VALUES (@id, 'admin', @h, @s, 1, 'admin@sistema.local')",
                    new { id = Guid.NewGuid().ToString(), h = hash, s = salt });
            }
        }

        public void PerformBackup()
        {
            try {
                if (!Directory.Exists(_backupFolder)) Directory.CreateDirectory(_backupFolder);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(_dbPath, Path.Combine(_backupFolder, $"backup_auto_{timestamp}.db"), true);
                
                // Mantém apenas os 10 últimos backups
                var files = new DirectoryInfo(_backupFolder).GetFiles("*.db")
                    .OrderByDescending(f => f.CreationTime).Skip(10);
                foreach (var f in files) f.Delete();
            } catch {}
        }

        public string GenerateSalt() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower();
        
        public string HashPassword(string password, string salt) {
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(password + salt))).ToLower();
        }

        public (bool Success, bool IsAdmin, string Username) Login(string username, string password) {
            using var conn = GetConnection();
            var user = conn.QueryFirstOrDefault("SELECT * FROM usuarios WHERE username = @u OR email = @u", new { u = username });
            
            if (user == null) return (false, false, "");
            
            // Verifica se o usuário tem senha definida (Fluxo de Primeiro Acesso do Python)
            if (string.IsNullOrEmpty(user.password_hash)) return (false, false, "");

            string inputHash = HashPassword(password, user.salt ?? "");
            if (user.password_hash == inputHash) return (true, user.is_admin == 1, user.username);
            
            return (false, false, "");
        }

        // Feature Python: Registrar e-mail autorizado
        public bool AuthorizeEmail(string email, bool isAdmin) {
            using var conn = GetConnection();
            try {
                conn.Execute("INSERT INTO usuarios (id, email, username, is_admin) VALUES (@id, @e, @e, @a)",
                    new { id = Guid.NewGuid().ToString(), e = email, a = isAdmin ? 1 : 0 });
                return true;
            } catch { return false; }
        }

        // Feature Python: Completar cadastro (Primeiro Acesso)
        public string CompleteRegistration(string email, string newUser, string newPass) {
            using var conn = GetConnection();
            var user = conn.QueryFirstOrDefault("SELECT id FROM usuarios WHERE email = @e", new { e = email });
            if (user == null) return "E-mail não autorizado.";

            // Se mudou o username, verifica colisão
            if (email != newUser) {
                var exists = conn.ExecuteScalar<int>("SELECT count(*) FROM usuarios WHERE username = @u", new { u = newUser });
                if (exists > 0) return "Nome de usuário já existe.";
            }

            var salt = GenerateSalt();
            var hash = HashPassword(newPass, salt);
            conn.Execute("UPDATE usuarios SET username=@u, password_hash=@h, salt=@s WHERE email=@e",
                new { u = newUser, h = hash, s = salt, e = email });
            return "OK";
        }
    }
}
