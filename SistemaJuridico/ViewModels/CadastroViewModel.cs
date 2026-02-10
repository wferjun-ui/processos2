using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace SistemaJuridico.ViewModels
{
    public partial class ReuDto : ObservableObject { [ObservableProperty] private string _nome = ""; }
    public partial class ItemDto : ObservableObject 
    { 
        [ObservableProperty] private string _tipo = "Medicamento"; 
        [ObservableProperty] private string _nome = "";
        [ObservableProperty] private string _qtd = "";
        [ObservableProperty] private string _frequencia = "Mensal";
        [ObservableProperty] private string _local = "Clínica";
        [ObservableProperty] private string _data = "";
    }

    public partial class CadastroViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly Action _closeAction;
        
        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private bool _isAntigo;
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _classificacao = "Cível";
        
        // CAMPOS QUE ESTAVAM FALTANDO NO INSERT:
        [ObservableProperty] private string _genitorNome = "";
        [ObservableProperty] private string _genitorTipo = "Genitor";
        
        [ObservableProperty] private Visibility _saudeVisibility = Visibility.Collapsed;

        public ObservableCollection<ReuDto> Reus { get; set; } = new();
        public ObservableCollection<ItemDto> ItensSaude { get; set; } = new();

        public CadastroViewModel(Action closeAction)
        {
            _db = App.DB!;
            _closeAction = closeAction;
            Reus.Add(new ReuDto());
        }

        partial void OnClassificacaoChanged(string value) => SaudeVisibility = value == "Saúde" ? Visibility.Visible : Visibility.Collapsed;

        partial void OnNumeroChanged(string value)
        {
            if (IsAntigo) return;
            var fmt = ProcessLogic.FormatCNJ(value);
            if (fmt != value) Numero = fmt;
        }

        [RelayCommand] public void AddReu() => Reus.Add(new ReuDto());
        [RelayCommand] public void RemoveReu(ReuDto item) => Reus.Remove(item);
        [RelayCommand] public void AddSaude() => ItensSaude.Add(new ItemDto());
        [RelayCommand] public void RemoveSaude(ItemDto item) => ItensSaude.Remove(item);

        [RelayCommand]
        public void Salvar()
        {
            if (string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Paciente))
            {
                MessageBox.Show("Preencha o Número e o Paciente.");
                return;
            }

            try
            {
                using var conn = _db.GetConnection();
                conn.Open();
                using var trans = conn.BeginTransaction();

                var procId = Guid.NewGuid().ToString();
                var hoje = DateTime.Now;
                var (dataPrazo, _) = ProcessLogic.CalculateDueDates(hoje.ToString("dd/MM/yyyy"));
                var user = Application.Current.Properties["Usuario"]?.ToString() ?? "Admin";

                // SQL ATUALIZADO COM OS CAMPOS DE GENITOR
                conn.Execute(@"
                    INSERT INTO processos (id, numero, is_antigo, paciente, juiz, genitor_rep_nome, genitor_rep_tipo, classificacao, status_fase, ultima_atualizacao, cache_proximo_prazo)
                    VALUES (@id, @n, @ia, @p, @j, @gn, @gt, @c, 'Conhecimento', @ua, @cp)",
                    new { 
                        id = procId, n = Numero, ia = IsAntigo ? 1 : 0, p = Paciente, j = Juiz, 
                        gn = GenitorNome, gt = GenitorTipo, c = Classificacao, // <--- Aqui estão os dados
                        ua = hoje.ToString("dd/MM/yyyy"), cp = dataPrazo 
                    }, trans);

                foreach (var r in Reus)
                {
                    if (!string.IsNullOrWhiteSpace(r.Nome))
                        conn.Execute("INSERT INTO reus (id, processo_id, nome) VALUES (@id, @pid, @n)",
                            new { id = Guid.NewGuid().ToString(), pid = procId, n = r.Nome }, trans);
                }

                if (Classificacao == "Saúde")
                {
                    foreach (var i in ItensSaude)
                    {
                        if (!string.IsNullOrWhiteSpace(i.Nome))
                            conn.Execute(@"INSERT INTO itens_saude (id, processo_id, tipo, nome, qtd, frequencia, local, data_prescricao)
                                           VALUES (@id, @pid, @t, @n, @q, @f, @l, @d)",
                                new { id = Guid.NewGuid().ToString(), pid = procId, t = i.Tipo, n = i.Nome, q = i.Qtd, f = i.Frequencia, l = i.Local, d = i.Data }, trans);
                    }
                }

                conn.Execute("INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, proximo_prazo_padrao, alteracoes_texto) VALUES (@id, @pid, @dh, 'Cadastro Inicial', @resp, @pp, 'Processo Criado')",
                    new { id = Guid.NewGuid().ToString(), pid = procId, dh = hoje.ToString("s"), resp = user, pp = dataPrazo }, trans);

                trans.Commit();
                _db.PerformBackup();
                MessageBox.Show("Processo cadastrado com sucesso!");
                _closeAction();
            }
            catch (Exception ex) { MessageBox.Show("Erro ao salvar: " + ex.Message); }
        }
    }
}
