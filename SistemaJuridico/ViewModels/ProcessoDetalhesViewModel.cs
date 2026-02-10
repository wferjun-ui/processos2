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
        private readonly DatabaseService _db;
        private readonly ContaService _contaService;
        private readonly VerificacaoService _verificacaoService;

        private readonly string _processoId;

        // ======================
        // DADOS DO PROCESSO
        // ======================

        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _faseProcessual = "Conhecimento";
        [ObservableProperty] private string _obsFixa = "";

        // ======================
        // FINANCEIRO
        // ======================

        [ObservableProperty] private string _saldoDisplay = "R$ 0,00";
        [ObservableProperty] private SolidColorBrush _saldoCor = Brushes.Black;

        [ObservableProperty] private string _finTipo = "Alvará";
        [ObservableProperty] private string _finData = DateTime.Now.ToString("dd/MM/yyyy");
        [ObservableProperty] private string _finValor = "";

        public ObservableCollection<ContaModel> Contas { get; set; } = new();

        // ======================
        // VERIFICAÇÕES
        // ======================

        [ObservableProperty] private string _responsavel = "";

        [ObservableProperty] private bool _diligenciaRealizada;
        [ObservableProperty] private string _diligenciaDesc = "";

        [ObservableProperty] private bool _diligenciaPendente;
        [ObservableProperty] private string _pendenciaDesc = "";

        [ObservableProperty] private string _proxData = "";

        // Snapshot visual dos itens saúde
        public ObservableCollection<ItemSaudeDto> ItensSaude { get; set; } = new();

        // ======================
        // CONSTRUTOR
        // ======================

        public ProcessoDetalhesViewModel(string processoId)
        {
            _processoId = processoId;
            _db = App.DB!;

            _contaService = new ContaService(_db);
            _verificacaoService = new VerificacaoService(_db);

            CarregarDadosProcesso();
            CarregarContas();
        }

        // ======================
        // CARREGAMENTO PROCESSO
        // ======================

        private void CarregarDadosProcesso()
        {
            using var conn = _db.GetConnection();

            var p = conn.QueryFirstOrDefault(
                @"SELECT numero, paciente, status_fase, observacao_fixa 
                  FROM processos WHERE id=@id",
                new { id = _processoId });

            if (p != null)
            {
                Numero = p.numero;
                Paciente = p.paciente;
                FaseProcessual = p.status_fase ?? "Conhecimento";
                ObsFixa = p.observacao_fixa ?? "";
            }
        }

        // ======================
        // FINANCEIRO
        // ======================

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

        // ======================
        // VERIFICAÇÃO PROCESSUAL
        // ======================

        [RelayCommand]
        public void SalvarVerificacao()
        {
            if (string.IsNullOrWhiteSpace(Responsavel))
            {
                MessageBox.Show("Informe o responsável");
                return;
            }

            var (prazo, notif) = ProcessLogic.CalculateDueDates(null, ProxData);

            var model = new VerificacaoModel
            {
                ProcessoId = _processoId,
                StatusProcesso = FaseProcessual,
                Responsavel = Responsavel,

                DiligenciaRealizada = DiligenciaRealizada,
                DiligenciaDescricao = DiligenciaDesc,

                DiligenciaPendente = DiligenciaPendente,
                PendenciaDescricao = PendenciaDesc,

                ProximoPrazo = prazo,
                DataNotificacao = notif,

                AlteracoesTexto = DiligenciaRealizada
                    ? $"[Dil] {DiligenciaDesc}"
                    : "Verificação de rotina",

                SnapshotItensJson = _verificacaoService
                    .GerarSnapshotItens(ItensSaude)
            };

            _verificacaoService.SalvarVerificacao(
                model,
                FaseProcessual,
                ObsFixa
            );

            MessageBox.Show("Verificação salva!");
        }
    }
}
