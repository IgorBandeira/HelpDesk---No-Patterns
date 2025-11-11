using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Models
{
    public record UserMiniDto(int? Id, string Name);
    public record CategoryMiniDto(int? Id, string Name);
    public record AddCommentDto(string Message, string? Visibility = CommentVisibility.Public);
    public record UpdateCommentMessageDto(string Message);
    public record CommentResponse(int Id, int? AuthorId, string Visibility, string Message, DateTime CreatedAt);
    public record CommentDetailsDto(
        int Id,
        UserMiniDto User,
        string Visibility,
        string Message,
        DateTime CreatedAt
    );
    public record TicketListItemDto(
    int Id,
    string Title,
    string Status,
    string Priority,
    DateTime CreatedAt,
    DateTime? SlaDueAt,
    UserMiniDto Requester,
    UserMiniDto? Assignee,
    CategoryMiniDto Category
);
    public record AttachmentDto(
            int Id,
            int TicketId,
            string FileName,
            string ContentType,
            long SizeBytes,
            string StorageKey,    // chave no S3
            string? PublicUrl,    // url pública ou pré-assinada
            DateTime UploadedAt
    );

    public record CommentDto(int Id, UserMiniDto Author, string Visibility, string Message, DateTime CreatedAt);

    public record TicketActionDto(
        string Description,
        DateTime CreatedAt
    );

    public record TicketDetailsDto(
        int Id,
        string Title,
        string Description,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime? AssignedAt,
        DateTime? ClosedAt,
        DateTime SlaStartAt,
        DateTime? SlaDueAt,
        UserMiniDto? Requester,
        UserMiniDto? Assignee,
        CategoryMiniDto Category,
        List<CommentDto> Comments,
        List<AttachmentListItemDto> Attachments,
        List<TicketActionDto> Actions
    );
    public record CreateTicketDto(string Title, string Description, string Priority, int CategoryId);
    public record TicketResponseDto(
        int Id,
        string Title,
        string Description,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime SlaStartAt,
        DateTime? SlaDueAt,
        int? RequesterId,
        int? CategoryId
    );
    public record AssignRequestDto(int AgentId);
    public record AssignResponseDto(
        int TicketId,
        string Status,
        DateTime AssignedAt,
        int AgentId,
        string AgentName
    );

    public record RequesterRequestDto(int RequesterId);
    public record RequesterResponseDto(
        int TicketId,
        string Status,
        int RequesterId,
        string RequesterName
    );

    public record ChangeStatusRequestDto(string NewStatus);
    public record ChangeStatusResponseDto(
        int TicketId,
        string PreviousStatus,
        string CurrentStatus,
        DateTime? ClosedAt
    );
    public record ReopenRequestDto(string Reason);
    public record ReopenResponseDto(
        int TicketId,
        string PreviousStatus,
        string CurrentStatus,
        DateTime ReopenedAt,
        int AuthorId,
        string Reason
    );
    public record CancelRequestDto(
        string Reason
    );
    public record CancelResponseDto(
        int TicketId,
        string PreviousStatus,
        string CurrentStatus,
        DateTime CanceledAt,
        int AuthorId,
        string Reason
    );

    public record CreateCategoryRequestDto(string Name, int? ParentId);
    public record CategoryItemDto(int Id, string Name, int? ParentId);
    public record CreateUserDto(string Name, string Email, string Role);

    public record UserResponseDto(
    int Id,
    string Name,
    string Email,
    string Role
);

    public record UpdateUserRequestDto(
        string? Name,
        string? Email,
        string? Role
    );

    public class UpdateTicketDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Priority { get; set; }
        public int? CategoryId { get; set; }
    }

    public record UserTicketDto(
        int Id,
        string Title,
        string Status,
        string Priority
    );

    public record UserWithTicketsResponseDto(
        int Id,
        string Name,
        string Email,
        string Role,
        IEnumerable<UserTicketDto> RequestedTickets,
        IEnumerable<UserTicketDto> AssignedTickets
    );
    public record AttachmentResponseDto(
        int Id,
        int TicketId,
        string FileName,
        string ContentType,
        long SizeBytes,
        string StorageKey,
        string? PublicUrl,
        DateTime UploadedAt,
        int? UploadedById
    );

    public record AttachmentListItemDto(
        int Id,
        int TicketId,
        string FileName,
        string ContentType,
        long SizeBytes,
        string StorageKey,
        string? PublicUrl,
        DateTime UploadedAt,
        UserMiniDto UploadedBy
    );
}

public class AttachmentUploadDto
{
    [Required(ErrorMessage = "O arquivo é obrigatório.")]
    [FromForm(Name = "file")]
    public IFormFile? File { get; set; }
}