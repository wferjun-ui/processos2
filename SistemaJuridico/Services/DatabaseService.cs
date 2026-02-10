using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SistemaJuridico.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            // Salva na AppData do usuário (Não precisa de Admin)
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

            // Otimizações de Performance (Paridade com Python source: 72)
            conn.Execute("PRAGMA journal_mode=WAL;");
            conn.Execute("PRAGMA synchronous=NORMAL;");

            // Criação das Tabelas (Baseado em source: 74-82)
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS usuarios (
                    id TEXT PRIMARY KEY, username TEXT UNIQUE, password_hash TEXT, 
                    salt TEXT, is_admin INTEGER DEFAULT 0, email TEXT UNIQUE);

                CREATE TABLE IF NOT EXISTS processos (
                    id TEXT PRIMARY KEY, numero TEXT, paciente TEXT, 
                    status_fase TEXT, cache_proximo_prazo TEXT);
            ");

            // Seed Admin Padrão (Baseado em source: 86)
            var count = conn.ExecuteScalar<int>("SELECT count(*) FROM usuarios");
            if (count == 0)
            {
                var salt = GenerateSalt();
                var hash = HashPassword("admin", salt);
                conn.Execute("INSERT INTO usuarios (id, username, password_hash, salt, is_admin) VALUES (@id, @u, @h, @s, 1)",
                    new { id = Guid.NewGuid().ToString(), u = "admin", h = hash, s = salt });
            }
        }

        // Helpers de Criptografia (Baseado em source: 96)
        public static string GenerateSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLower();
        }

        public static string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var combined = Encoding.UTF8.GetBytes(password + salt);
            return Convert.ToHexString(sha256.ComputeHash(combined)).ToLower();
        }
    }
}
