using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;

namespace SistemaJuridico.ViewModels
{
    // DTOs auxiliares
    public partial class ReuDto : ObservableObject { [ObservableProperty] private string _nome = ""; }

    // --- CLASSE QUE FALTAVA ---
    public partial class ItemCadastroDto : ObservableObject 
    {
        [ObservableProperty] private string _tipo = "Medicamento";
        [ObservableProperty] private string _nome = "";
        [ObservableProperty] private string _qtd = "";
        [ObservableProperty] private string _frequencia = "Mensal";
        [ObservableProperty] private string _local = "Clínica";
        [ObservableProperty] private string _data = "";
    }
    // --------------------------

    public partial class CadastroViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly Action _closeAction;
        private string _processoIdEdit;

        [ObservableProperty] private string _tituloJanela = "Novo Processo";
        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private bool _isAntigo;
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _classificacao = "Cível";
        [ObservableProperty] private string _genitorNome = "";
        [ObservableProperty] private string _genitorTipo = "Genitor";
        [ObservableProperty] private Visibility _saudeVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _btnExcluirVisibility = Visibility.Collapsed;

        // Autocomplete Collections
        public ObservableCollection<string> SugestoesJuiz { get; set; } = new();
        public ObservableCollection<string> SugestoesPaciente { get; set; } = new();
        public ObservableCollection<string> SugestoesGenitor { get; set; } = new();

        public ObservableCollection<ReuDto> Reus { get; set; } = new();
        public ObservableCollection<ItemCadastroDto> ItensSaude { get; set; } = new();
        public ObservableCollection<string> SugestoesTratamento { get; set; } = new();

        public CadastroViewModel(Action closeAction, string processoId = null)
        {
            _db = App.DB!;
            _closeAction = closeAction;
            _processoIdEdit = processoId;
            
            CarregarSugestoesTratamento();

            if (processoId != null) CarregarDados(processoId);
            else Reus.Add(new ReuDto());
        }

        // --- LÓGICA DE AUTOCOMPLETE SIMULADA ---
        // Em WPF puro, a View deve bindar 'TextChanged' ou usar um ComboBox IsEditable
        // Aqui simulamos buscando do banco quando a propriedade muda
        
        partial void OnJuizChanged(string value) => BuscarSugestao("juiz", value, SugestoesJuiz);
        partial void OnPacienteChanged(string value) => BuscarSugestao("paciente", value, SugestoesPaciente);
        partial void OnGenitorNomeChanged(string value) => BuscarSugestao("genitor_rep_nome", value, SugestoesGenitor);

        private void BuscarSugestao(string campo, string termo, ObservableCollection<string> lista)
        {
            if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2) return;
            // Debounce seria ideal, mas para local DB é rápido o suficiente
            using var conn = _db.GetConnection();
            var results = conn.Query<string>($"SELECT DISTINCT {campo} FROM processos WHERE {campo} LIKE @q LIMIT 5", new { q = $"%{termo}%" });
            
            lista.Clear();
            foreach(var r in results) if(!string.IsNullOrEmpty(r)) lista.Add(r);
        }

        private void CarregarSugestoesTratamento() {
            using var conn = _db.GetConnection();
            var items = conn.Query<string>("SELECT nome FROM sugestoes_tratamento LIMIT 50");
            foreach(var i in items) SugestoesTratamento.Add(i);
        }

        private void CarregarDados(string id)
        {
            TituloJanela = "Editar Processo";
            BtnExcluirVisibility = Visibility.Visible;
            using var conn = _db.GetConnection();
            var p = conn.QueryFirstOrDefault("SELECT * FROM processos WHERE id=@id", new { id });
            
            Numero = p.numero; IsAntigo = p.is_antigo == 1; 
            // Preenche sem disparar o autocomplete loucamente
            _paciente = p.paciente; OnPropertyChanged(nameof(Paciente));
            _juiz = p.juiz; OnPropertyChanged(nameof(Juiz));
            _genitorNome = p.genitor_rep_nome; OnPropertyChanged(nameof(GenitorNome));
            
            Classificacao = p.classificacao; GenitorTipo = p.genitor_rep_tipo;
            
            var rs = conn.Query("SELECT nome FROM reus WHERE processo_id=@id", new { id });
            foreach(var r in rs) Reus.Add(new ReuDto { Nome = r.nome });

            if (Classificacao == "Saúde") {
                SaudeVisibility = Visibility.Visible;
                var its = conn.Query<ItemCadastroDto>("SELECT tipo as Tipo, nome as Nome, qtd as Qtd, frequencia as Frequencia, local as Local, data_prescricao as Data FROM itens_saude WHERE processo_id=@id", new { id });
                foreach(var i in its) ItensSaude.Add(i);
            }
        }

        partial void OnNumeroChanged(string value) {
            if (IsAntigo) return;
            var fmt = ProcessLogic.FormatCNJ(value);
            if (fmt != value) Numero = fmt;
        }

        partial void OnClassificacaoChanged(string value) => SaudeVisibility = value == "Saúde" ? Visibility.Visible : Visibility.Collapsed;

        [RelayCommand] public void AddReu() => Reus.Add(new ReuDto());
        [RelayCommand] public void RemoveReu(ReuDto item) => Reus.Remove(item);
        [RelayCommand] public void AddSaude() => ItensSaude.Add(new ItemCadastroDto());
        [RelayCommand] public void RemoveSaude(ItemCadastroDto item) => ItensSaude.Remove(item);

        [RelayCommand]
        public void Salvar()
        {
            if (string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Paciente)) { MessageBox.Show("Preencha campos obrigatórios."); return; }

            try {
                using var conn = _db.GetConnection(); conn.Open(); using var trans = conn.BeginTransaction();
                string pid = _processoIdEdit ?? Guid.NewGuid().ToString();
                
                if (_processoIdEdit != null) {
                    conn.Execute("UPDATE processos SET numero=@n, is_antigo=@ia, paciente=@p, juiz=@j, genitor_rep_nome=@gn, genitor_rep_tipo=@gt, classificacao=@c WHERE id=@id",
                        new { n=Numero, ia=IsAntigo?1:0, p=Paciente, j=Juiz, gn=GenitorNome, gt=GenitorTipo, c=Classificacao, id=pid }, trans);
                    conn.Execute("DELETE FROM reus WHERE processo_id=@id", new { id=pid }, trans);
                    conn.Execute("DELETE FROM itens_saude WHERE processo_id=@id", new { id=pid }, trans);
                } else {
                    var (prz, _) = ProcessLogic.CalculateDueDates(DateTime.Now.ToString("dd/MM/yyyy"));
                    conn.Execute("INSERT INTO processos (id, numero, is_antigo, paciente, juiz, genitor_rep_nome, genitor_rep_tipo, classificacao, status_fase, ultima_atualizacao, cache_proximo_prazo) VALUES (@id, @n, @ia, @p, @j, @gn, @gt, @c, 'Conhecimento', @ua, @cp)",
                        new { id=pid, n=Numero, ia=IsAntigo?1:0, p=Paciente, j=Juiz, gn=GenitorNome, gt=GenitorTipo, c=Classificacao, ua=DateTime.Now.ToString("dd/MM/yyyy"), cp=prz }, trans);
                    
                    conn.Execute("INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, alteracoes_texto, proximo_prazo_padrao) VALUES (@id, @pid, @dh, 'Cadastro', 'System', 'Processo Criado', @prz)",
                        new { id=Guid.NewGuid().ToString(), pid, dh=DateTime.Now.ToString("s"), prz }, trans);
                }

                foreach(var r in Reus) if (!string.IsNullOrWhiteSpace(r.Nome))
                    conn.Execute("INSERT INTO reus (id, processo_id, nome) VALUES (@id, @pid, @n)", new { id=Guid.NewGuid().ToString(), pid, n=r.Nome }, trans);

                if (Classificacao == "Saúde") {
                    foreach(var i in ItensSaude) if (!string.IsNullOrWhiteSpace(i.Nome)) {
                        conn.Execute("INSERT INTO itens_saude (id, processo_id, tipo, nome, qtd, frequencia, local, data_prescricao) VALUES (@id, @pid, @t, @n, @q, @f, @l, @d)",
                            new { id=Guid.NewGuid().ToString(), pid, t=i.Tipo, n=i.Nome, q=i.Qtd, f=i.Frequencia, l=i.Local, d=i.Data }, trans);
                        conn.Execute("INSERT OR IGNORE INTO sugestoes_tratamento (nome, tipo) VALUES (@n, @t)", new { n=i.Nome, t=i.Tipo }, trans);
                    }
                }

                trans.Commit();
                _db.PerformBackup();
                MessageBox.Show("Salvo com sucesso!");
                _closeAction();
            } catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }

        [RelayCommand]
        public void Excluir() {
            if (MessageBox.Show("Tem certeza que deseja excluir tudo?", "Perigo", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                using var conn = _db.GetConnection();
                conn.Execute("DELETE FROM processos WHERE id=@id", new { id = _processoIdEdit });
                _closeAction();
            }
        }
    }
}
