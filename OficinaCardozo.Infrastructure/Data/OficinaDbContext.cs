using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Infrastructure.Data
{
    public class OficinaDbContext : DbContext
    {
        public OficinaDbContext(DbContextOptions<OficinaDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Veiculo> Veiculos { get; set; }
        public DbSet<Servico> Servicos { get; set; }
        public DbSet<Peca> Pecas { get; set; }
        public DbSet<OrdemServico> OrdensServico { get; set; }
        public DbSet<OrdemServicoServico> OrdensServicoServicos { get; set; }
        public DbSet<OrdemServicoPeca> OrdensServicoPecas { get; set; }
        public DbSet<Orcamento> Orcamentos { get; set; }
        public DbSet<OrdemServicoStatus> OrdensServicoStatus { get; set; }
        public DbSet<OrcamentoStatus> OrcamentoStatus { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<OrdemServicoServico>()
                .HasKey(oss => new { oss.IdOrdemServico, oss.IdServico });

            modelBuilder.Entity<OrdemServicoServico>()
                .HasOne(oss => oss.OrdemServico)
                .WithMany(os => os.OrdemServicoServicos)
                .HasForeignKey(oss => oss.IdOrdemServico);

            modelBuilder.Entity<OrdemServicoPeca>()
                .HasKey(osp => new { osp.IdOrdemServico, osp.IdPeca });
                     

            var provider = Database.ProviderName;

            if (provider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                #region Data Seeding (ambiente local com SQLite)
                modelBuilder.Entity<OrdemServicoStatus>().HasData(
                    new OrdemServicoStatus { Id = 1, Descricao = "Recebida" },
                    new OrdemServicoStatus { Id = 2, Descricao = "Em diagnostico" },        
                    new OrdemServicoStatus { Id = 3, Descricao = "Aguardando aprovacao" },  
                    new OrdemServicoStatus { Id = 4, Descricao = "Em execucao" },           
                    new OrdemServicoStatus { Id = 5, Descricao = "Finalizada" },
                    new OrdemServicoStatus { Id = 6, Descricao = "Entregue" },
                    new OrdemServicoStatus { Id = 7, Descricao = "Em elaboracao" },         
                    new OrdemServicoStatus { Id = 8, Descricao = "Cancelada" },
                    new OrdemServicoStatus { Id = 9, Descricao = "Devolvida" }
                );

                modelBuilder.Entity<OrcamentoStatus>().HasData(
                    new OrcamentoStatus { Id = 1, Descricao = "Criado" },
                    new OrcamentoStatus { Id = 2, Descricao = "Pendente aprovacao" },  
                    new OrcamentoStatus { Id = 3, Descricao = "Aprovado" },
                    new OrcamentoStatus { Id = 4, Descricao = "Rejeitado" },
                    new OrcamentoStatus { Id = 5, Descricao = "Em elaboracao" }        
                );
                #endregion

                foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                {
                    var properties = entityType.GetProperties().Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?));
                    foreach (var property in properties)
                    {
                        modelBuilder.Entity(entityType.Name).Property(property.Name)
                            .HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeToBinaryConverter());
                    }
                }
            }
            else if (provider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // Tipos explícitos para colunas decimais principais no Postgres
                modelBuilder.Entity<Servico>()
                    .Property(s => s.Preco)
                    .HasColumnType("numeric(18,2)");

                modelBuilder.Entity<Peca>()
                    .Property(p => p.Preco)
                    .HasColumnType("numeric(18,2)");

                modelBuilder.Entity<OrdemServicoServico>()
                    .Property(oss => oss.ValorAplicado)
                    .HasColumnType("numeric(18,2)");

                modelBuilder.Entity<OrdemServicoPeca>()
                    .Property(osp => osp.ValorUnitario)
                    .HasColumnType("numeric(18,2)");
                // Não aplicar conversão para datas, deixar EF mapear DateTime como TIMESTAMP
            }
            else
            {
                modelBuilder.Entity<Servico>()
                    .Property(s => s.Preco)
                    .HasColumnType("decimal(18,2)");

                modelBuilder.Entity<Peca>()
                    .Property(p => p.Preco)
                    .HasColumnType("decimal(18,2)");

                modelBuilder.Entity<OrdemServicoServico>()
                    .Property(oss => oss.ValorAplicado)
                    .HasColumnType("decimal(18,2)");

                modelBuilder.Entity<OrdemServicoPeca>()
                    .Property(osp => osp.ValorUnitario)
                    .HasColumnType("decimal(18,2)");

                // Configurações para o tipo de coluna de datas.
                foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                {
                    var properties = entityType.GetProperties().Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?));
                    foreach (var property in properties)
                    {
                        modelBuilder.Entity(entityType.Name).Property(property.Name).HasColumnType("datetime2");
                    }
                }
            }
        }
    }
}