using System.Windows;
using Microsoft.Win32;
using SistemaJuridico.Services;
using System.ComponentModel;

namespace SistemaJuridico.Views
{
    public partial class SetupWindow : Window, INotifyPropertyChanged
    {
        private string _pathTexto;
        public string PathTexto
        {
            get => _pathTexto;
            set { _pathTexto = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PathTexto))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // Comandos manuais para simplificar
        public RelayCommand SelecionarPastaCommand { get; }
        public RelayCommand ConfirmarCommand { get; }

        public SetupWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            // Define padrão inicial
            PathTexto = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "SistemaJuridico");

            SelecionarPastaCommand = new RelayCommand(_ => {
                var dialog = new OpenFolderDialog();
                if (dialog.ShowDialog() == true)
                {
                    PathTexto = dialog.FolderName;
                }
            });

            ConfirmarCommand = new RelayCommand(_ => {
                // Salva configuração e fecha
                var config = new ConfigService();
                config.SaveConfig(PathTexto);
                this.DialogResult = true;
                this.Close();
            });
        }
    }

    // Helper simples de comando para evitar criar outro arquivo
    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly System.Action<object> _execute;
        public RelayCommand(System.Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event System.EventHandler CanExecuteChanged;
    }
}
