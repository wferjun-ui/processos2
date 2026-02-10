using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SistemaJuridico.Models;
using SistemaJuridico.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace SistemaJuridico.ViewModels
{
    public partial class ProcessoDetalhesViewModel : ObservableObject
    {
        private readonly ContaService _contaService;
        private readonly DatabaseService _db;
        private readonly string _processoId;

        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _saldoDisplay = "R$ 0,00";
        [ObservableProperty] private SolidColorBrush _saldoCor = Brushes.Black;

        [ObservableProperty] private string _finTipo = "Alvará";
        [ObservableProperty] private string _finData = DateTime.Now.ToString("dd/MM/yyyy");
        [ObservableProperty] private string _finValor = "";

        public ObservableCollection<ContaModel> Contas { get; set; } = new();

        public ProcessoDetalhesViewModel(string id)
        {
            _processoId = id;
            _db = App.DB!;
            _contaService = new ContaService(_db);

            CarregarDadosProcesso();
            CarregarContas();
        }

        private void CarregarDadosProcesso()
        {
            using var conn = _db.GetConnection();

            var p = conn.QueryFirstOrDefault(
                "SELECT numero, paciente FROM processos WHERE id=@id",
                new { id = _processoId });

            if (p != null)
            {
                Numero = p.numero;
                Paciente = p.paciente;
            }
        }

        private void CarregarContas()
        {
            Contas.Clear();

            var lista = _contaService.GetContas(_processoId);

            decimal saldo = 0;

            foreach (var c in lista)
            {
                saldo += (c.ValorAlvara - c.ValorConta);
                Contas.Add(c);
            }

            SaldoDisplay = ProcessLogic.FormatMoney(saldo);
            SaldoCor = saldo >= 0 ? Brushes.Green : Brushes.Red;
        }

        [RelayCommand]
        public void AdicionarConta()
        {
            decimal valor = ProcessLogic.ParseMoney(FinValor);

            if (valor <= 0)
            {
                MessageBox.Show("Valor inválido");
                return;
            }

            var model = new ContaModel
            {
                ProcessoId = _processoId,
                Data = FinData,
                Tipo = FinTipo,
                Historico = FinTipo,
                ValorAlvara = FinTipo == "Alvará" ? valor : 0,
                ValorConta = FinTipo != "Alvará" ? valor : 0,
                Responsavel = "Sistema"
            };

            _contaService.SaveConta(model);

            FinValor = "";
            CarregarContas();
        }

        [RelayCommand]
        public void LancarTudo()
        {
            _contaService.LancarTudo(_processoId);
            CarregarContas();
        }
    }
}
