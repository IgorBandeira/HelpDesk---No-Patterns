namespace HelpDesk.Models
{
    public class TicketActionModel
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public TicketModel Ticket { get; set; } = null!;

        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
