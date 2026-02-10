using System.Windows;
using System.IO;
using SistemaJuridico.Services;

namespace SistemaJuridico
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Garante que o banco existe ao iniciar
            var db = new DatabaseService();
            db.Initialize();
        }
    }
}
