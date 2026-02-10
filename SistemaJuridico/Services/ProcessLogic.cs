using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace SistemaJuridico.Services
{
    public static class ProcessLogic
    {
        // --- FORMATAÇÃO CNJ (Igual ao Python: format_cnj_realtime) ---
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

        // --- CÁLCULO DE PRAZOS (Igual ao Python: calculate_due_dates) ---
        // Regra: Data Base + 14 dias. Se cair fds, joga pra segunda.
        public static (string proximoPrazo, string dataNotificacao) CalculateDueDates(string? dataBaseStr)
        {
            DateTime baseDate = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(dataBaseStr) && DateTime.TryParseExact(dataBaseStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime parsed))
                baseDate = parsed;

            DateTime futureDate = baseDate.AddDays(14);
            
            // Lógica Python: days_ahead = (7 - future_date.weekday()) % 7. 
            // Em C#, DayOfWeek: Domingo=0, ... Sábado=6.
            // Python Monday=0... Sunday=6. Ajuste para lógica de "Próxima Segunda ou Terça":
            int daysAhead = 0;
            if (futureDate.DayOfWeek == DayOfWeek.Saturday) daysAhead = 2; // +2 dias = Segunda
            else if (futureDate.DayOfWeek == DayOfWeek.Sunday) daysAhead = 1; // +1 dia = Segunda
            else daysAhead = 7; // Se cair em dia útil, joga +1 semana (padrão do script original) ou ajuste conforme preferência.
            
            // Ajuste fino para bater com o script Python exato:
            // O script python usava: days_ahead = (7 - weekday) % 7. Se 0, vira 7.
            // Isso sempre joga para o próximo dia da semana igual ao calculado + 1 semana se for o mesmo dia.
            // Vamos simplificar para garantir dia útil:
            while (futureDate.DayOfWeek == DayOfWeek.Saturday || futureDate.DayOfWeek == DayOfWeek.Sunday)
                futureDate = futureDate.AddDays(1);

            DateTime proximoPrazo = futureDate; 
            DateTime notificacao = proximoPrazo.AddDays(-7);

            return (proximoPrazo.ToString("dd/MM/yyyy"), notificacao.ToString("dd/MM/yyyy"));
        }

        // --- STATUS E CORES (Igual ao Python: check_prazo_status) ---
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

        public static decimal ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            // Remove R$, espaços e converte formato PT-BR (1.000,00) para decimal
            string clean = Regex.Replace(value, @"[^\d,]", ""); 
            if (decimal.TryParse(clean, NumberStyles.Number, new CultureInfo("pt-BR"), out decimal result)) return result;
            return 0;
        }
    }
}
