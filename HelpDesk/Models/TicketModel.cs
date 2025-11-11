namespace HelpDesk.Models

{
    public class TicketModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = TicketStatus.Novo;
        public string PriorityLevel { get; set; } = Priority.Media;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime SlaStartAt { get; set; } = DateTime.Now;
        public DateTime? AssignedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? SlaDueAt { get; set; }

        public int? RequesterId { get; set; }
        public UserModel? Requester { get; set; } = null!;

        public int? AssigneeId { get; set; }
        public UserModel? Assignee { get; set; }

        public int? CategoryId { get; set; }
        public CategoryModel? Category { get; set; } = null!;

        public ICollection<TicketCommentModel> Comments { get; set; } = new List<TicketCommentModel>();
        public ICollection<AttachmentModel> Attachments { get; set; } = new List<AttachmentModel>();
        public ICollection<TicketActionModel> Actions { get; set; } = new List<TicketActionModel>();
    }

}
