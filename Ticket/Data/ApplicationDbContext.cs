using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticket.Domain.Entities;
using Ticket.Domain.ValueObjects;
using CategoryEntity = Ticket.Domain.Entities.Category;
using TicketEntity = Ticket.Domain.Entities.Ticket;
using TicketHistoryEntity = Ticket.Domain.Entities.TicketHistory;

namespace Ticket.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<TicketEntity> Tickets => Set<TicketEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<TicketHistoryEntity> TicketHistories => Set<TicketHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureTicket(modelBuilder);
        ConfigureCategory(modelBuilder);
        ConfigureTicketHistory(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyEmulatedRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyEmulatedRowVersions()
    {
        var provider = Database.ProviderName ?? string.Empty;
        var requiresEmulation = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                                provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

        if (!requiresEmulation)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Property(e => e.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
            }
        }
    }

    private void ConfigureTicket(ModelBuilder builder)
    {
        EntityTypeBuilder<TicketEntity> ticket = builder.Entity<TicketEntity>();
        ticket.ToTable("Tickets");
        ticket.HasKey(x => x.Id);
        ticket.Property(x => x.Title).IsRequired().HasMaxLength(200);
        ticket.Property(x => x.Description).IsRequired().HasMaxLength(5000);
        ticket.HasIndex(x => x.CreatedAtUtc);
        ticket.HasIndex(x => x.CategoryId);
        ticket.HasIndex(x => x.Status);
        ticket.HasIndex(x => x.Priority);
        var rowVersionProperty = ticket.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .IsRequired();

        var provider = Database.ProviderName ?? string.Empty;
        var requiresEmulation = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                                provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

        if (!requiresEmulation)
        {
            rowVersionProperty.IsRowVersion();
        }
        else
        {
            rowVersionProperty.ValueGeneratedNever();
        }
        ticket.HasOne(x => x.Category)
            .WithMany(c => c.Tickets)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        ticket.HasMany(x => x.History)
            .WithOne(h => h.Ticket!)
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        ticket.HasQueryFilter(x => !x.IsDeleted);

        ticket.OwnsOne(x => x.Requester, (OwnedNavigationBuilder<TicketEntity, TicketContactInfo> owned) =>
        {
            owned.Property(o => o.Name).HasColumnName("RequesterName").HasMaxLength(200);
            owned.Property(o => o.Email).HasColumnName("RequesterEmail").HasMaxLength(200);
            owned.Property(o => o.Phone).HasColumnName("RequesterPhone").HasMaxLength(50);
        });

        ticket.OwnsOne(x => x.Recipient, (OwnedNavigationBuilder<TicketEntity, TicketContactInfo> owned) =>
        {
            owned.Property(o => o.Name).HasColumnName("RecipientName").HasMaxLength(200);
            owned.Property(o => o.Email).HasColumnName("RecipientEmail").HasMaxLength(200);
            owned.Property(o => o.Phone).HasColumnName("RecipientPhone").HasMaxLength(50);
        });

        ticket.OwnsOne(x => x.Metadata, (OwnedNavigationBuilder<TicketEntity, TicketMetadata> owned) =>
        {
            owned.Property(o => o.IsExternal).HasColumnName("IsExternal");
            owned.Property(o => o.RequiresFollowUp).HasColumnName("RequiresFollowUp");
        });
    }

    private static void ConfigureCategory(ModelBuilder builder)
    {
        var category = builder.Entity<CategoryEntity>();
        category.ToTable("Categories");
        category.HasKey(x => x.Id);
        category.Property(x => x.Name).IsRequired().HasMaxLength(150);
        category.Property(x => x.Description).HasMaxLength(500);
        category.HasIndex(x => x.IsActive);
        category.HasIndex(x => x.Name).IsUnique();
    }

    private static void ConfigureTicketHistory(ModelBuilder builder)
    {
        var history = builder.Entity<TicketHistoryEntity>();
        history.ToTable("TicketHistories");
        history.HasKey(x => x.Id);
        history.Property(x => x.Action).IsRequired().HasMaxLength(200);
        history.Property(x => x.Note).HasMaxLength(2000);
        history.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        history.HasIndex(x => x.OccurredAtUtc);
        history.HasQueryFilter(x => !x.Ticket!.IsDeleted);
    }
}
