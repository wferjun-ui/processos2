using System.Windows;
using SistemaJuridico.Services;
using SistemaJuridico.Views;

namespace SistemaJuridico
{
    public partial class App : Application
    {
        public static DatabaseService? DB { get; private set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var configService = new ConfigService();
            var config = configService.LoadConfig();
            string dbPath = "";

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
                    Shutdown();
                    return;
                }
            }

            if (config != null) dbPath = config.DatabasePath;

            if (string.IsNullOrEmpty(dbPath))
            {
                Shutdown();
                return;
            }

            // INICIALIZAÇÃO CRÍTICA
            // Isso vai rodar o DELETE e INSERT do admin
            DB = new DatabaseService(dbPath);
            try 
            {
                DB.Initialize(); 
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Erro crítico ao criar banco de dados: {ex.Message}\nO app será fechado.", "Erro Fatal");
                Shutdown();
                return;
            }

            var login = new LoginWindow();
            MainWindow = login;
            login.Show();

            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
    }
}
