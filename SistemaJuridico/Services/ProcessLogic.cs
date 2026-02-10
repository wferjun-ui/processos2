using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace SistemaJuridico.Services
{
    public static class ProcessLogic
    {
        // Python: format_cnj_realtime
        public static string FormatCNJ(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string v = Regex.Replace(value, @"\D", "");
            if (v.Length > 20) v = v.Substring(0, 20);

            if (v.Length > 16) return $"{v.Substring(0, 7)}-{v.Substring(7, 2)}.{v.Substring(9, 4)}.{v.Substring(13, 1)}.{v.Substring(14, 2)}.{v.Substring(16)}";
            if (v.Length > 14) return $"{v.Substring(0, 7)}-{v.Substring(7, 2)}.{v.Substring(9, 4)}.{v.Substring(13, 1)}.{v.Substring(14)}";
            if (v.Length > 13) return $"{v.Substring(0, 7)}-{v.Substring(7, 2)}.{v.Substring(9, 4)}.{v.Substring(13)}";
            if (v.Length > 9) return $"{v.Substring(0, 7)}-{v.Substring(7, 2)}.{v.Substring(9)}";
            if (v.Length > 7) return $"{v.Substring(0, 7)}-{v.Substring(7)}";
            return v;
        }

        // Python: calculate_due_dates
        public static (string proximoPrazo, string dataNotificacao) CalculateDueDates(string? dataBaseStr, string? manualDateStr = null)
        {
            // Se o usuário digitou uma data manual válida, usa ela como base para cálculo reverso
            if (!string.IsNullOrWhiteSpace(manualDateStr) && DateTime.TryParseExact(manualDateStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime manualDate))
            {
                return (manualDate.ToString("dd/MM/yyyy"), manualDate.AddDays(-7).ToString("dd/MM/yyyy"));
            }

            DateTime baseDate = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(dataBaseStr) && DateTime.TryParseExact(dataBaseStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime parsed))
            {
                baseDate = parsed;
            }

            // Regra Python: base + 14 dias. Se cair FDS, joga para segunda.
            DateTime futureDate = baseDate.AddDays(14);
            
            // Lógica Python: days_ahead = (7 - future_date.weekday()) % 7.
            // Se for Sábado (6) -> 7-6 = 1 dia (Domingo) -> +1 = Segunda. 
            // Se for Domingo (0) -> No Python Domingo é 6. Vamos simplificar garantindo dia útil:
            while (futureDate.DayOfWeek == DayOfWeek.Saturday || futureDate.DayOfWeek == DayOfWeek.Sunday)
            {
                futureDate = futureDate.AddDays(1);
            }

            DateTime proximoPrazo = futureDate;
            DateTime notificacao = proximoPrazo.AddDays(-7);

            return (proximoPrazo.ToString("dd/MM/yyyy"), notificacao.ToString("dd/MM/yyyy"));
        }

        // Python: check_prazo_status
        public static (string texto, SolidColorBrush cor) CheckPrazoStatus(string proximoPrazoStr)
        {
            if (string.IsNullOrEmpty(proximoPrazoStr)) 
                return ("Sem Prazo", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")));

            if (DateTime.TryParseExact(proximoPrazoStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime prazo))
            {
                int diff = (prazo.Date - DateTime.Now.Date).Days;

                if (diff < 0) return ("ATRASADO", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")));
                if (diff == 0) return ("VENCE HOJE", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")));
                if (diff <= 7) return ($"Vence em {diff} dias", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")));
                return ("No Prazo", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")));
            }
            return ("Data Inválida", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")));
        }

        // Python: parse_currency / format_money_input
        public static decimal ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            // Remove tudo que não é dígito ou vírgula
            string clean = Regex.Replace(value, @"[^\d,]", "");
            if (decimal.TryParse(clean, NumberStyles.Number, new CultureInfo("pt-BR"), out decimal result)) 
                return result;
            return 0;
        }
    }
}
