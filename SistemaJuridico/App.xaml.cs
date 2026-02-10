using System.Windows;
using SistemaJuridico.Services;
using SistemaJuridico.Views;

namespace SistemaJuridico
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1. Garante que o banco de dados existe
            var db = new DatabaseService();
            db.Initialize();

            // 2. Abre a tela de Login
            var login = new LoginWindow();
            login.Show();
        }
    }
}
