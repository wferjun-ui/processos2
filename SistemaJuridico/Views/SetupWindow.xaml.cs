using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input; // Necessário para ICommand
using Microsoft.Win32;
using SistemaJuridico.Services;

namespace SistemaJuridico.Views
{
    public partial class SetupWindow : Window, INotifyPropertyChanged
    {
        private string _pathTexto = string.Empty;
        public string PathTexto
        {
            get => _pathTexto;
            set 
            { 
                _pathTexto = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PathTexto))); 
            }
        }

        // Evento anulável para satisfazer a interface
        public event PropertyChangedEventHandler? PropertyChanged;

        public RelayCommand SelecionarPastaCommand { get; }
        public RelayCommand ConfirmarCommand { get; }

        public SetupWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            PathTexto = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SistemaJuridico");

            SelecionarPastaCommand = new RelayCommand(_ => {
                // Em WPF moderno (Core/5+), OpenFolderDialog é nativo
                var dialog = new OpenFolderDialog();
                if (dialog.ShowDialog() == true)
                {
                    PathTexto = dialog.FolderName;
                }
            });

            ConfirmarCommand = new RelayCommand(_ => {
                var config = new ConfigService();
                config.SaveConfig(PathTexto);
                this.DialogResult = true;
                this.Close();
            });
        }
    }

    // Helper RelayCommand corrigido para nulabilidade
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        
        public RelayCommand(Action<object?> execute) => _execute = execute;
        
        public bool CanExecute(object? parameter) => true;
        
        public void Execute(object? parameter) => _execute(parameter);
        
        // Evento necessário pela interface, mas não usado aqui
        public event EventHandler? CanExecuteChanged 
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
