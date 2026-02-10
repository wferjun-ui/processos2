using System.Windows;
using SistemaJuridico.Services;
using SistemaJuridico.Views;

namespace SistemaJuridico
{
    public partial class App : Application
    {
        // Nullable para evitar aviso de construtor
        public static DatabaseService? DB { get; private set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var configService = new ConfigService();
            var config = configService.LoadConfig();
            string dbPath = "";

            if (config == null || string.IsNullOrEmpty(config.DatabasePath))
            {
                var setup = new SetupWindow();
                if (setup.ShowDialog() == true)
                {
                    // Recarrega configuração recém-criada
                    var newConfig = configService.LoadConfig();
                    if (newConfig != null) dbPath = newConfig.DatabasePath;
                }
                else
                {
                    Shutdown();
                    return;
                }
            }
            else
            {
                dbPath = config.DatabasePath;
            }

            // Fallback de segurança se algo falhou no setup
            if (string.IsNullOrEmpty(dbPath)) 
            {
                Shutdown();
                return;
            }

            DB = new DatabaseService(dbPath);
            DB.Initialize();

            var login = new LoginWindow();
            login.Show();
        }
    }
}
