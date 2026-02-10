using System.Windows;
using SistemaJuridico.Services;
using SistemaJuridico.Views;

namespace SistemaJuridico
{
    public partial class App : Application
    {
        // Nullable para evitar aviso, mas garantido no Startup
        public static DatabaseService? DB { get; private set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // FIX: Impede que o app feche quando a janela de Setup é fechada
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var configService = new ConfigService();
            var config = configService.LoadConfig();
            string dbPath = "";

            // 1. Fluxo de Setup (Se config não existir)
            if (config == null || string.IsNullOrEmpty(config.DatabasePath))
            {
                var setup = new SetupWindow();
                bool? result = setup.ShowDialog();

                if (result == true)
                {
                    config = configService.LoadConfig();
                }
                else
                {
                    Shutdown(); // Encerra se o usuário cancelar
                    return;
                }
            }

            if (config != null) dbPath = config.DatabasePath;

            if (string.IsNullOrEmpty(dbPath))
            {
                Shutdown();
                return;
            }

            // 2. Inicializa Banco e Reseta Admin
            DB = new DatabaseService(dbPath);
            DB.Initialize();

            // 3. Abre Login
            var login = new LoginWindow();
            MainWindow = login;
            login.Show();

            // FIX: Agora que a janela principal (Login) está aberta, restaura comportamento padrão
            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
    }
}
