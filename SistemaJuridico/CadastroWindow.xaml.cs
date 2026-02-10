using System.Windows;
using SistemaJuridico.ViewModels;

namespace SistemaJuridico
{
    public partial class CadastroWindow : Window
    {
        public CadastroWindow()
        {
            InitializeComponent();
            // Passa a ação de fechar para o ViewModel
            DataContext = new CadastroViewModel(this.Close);
        }
    }
}
