using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace SistemaJuridico.Services
{
    public static class ProcessLogic
    {
        // --- LÓGICA DE FORMATAÇÃO DO CNJ (Tempo Real) ---
        public static string FormatCNJ(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            // Remove tudo que não é número
            string v = Regex.Replace(value, @"\D", "");
            
            // Limita a 20 caracteres
            if (v.Length > 20) v = v.Substring(0, 20);

            // Aplica a máscara progressivamente: 0000000-00.0000.0.00.0000
            if (v.Length > 16)
                return $"{v.Substring(0, 7)}-{v.Substring(7, 2)}.{v.Substring(9, 4)}.{v.Substring(13, 1)}.{v.Substring(14, 2)}.{v.Substring(16)}";
            if (v.Length > 14)
                return $"{v.Substring(0, 7)}-{v.Substring(7, 2)}.{v.Substring(9, 4)}.{v.Substring(13, 1)}.{v.Substring(14)}";
            if (v.Length > 13)
                return $"{v.Substring(0, 7)}-{v.Substring(7, 2)}.{v.Substring(9, 4)}.{v.Substring(13)}";
            if (v.Length > 9)
                return $"{v.Substring(0, 7)}-{v.Substring(7, 2)}.{v.Substring(9)}";
            if (v.Length > 7)
                return $"{v.Substring(0, 7)}-{v.Substring(7)}";
            
            return v;
        }

        // --- LÓGICA DE CÁLCULO DE PRAZOS (14 Dias + Regra de Segunda-feira) ---
        // Retorna: (Próximo Prazo, Data de Notificação)
        public static (string proximoPrazo, string dataNotificacao) CalculateDueDates(string? dataBaseStr)
        {
            DateTime baseDate = DateTime.Now;
            
            // Se foi passada uma data manual válida, usa ela. Senão, usa hoje.
            if (!string.IsNullOrWhiteSpace(dataBaseStr) && DateTime.TryParseExact(dataBaseStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime parsed))
            {
                baseDate = parsed;
            }

            // Lógica Python traduzida: future_date = base + 14 dias
            DateTime futureDate = baseDate.AddDays(14);

            // Ajuste para garantir que caia no início da semana (Lógica do Python: (7 - weekday) % 7)
            // DayOfWeek: Sunday=0, Monday=1... Saturday=6
            int daysAhead = (7 - (int)futureDate.DayOfWeek) % 7;
            if (daysAhead == 0) daysAhead = 7; // Se já for o dia certo, joga pra próxima semana (comportamento do Python script original)

            DateTime proximoPrazo = futureDate.AddDays(daysAhead);
            DateTime notificacao = proximoPrazo.AddDays(-7);

            return (proximoPrazo.ToString("dd/MM/yyyy"), notificacao.ToString("dd/MM/yyyy"));
        }

        // --- LÓGICA DE CORES DO DASHBOARD ---
        public static (string texto, SolidColorBrush cor) CheckPrazoStatus(string proximoPrazoStr)
        {
            if (string.IsNullOrEmpty(proximoPrazoStr)) 
                return ("Sem Prazo", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"))); // Cinza

            if (DateTime.TryParseExact(proximoPrazoStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime prazo))
            {
                int diff = (prazo.Date - DateTime.Now.Date).Days;

                if (diff < 0) 
                    return ("ATRASADO", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"))); // Vermelho
                else if (diff == 0) 
                    return ("VENCE HOJE", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))); // Laranja/Amarelo
                else if (diff <= 7) 
                    return ($"Vence em {diff} dias", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))); // Laranja
                else 
                    return ("No Prazo", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))); // Verde
            }

            return ("Data Inválida", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")));
        }
        
        // Helper para limpar moeda
        public static decimal ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            string clean = Regex.Replace(value, @"[^\d,]", "").Replace(".", ""); // Mantém números e vírgula
            if (decimal.TryParse(clean, out decimal result)) return result;
            return 0;
        }
    }
}
