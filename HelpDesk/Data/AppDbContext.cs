using HelpDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<UserModel> Users => Set<UserModel>();
        public DbSet<CategoryModel> Categories => Set<CategoryModel>();
        public DbSet<TicketModel> Tickets => Set<TicketModel>();
        public DbSet<TicketCommentModel> TicketComments => Set<TicketCommentModel>();
        public DbSet<AttachmentModel> Attachments => Set<AttachmentModel>();
        public DbSet<TicketActionModel> TicketActions => Set<TicketActionModel>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<UserModel>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Email).HasMaxLength(180).IsRequired();
                e.Property(x => x.Role).HasMaxLength(30).IsRequired();

                e.HasIndex(x => x.Email).IsUnique();

            });

            b.Entity<CategoryModel>(e =>
            {
                e.ToTable("Categories");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(180).IsRequired();

                e.HasIndex(x => x.Name).IsUnique();


                e.HasOne(x => x.Parent)
                 .WithMany(x => x.Children)
                 .HasForeignKey(x => x.ParentId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<TicketModel>(e =>
            {
                e.ToTable("Tickets");
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).HasMaxLength(180).IsRequired();
                e.Property(x => x.Status).HasMaxLength(30).IsRequired();
                e.Property(x => x.PriorityLevel).HasMaxLength(20).IsRequired();

                e.HasOne(x => x.Requester)
                 .WithMany(x => x.RequestedTickets)
                 .HasForeignKey(x => x.RequesterId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Assignee)
                 .WithMany(x => x.AssignedTickets)
                 .HasForeignKey(x => x.AssigneeId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Category)
                 .WithMany()
                 .HasForeignKey(x => x.CategoryId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            b.Entity<TicketCommentModel>(e =>
            {
                e.ToTable("TicketComments");
                e.HasKey(x => x.Id);
                e.Property(x => x.Visibility).HasMaxLength(16).IsRequired();
                e.Property(x => x.Message).HasMaxLength(4000).IsRequired();

                e.HasOne(x => x.Ticket)
                 .WithMany(x => x.Comments)
                 .HasForeignKey(x => x.TicketId);

                e.HasOne(x => x.Author)
                 .WithMany()
                 .HasForeignKey(x => x.AuthorId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            b.Entity<AttachmentModel>(e =>
            {
                e.ToTable("Attachments");
                e.HasKey(x => x.Id);

                e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
                e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();

                // era StoragePath -> agora StorageKey/PublicUrl
                e.Property(x => x.StorageKey).HasMaxLength(1000).IsRequired();
                e.Property(x => x.PublicUrl).HasMaxLength(2000); // opcional

                e.Property(x => x.UploadedAt).IsRequired();

                e.HasOne<TicketModel>()               // <-- sem x => x.Ticket
                 .WithMany(t => t.Attachments)
                 .HasForeignKey(x => x.TicketId);

                e.HasOne(x => x.UploadedBy)         
                 .WithMany()
                 .HasForeignKey(x => x.UploadedById)
                 .OnDelete(DeleteBehavior.SetNull);   

            });

            b.Entity<TicketActionModel>(e =>
            {
                e.ToTable("TicketActions");
                e.HasKey(x => x.Id);
                e.Property(x => x.Description).HasMaxLength(600).IsRequired();
                e.Property(x => x.CreatedAt).IsRequired();

                e.HasOne(x => x.Ticket)
                    .WithMany(t => t.Actions)
                    .HasForeignKey(x => x.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);
            });


            b.Entity<UserModel>().HasData(
               new UserModel { Id = 1, Name = "Alice Johnson", Email = "alice.johnson@acme.com", Role = "Requester" },
               new UserModel { Id = 2, Name = "Bob Miller", Email = "bob.miller@acme.com", Role = "Agent" },
               new UserModel { Id = 3, Name = "Clara Thompson", Email = "clara.thompson@acme.com", Role = "Manager" },
               new UserModel { Id = 4, Name = "David Anderson", Email = "david.anderson@acme.com", Role = "Requester" },
               new UserModel { Id = 5, Name = "Emily Carter", Email = "emily.carter@acme.com", Role = "Agent" },
               new UserModel { Id = 6, Name = "Frank Harris", Email = "frank.harris@acme.com", Role = "Manager" },
               new UserModel { Id = 7, Name = "Grace Lewis", Email = "grace.lewis@acme.com", Role = "Requester" },
               new UserModel { Id = 8, Name = "Henry Clark", Email = "henry.clark@acme.com", Role = "Agent" },
               new UserModel { Id = 9, Name = "Isabella Scott", Email = "isabella.scott@acme.com", Role = "Manager" },
               new UserModel { Id = 10, Name = "Jack Wilson", Email = "jack.wilson@acme.com", Role = "Requester" }
            );

            b.Entity<CategoryModel>().HasData(
               new CategoryModel { Id = 1, Name = "Infraestrutura", ParentId = null },
               new CategoryModel { Id = 2, Name = "Aplicações", ParentId = null },
               new CategoryModel { Id = 3, Name = "Redes", ParentId = null },
               new CategoryModel { Id = 4, Name = "Segurança", ParentId = null },
               new CategoryModel { Id = 5, Name = "Suporte", ParentId = null },

               new CategoryModel { Id = 6, Name = "Serviços em Nuvem", ParentId = 1 },
               new CategoryModel { Id = 7, Name = "Bancos de Dados", ParentId = 2 },
               new CategoryModel { Id = 8, Name = "Firewall", ParentId = 4 },
               new CategoryModel { Id = 9, Name = "Central de Ajuda", ParentId = 5 },
               new CategoryModel { Id = 10, Name = "LAN/WAN", ParentId = 3 }
            );
        }
    }
}