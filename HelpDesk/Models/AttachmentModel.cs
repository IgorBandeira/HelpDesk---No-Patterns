namespace HelpDesk.Models
{
    public class AttachmentModel
    {
        public int Id { get; set; }
        public int TicketId { get; set; }

        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long SizeBytes { get; set; }
        public string StorageKey { get; set; } = "";
        public string? PublicUrl { get; set; }                                                   
        public int? UploadedById { get; set; }
        public UserModel? UploadedBy { get; set; } = null!;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }

}
