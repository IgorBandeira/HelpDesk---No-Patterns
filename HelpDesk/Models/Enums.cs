namespace HelpDesk.Models
{
    public static class TicketStatus
    {
        public const string Novo = "Novo";
        public const string EmAnalise = "Em Análise";
        public const string EmAndamento = "Em Andamento";
        public const string Resolvido = "Resolvido";
        public const string Fechado = "Fechado";
        public const string Cancelado = "Cancelado";
    }

    public static class Priority
    {
        public const string Baixa = "Baixa"; 
        public const string Media = "Média";
        public const string Alta = "Alta";
        public const string Critica = "Crítica";
        public static TimeSpan ToSla(string p) => p switch
        {
            Critica => TimeSpan.FromHours(8),
            Alta => TimeSpan.FromHours(24),
            Media => TimeSpan.FromHours(48),
            _ => TimeSpan.FromHours(72),
        };
    }

    public static class CommentVisibility
    {
        public const string Public = "Público";
        public const string Internal = "Interno";
    }

}
