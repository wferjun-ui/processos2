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

        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _reu = "";
        [ObservableProperty] private string _classificacao = "Cível";

        public CadastroViewModel(Action closeAction)
{
    _db = App.DB;
    _closeAction = closeAction;
}

        [RelayCommand]
        public void Salvar()
        {
            if (string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Paciente))
            {
                MessageBox.Show("Preencha o Número e o Paciente.", "Aviso");
                return;
            }

            try
            {
                string usuario = Application.Current.Properties["Usuario"]?.ToString() ?? "Admin";
                _db.SalvarNovoProcesso(Numero, Paciente, Juiz, Reu, Classificacao, usuario);
                
                MessageBox.Show("Processo salvo com sucesso!");
                _closeAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}");
            }
        }
        
        // Formatação simples do CNJ ao digitar
        partial void OnNumeroChanged(string value)
        {
             // Limita a 20 caracteres
             if (value.Length > 25) Numero = value.Substring(0, 25);
        }
    }
}
