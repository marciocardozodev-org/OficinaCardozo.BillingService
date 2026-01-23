using OficinaCardozo.Application.Interfaces;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.Infrastructure.Services
{
    public class HealthService : IHealthService
    {
        private readonly OficinaDbContext _context;
        public HealthService(OficinaDbContext context)
        {
            _context = context;
        }

        public bool IsDatabaseHealthy()
        {
            try
            {
                // Executa uma query simples para testar conexão
                return _context.Database.CanConnect();
            }
            catch
            {
                return false;
            }
        }
    }
}
