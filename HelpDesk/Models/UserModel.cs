using System.Net.Sockets;

namespace HelpDesk.Models
{
    public class UserModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "Requester"; // Requester, Agent, Manager

        public ICollection<TicketModel> RequestedTickets { get; set; } = new List<TicketModel>();
        public ICollection<TicketModel> AssignedTickets { get; set; } = new List<TicketModel>();
    }

}
