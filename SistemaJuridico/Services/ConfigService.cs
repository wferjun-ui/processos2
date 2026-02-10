using System;
using System.IO;
using System.Text.Json;

namespace SistemaJuridico.Services
{
    public class AppConfig
    {
        public string DatabasePath { get; set; } = string.Empty;
    }

    public class ConfigService
    {
        private readonly string _configFolderPath;
        private readonly string _configPath;

        public ConfigService()
        {
            // FIX: Usa AppData do usuário para garantir permissão de escrita sem Admin
            _configFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SistemaJuridico");
            _configPath = Path.Combine(_configFolderPath, "config.json");
        }

        public AppConfig? LoadConfig()
        {
            if (!File.Exists(_configPath)) return null;
            try
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json);
            }
            catch { return null; }
        }

        public void SaveConfig(string dbPath)
        {
            if (!Directory.Exists(_configFolderPath))
            {
                Directory.CreateDirectory(_configFolderPath);
            }

            var config = new AppConfig { DatabasePath = dbPath };
            var json = JsonSerializer.Serialize(config);
            File.WriteAllText(_configPath, json);
        }
    }
}
