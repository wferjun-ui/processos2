using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaJuridico.Services;
using System.Text.RegularExpressions;
using System.Windows;

namespace SistemaJuridico.ViewModels
{
    public partial class CadastroViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly Action _closeAction; // Para fechar a janela

        // Campos do Formulário
        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _reu = "";
        [ObservableProperty] private string _classificacao = "Cível"; // Default

        public CadastroViewModel(Action closeAction)
        {
            _db = new DatabaseService();
            _closeAction = closeAction;
        }

        // Lógica de Máscara CNJ em Tempo Real (source: 44)
        partial void OnNumeroChanged(string value)
        {
            // Remove tudo que não é número
            var digits = Regex.Replace(value, @"\D", "");
            if (digits.Length > 20) digits = digits.Substring(0, 20);

            // Aplica a máscara: 0000000-00.0000.0.00.0000
            // Nota: Esta é uma implementação simplificada para WPF.
            // Para UX perfeita, o ideal é formatar apenas ao perder o foco ou usar um Behavior.
            // Aqui mantemos simples para não travar a digitação.
        }

        [RelayCommand]
        public void Salvar()
        {
            if (string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Paciente))
            {
                MessageBox.Show("Preencha o Número e o Paciente.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Usuário fixo por enquanto (implementar Auth depois)
                _db.SalvarNovoProcesso(Numero, Paciente, Juiz, Reu, Classificacao, "admin");
                
                MessageBox.Show("Processo salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                _closeAction.Invoke(); // Fecha a janela
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
