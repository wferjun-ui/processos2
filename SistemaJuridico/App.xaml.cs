using System.Windows;
using SistemaJuridico.Services;
using SistemaJuridico.Views;

namespace SistemaJuridico
{
    public partial class App : Application
    {
        // Torna o serviço de banco acessível globalmente (Singleton simples)
        public static DatabaseService DB { get; private set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var configService = new ConfigService();
            var config = configService.LoadConfig();
            string dbPath = "";

            // Se não tem config, abre a janela de Setup
            if (config == null || string.IsNullOrEmpty(config.DatabasePath))
            {
                var setup = new SetupWindow();
                if (setup.ShowDialog() == true)
                {
                    // Recarrega após salvar
                    config = configService.LoadConfig();
                }
                else
                {
                    // Se cancelou o setup, fecha o app
                    Shutdown();
                    return;
                }
            }

            dbPath = config.DatabasePath;

            // Inicializa o Banco com o caminho escolhido
            DB = new DatabaseService(dbPath);
            DB.Initialize(); // Aqui ele recria o admin/admin

            // Abre Login
            var login = new LoginWindow();
            login.Show();
        }
    }
}
