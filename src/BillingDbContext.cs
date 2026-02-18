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
                entity.Property(e => e.AggregateId).HasColumnName("aggregate_id").IsRequired();
                entity.Property(e => e.AggregateType).HasColumnName("aggregate_type").IsRequired();
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
                entity.Property(e => e.Processed).HasColumnName("processed");
                entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            });

            // Configurar Orcamento
            modelBuilder.Entity<Orcamento>(entity =>
            {
                entity.ToTable("orcamento");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.OsId).HasColumnName("os_id").IsRequired();
                entity.Property(e => e.Valor).HasColumnName("valor").IsRequired();
                entity.Property(e => e.EmailCliente).HasColumnName("email_cliente").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").IsRequired();
                entity.Property(e => e.CorrelationId).HasColumnName("correlation_id").IsRequired();
                entity.Property(e => e.CausationId).HasColumnName("causation_id").IsRequired();
                entity.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
                entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
                entity.HasIndex(e => e.OsId);
                entity.HasIndex(e => e.CorrelationId);
            });

            // Configurar Pagamento
            modelBuilder.Entity<Pagamento>(entity =>
            {
                entity.ToTable("pagamento");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.OsId).HasColumnName("os_id").IsRequired();
                entity.Property(e => e.OrcamentoId).HasColumnName("orcamento_id").IsRequired();
                entity.Property(e => e.Valor).HasColumnName("valor").IsRequired();
                entity.Property(e => e.Metodo).HasColumnName("metodo").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").IsRequired();
                entity.Property(e => e.ProviderPaymentId).HasColumnName("provider_payment_id");
                entity.Property(e => e.CorrelationId).HasColumnName("correlation_id").IsRequired();
                entity.Property(e => e.CausationId).HasColumnName("causation_id").IsRequired();
                entity.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
                entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
                entity.HasIndex(e => e.OsId);
                entity.HasIndex(e => e.OrcamentoId);
                entity.HasIndex(e => e.CorrelationId);
                entity.HasOne<Orcamento>()
                    .WithMany()
                    .HasForeignKey(e => e.OrcamentoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configurar AtualizacaoStatusOs
            modelBuilder.Entity<AtualizacaoStatusOs>(entity =>
            {
                entity.ToTable("atualizacao_status_os");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.OsId).HasColumnName("os_id").IsRequired();
                entity.Property(e => e.NovoStatus).HasColumnName("novo_status").IsRequired();
                entity.Property(e => e.EventType).HasColumnName("event_type");
                entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
                entity.Property(e => e.CausationId).HasColumnName("causation_id");
                entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
                entity.HasIndex(e => e.OsId);
                entity.HasIndex(e => e.CorrelationId);
            });
        }
    }
}
