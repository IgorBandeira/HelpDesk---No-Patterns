using System.Net.Sockets;

namespace HelpDesk.Models
{
    public class TicketCommentModel
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public TicketModel Ticket { get; set; } = null!;
        public int? AuthorId { get; set; }
        public UserModel? Author { get; set; } = null!;
        public string Visibility { get; set; } = CommentVisibility.Public;
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
