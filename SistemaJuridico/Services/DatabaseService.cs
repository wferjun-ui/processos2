using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SistemaJuridico.Services
{
    // Modelos de Dados (Espelho do Python)
    public class UserModel 
    { 
        public string Id { get; set; } = string.Empty; 
        public string Username { get; set; } = string.Empty; 
        public string Email { get; set; } = string.Empty; 
        public bool IsAdmin { get; set; } 
    }

    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly string _backupFolder;

        public DatabaseService(string dbFolder)
        {
            if (string.IsNullOrWhiteSpace(dbFolder)) 
                throw new ArgumentException("Caminho do banco não pode ser vazio");

            if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);
            
            _dbPath = Path.Combine(dbFolder, "juridico_v5.db");
            _backupFolder = Path.Combine(dbFolder, "Backups");
            _connectionString = $"Data Source={_dbPath}";
        }

        public SqliteConnection GetConnection() => new SqliteConnection(_connectionString);

        public void Initialize()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                // Otimização de Performance (WAL Mode do Python)
                conn.Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. CRIAÇÃO DAS TABELAS (Schema Completo do Python)
                        conn.Execute(@"
                            CREATE TABLE IF NOT EXISTS usuarios (
                                id TEXT PRIMARY KEY, username TEXT UNIQUE, password_hash TEXT, salt TEXT, is_admin INTEGER DEFAULT 0, email TEXT UNIQUE);

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
                        ", transaction: transaction);

                        // 2. RESET FORÇADO DO ADMIN (Para garantir acesso na primeira vez)
                        // Remove qualquer usuário admin antigo para recriar com a senha padrão correta
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
            }
            
            // Realiza backup inicial ao abrir
            PerformBackup();
        }

        // --- SISTEMA DE BACKUP AUTOMÁTICO (Python source: 97) ---
        public void PerformBackup()
        {
            try
            {
                if (!Directory.Exists(_backupFolder)) Directory.CreateDirectory(_backupFolder);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(_backupFolder, $"backup_auto_{timestamp}.db");

                // Copia o arquivo do banco
                File.Copy(_dbPath, backupPath, true);

                // Mantém apenas os 10 últimos backups
                var files = new DirectoryInfo(_backupFolder).GetFiles("*.db")
                                                            .OrderBy(f => f.CreationTime)
                                                            .ToList();
                
                while (files.Count > 10)
                {
                    files[0].Delete();
                    files.RemoveAt(0);
                }
            }
            catch { /* Falhas de backup não devem travar o app */ }
        }

        // --- AUTENTICAÇÃO ---
        public (bool Success, bool IsAdmin, string Username) Login(string username, string password)
        {
            using (var conn = GetConnection())
            {
                var user = conn.QueryFirstOrDefault("SELECT * FROM usuarios WHERE lower(username) = lower(@u)", new { u = username });

                if (user == null) return (false, false, "");

                string salt = user.salt;
                string storedHash = user.password_hash;
                string inputHash = HashPassword(password, salt);

                if (storedHash == inputHash)
                {
                    return (true, user.is_admin == 1, user.username);
                }
                return (false, false, "");
            }
        }

        public void RegistrarUsuario(string username, string password, string email, bool isAdmin)
        {
            var salt = GenerateSalt();
            var hash = HashPassword(password, salt);
            
            using (var conn = GetConnection())
            {
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
        }

        // --- MÉTODOS DE ADMINISTRAÇÃO ---
        public IEnumerable<UserModel> GetAllUsers()
        {
            using (var conn = GetConnection())
            {
                var users = conn.Query("SELECT * FROM usuarios");
                return users.Select(u => new UserModel 
                { 
                    Id = u.id, 
                    Username = u.username, 
                    Email = u.email, 
                    IsAdmin = (u.is_admin == 1) 
                });
            }
        }

        public void DeleteUser(string id)
        {
            using (var conn = GetConnection())
            {
                conn.Execute("DELETE FROM usuarios WHERE id = @id", new { id });
            }
        }

        // --- CRYPTO HELPER ---
        private string GenerateSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToHexString(bytes).ToLower();
        }

        private string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var combined = Encoding.UTF8.GetBytes(password + salt);
                return Convert.ToHexString(sha256.ComputeHash(combined)).ToLower();
            }
        }
    }
}
