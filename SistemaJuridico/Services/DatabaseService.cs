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

        public DatabaseService()
        {
            // Caminho do banco de dados na pasta do usuário
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

            // --- CRIAÇÃO DAS TABELAS ---
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

            // --- CORREÇÃO DE LOGIN: FORÇAR ADMIN ---
            // Verifica se o admin já existe
            var adminExists = conn.ExecuteScalar<int>("SELECT count(*) FROM usuarios WHERE username = 'admin'");

            if (adminExists == 0)
            {
                // Se não existe, cria do zero
                RegistrarUsuario("admin", "admin", "admin@sistema.local", true);
            }
            else
            {
                // SE JÁ EXISTE, RESETA A SENHA PARA "admin" PARA GARANTIR O ACESSO
                var salt = GenerateSalt();
                var hash = HashPassword("admin", salt);
                conn.Execute("UPDATE usuarios SET password_hash = @h, salt = @s, is_admin = 1 WHERE username = 'admin'", 
                    new { h = hash, s = salt });
            }
        }

        // --- AUTENTICAÇÃO ---

        public (bool Success, bool IsAdmin, string Username) Login(string username, string password)
        {
            using var conn = GetConnection();
            var user = conn.QueryFirstOrDefault("SELECT * FROM usuarios WHERE username = @u", new { u = username });

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
                    VALUES (@id, @num, @pac, @juiz, @classificacao, 'Conhecimento', @dt, @prazo)",
                    new { 
                        id = procId, 
                        num = numero, 
                        pac = paciente, 
                        juiz, 
                        classificacao, 
                        dt = hoje.ToString("dd/MM/yyyy"), 
                        prazo = prazoStr 
                    }, trans);

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
            catch 
            { 
                trans.Rollback(); 
                throw; 
            }
        }
    }
}
