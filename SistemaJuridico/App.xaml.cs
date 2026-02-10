using System.Windows;
using SistemaJuridico.Services;
using SistemaJuridico.Views; // Certifique-se de criar a pasta Views

namespace SistemaJuridico
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1. Inicializa Banco de Dados
            var db = new DatabaseService();
            db.Initialize();

            // 2. Abre a Tela de Login
            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }
}
