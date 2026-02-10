using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaJuridico.Models;
using SistemaJuridico.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace SistemaJuridico.ViewModels
{
    public partial class CadastroViewModel : ObservableObject
    {
        private readonly ProcessService _service;
        private readonly Action _closeAction;

        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _classificacao = "Cível";

        public ObservableCollection<string> Reus { get; set; } = new();
        public ObservableCollection<ItemSaudeDto> ItensSaude { get; set; } = new();

        public CadastroViewModel(Action close)
        {
            _closeAction = close;
            _service = new ProcessService(App.DB!);
            Reus.Add("");
        }

        [RelayCommand]
        public void AddReu() => Reus.Add("");

        [RelayCommand]
        public void RemoveReu(string r) => Reus.Remove(r);

        [RelayCommand]
        public void AddSaude() => ItensSaude.Add(new());

        [RelayCommand]
        public void RemoveSaude(ItemSaudeDto i) => ItensSaude.Remove(i);

        [RelayCommand]
        public void Salvar()
        {
            if (string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Paciente))
            {
                MessageBox.Show("Campos obrigatórios");
                return;
            }

            var model = new ProcessoModel
            {
                Numero = Numero,
                Paciente = Paciente,
                Juiz = Juiz,
                Classificacao = Classificacao,
                Reus = Reus.ToList(),
                ItensSaude = ItensSaude.Select(i => new ItemSaudeModel
                {
                    Nome = i.Nome,
                    Tipo = i.Tipo,
                    Qtd = i.Qtd,
                    Frequencia = i.Frequencia,
                    Local = i.Local,
                    Data = i.Data
                }).ToList()
            };

            _service.SaveProcess(model);

            MessageBox.Show("Processo salvo!");
            _closeAction();
        }
    }
}
