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
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        // Retorna anulável (AppConfig?) explicitamente
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
            var config = new AppConfig { DatabasePath = dbPath };
            var json = JsonSerializer.Serialize(config);
            File.WriteAllText(_configPath, json);
        }
    }
}
