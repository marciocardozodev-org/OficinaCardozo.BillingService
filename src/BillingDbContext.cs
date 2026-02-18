using Microsoft.EntityFrameworkCore;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;

namespace OFICINACARDOZO.BILLINGSERVICE
{
    public class BillingDbContext : DbContext
    {
        public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

        public DbSet<Orcamento> Orcamentos { get; set; }
        public DbSet<Pagamento> Pagamentos { get; set; }
        public DbSet<AtualizacaoStatusOs> AtualizacoesStatusOs { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<InboxMessage> InboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configurar OutboxMessage
            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.ToTable("outbox_message");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
                entity.Property(e => e.Payload).HasColumnName("payload").IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.Published).HasColumnName("published");
                entity.Property(e => e.PublishedAt).HasColumnName("published_at");
                entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
                entity.Property(e => e.CausationId).HasColumnName("causation_id");
            });

            // Configurar InboxMessage
            modelBuilder.Entity<InboxMessage>(entity =>
            {
                entity.ToTable("inbox_message");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
                entity.Property(e => e.Payload).HasColumnName("payload").IsRequired();
                entity.Property(e => e.ReceivedAt).HasColumnName("received_at");
                entity.Property(e => e.ProviderEventId).HasColumnName("provider_event_id");
                entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
                entity.Property(e => e.CausationId).HasColumnName("causation_id");
            });
        }
    }
}
