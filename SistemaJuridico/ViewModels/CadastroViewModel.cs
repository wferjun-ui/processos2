using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using SistemaJuridico.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace SistemaJuridico.ViewModels
{
    // DTOs auxiliares para as listas dinâmicas
    public partial class ReuDto : ObservableObject 
    { 
        [ObservableProperty] private string _nome = ""; 
    }

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
        
        // Campos Principais
        [ObservableProperty] private string _numero = "";
        [ObservableProperty] private bool _isAntigo;
        [ObservableProperty] private string _paciente = "";
        [ObservableProperty] private string _juiz = "";
        [ObservableProperty] private string _classificacao = "Cível"; // Cível, Saúde, Família...
        
        // Genitor / Representante
        [ObservableProperty] private string _genitorNome = "";
        [ObservableProperty] private string _genitorTipo = "Genitor"; // "Genitor" ou "Representante"
        
        // Controle de Visibilidade (Igual ao Python: on_class)
        [ObservableProperty] private Visibility _saudeVisibility = Visibility.Collapsed;

        // Listas Dinâmicas
        public ObservableCollection<ReuDto> Reus { get; set; } = new();
        public ObservableCollection<ItemDto> ItensSaude { get; set; } = new();

        public CadastroViewModel(Action closeAction)
        {
            _db = App.DB!;
            _closeAction = closeAction;
            Reus.Add(new ReuDto()); // Começa com 1 réu vazio
        }

        // Trigger quando muda a classificação (Lógica do Python)
        partial void OnClassificacaoChanged(string value)
        {
            SaudeVisibility = value == "Saúde" ? Visibility.Visible : Visibility.Collapsed;
        }

        // Trigger formatação CNJ
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
            // Validação Básica
            if (string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Paciente))
            {
                MessageBox.Show("Campos obrigatórios: Número do Processo e Paciente.");
                return;
            }

            try
            {
                using var conn = _db.GetConnection();
                conn.Open();
                using var trans = conn.BeginTransaction();

                var procId = Guid.NewGuid().ToString();
                var hoje = DateTime.Now;
                
                // Cálculo inicial de prazo (14 dias)
                var (dataPrazo, _) = ProcessLogic.CalculateDueDates(hoje.ToString("dd/MM/yyyy"));
                
                var user = Application.Current.Properties["Usuario"]?.ToString() ?? "Admin";

                // 1. Salvar Processo
                conn.Execute(@"
                    INSERT INTO processos (id, numero, is_antigo, paciente, juiz, genitor_rep_nome, genitor_rep_tipo, classificacao, status_fase, ultima_atualizacao, cache_proximo_prazo)
                    VALUES (@id, @n, @ia, @p, @j, @gn, @gt, @c, 'Conhecimento', @ua, @cp)",
                    new { 
                        id = procId, n = Numero, ia = IsAntigo ? 1 : 0, p = Paciente, j = Juiz, 
                        gn = GenitorNome, gt = GenitorTipo, c = Classificacao, 
                        ua = hoje.ToString("dd/MM/yyyy"), cp = dataPrazo 
                    }, trans);

                // 2. Salvar Réus
                foreach (var r in Reus)
                {
                    if (!string.IsNullOrWhiteSpace(r.Nome))
                        conn.Execute("INSERT INTO reus (id, processo_id, nome) VALUES (@id, @pid, @n)",
                            new { id = Guid.NewGuid().ToString(), pid = procId, n = r.Nome }, trans);
                }

                // 3. Salvar Itens de Saúde (Se for Saúde)
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

                // 4. Criar Verificação Inicial (Log)
                conn.Execute(@"
                    INSERT INTO verificacoes (id, processo_id, data_hora, status_processo, responsavel, proximo_prazo_padrao, alteracoes_texto) 
                    VALUES (@id, @pid, @dh, 'Cadastro Inicial', @resp, @pp, 'Processo Criado')",
                    new { id = Guid.NewGuid().ToString(), pid = procId, dh = hoje.ToString("s"), resp = user, pp = dataPrazo }, trans);

                trans.Commit();
                _db.PerformBackup();
                
                MessageBox.Show("Processo cadastrado com sucesso!");
                _closeAction();
            }
            catch (Exception ex) 
            { 
                MessageBox.Show($"Erro crítico ao salvar: {ex.Message}"); 
            }
        }
    }
}
