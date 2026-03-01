using MediatR;
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
    private readonly IMediator? _mediator;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IMediator? mediator = null)
        : base(options)
    {
        _mediator = mediator;
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

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyEmulatedRowVersions();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await DispatchDomainEventsAsync(cancellationToken);
        return result;
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

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var domainEntities = ChangeTracker.Entries<BaseEntity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        if (domainEntities.Count == 0)
        {
            return;
        }

        var events = domainEntities.SelectMany(entity => entity.DomainEvents).ToList();
        domainEntities.ForEach(entity => entity.ClearDomainEvents());

        if (_mediator is null)
        {
            return;
        }

        foreach (var domainEvent in events)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }

    private void ConfigureTicket(ModelBuilder builder)
    {
        EntityTypeBuilder<TicketEntity> ticket = builder.Entity<TicketEntity>();
        ticket.ToTable("Tickets");
        ticket.HasKey(x => x.Id);
        ticket.Property(x => x.Title).IsRequired().HasMaxLength(200);
        ticket.Property(x => x.Description).IsRequired().HasMaxLength(5000);
        ticket.Property(x => x.TitleNormalized).IsRequired().HasMaxLength(200);
        ticket.Property(x => x.DescriptionNormalized).IsRequired().HasMaxLength(5000);
        ticket.Property(x => x.RequesterNameNormalized).HasMaxLength(200);
        ticket.Property(x => x.RequesterEmailNormalized).HasMaxLength(200);
        ticket.Property(x => x.RecipientNameNormalized).HasMaxLength(200);
        ticket.Property(x => x.RecipientEmailNormalized).HasMaxLength(200);
        ticket.Property(x => x.ReferenceCode).HasMaxLength(100);
        ticket.Property(x => x.ReferenceCodeNormalized).HasMaxLength(100);
        ticket.HasIndex(x => x.CreatedAtUtc);
        ticket.HasIndex(x => x.CategoryId);
        ticket.HasIndex(x => x.Status);
        ticket.HasIndex(x => x.Priority);
        ticket.HasIndex(x => x.TitleNormalized);

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
