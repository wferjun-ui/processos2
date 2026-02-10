using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaJuridico.Services;
using System;
using System.Windows;
using System.Text.RegularExpressions;

namespace SistemaJuridico.ViewModels
{
    public partial class CadastroViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly Action _closeAction;

        // Inicialização obrigatória
        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _reu = "";
        [ObservableProperty] private string _classificacao = "Cível";

        public CadastroViewModel(Action closeAction)
        {
            _db = App.DB!;
            _closeAction = closeAction;
        }

        [RelayCommand]
        public void Salvar()
        {
            if (string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Paciente))
            {
                MessageBox.Show("Preencha o Número CNJ e o Paciente.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string usuario = Application.Current.Properties["Usuario"]?.ToString() ?? "Admin";
                
                _db.SalvarNovoProcesso(Numero, Paciente, Juiz, Reu, Classificacao, usuario);
                
                MessageBox.Show("Processo salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                _closeAction.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // Formatação simples ao digitar
        partial void OnNumeroChanged(string value)
        {
             // Limpeza básica
             var limpo = Regex.Replace(value, @"[^0-9\-\.]", "");
             if (limpo.Length > 25) Numero = limpo.Substring(0, 25);
             else if (limpo != value) Numero = limpo;
        }
    }
}
