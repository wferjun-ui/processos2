using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace SistemaJuridico.Services
{
    public static class ProcessLogic
    {
        [cite_start]// Réplica de format_cnj_realtime [cite: 156]
        public static string FormatCNJ(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string v = Regex.Replace(value, @"\D", "");
            if (v.Length > 20) v = v.Substring(0, 20);
            
            // Máscara dinâmica igual ao Python
            if (v.Length > 16) return $"{v[..7]}-{v.Substring(7, 2)}.{v.Substring(9, 4)}.{v.Substring(13, 1)}.{v.Substring(14, 2)}.{v[16..]}";
            return v; 
        }

        [cite_start]// Réplica exata de calculate_due_dates [cite: 157]
        public static (string proximoPrazo, string dataNotificacao) CalculateDueDates(string? dataBaseStr, string? manualDateStr = null)
        {
            // Prioridade para data manual
            if (!string.IsNullOrWhiteSpace(manualDateStr) && DateTime.TryParseExact(manualDateStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime manual))
            {
                return (manual.ToString("dd/MM/yyyy"), manual.AddDays(-7).ToString("dd/MM/yyyy"));
            }

            DateTime baseDate = DateTime.Now;
            if (DateTime.TryParseExact(dataBaseStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime parsed)) baseDate = parsed;

            DateTime futureDate = baseDate.AddDays(14);
            
            // Lógica Python: days_ahead = (7 - future_date.weekday()) % 7. 
            // Em C# DayOfWeek: Domingo=0, Python Domingo=6. Ajustamos:
            int pythonWeekday = (int)futureDate.DayOfWeek == 0 ? 6 : (int)futureDate.DayOfWeek - 1; 
            int daysAhead = (7 - pythonWeekday) % 7;
            if (daysAhead == 0) daysAhead = 7; // Força pular para a próxima semana se cair na segunda exata da lógica

            DateTime proximoPrazo = futureDate.AddDays(daysAhead);
            DateTime notificacao = proximoPrazo.AddDays(-7);

            return (proximoPrazo.ToString("dd/MM/yyyy"), notificacao.ToString("dd/MM/yyyy"));
        }

        [cite_start]// Réplica de check_prazo_status [cite: 165]
        public static (string texto, SolidColorBrush cor) CheckPrazoStatus(string proximoPrazoStr)
        {
            if (string.IsNullOrEmpty(proximoPrazoStr)) 
                return ("Sem Prazo", new SolidColorBrush(Colors.Gray));

            if (DateTime.TryParseExact(proximoPrazoStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime prazo))
            {
                int diff = (prazo.Date - DateTime.Now.Date).Days;
                if (diff < 0) return ("ATRASADO", new SolidColorBrush(Colors.Red)); // Python Theme.DANGER
                if (diff == 0) return ("VENCE HOJE", new SolidColorBrush(Colors.Orange)); // Python Theme.WARNING
                if (diff <= 7) return ($"Vence em {diff} dias", new SolidColorBrush(Colors.Orange));
                return ("No Prazo", new SolidColorBrush(Colors.Green)); // Python Theme.SUCCESS
            }
            return ("Data Inválida", new SolidColorBrush(Colors.Gray));
        }

        public static decimal ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            string clean = Regex.Replace(value, @"[^\d,]", ""); // Mantém apenas números e vírgula
            if (decimal.TryParse(clean, NumberStyles.Number, new CultureInfo("pt-BR"), out decimal result)) return result;
            return 0;
        }
    }
}
