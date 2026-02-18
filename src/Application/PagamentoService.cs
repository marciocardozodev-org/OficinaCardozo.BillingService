using OFICINACARDOZO.BILLINGSERVICE.Domain;

namespace OFICINACARDOZO.BILLINGSERVICE.Application
{
    public class PagamentoService
    {
        private readonly BillingDbContext _context;
        public PagamentoService(BillingDbContext context)
        {
            _context = context;
        }

        public Pagamento RegistrarPagamento(
            Guid osId, 
            long orcamentoId,
            decimal valor, 
            string metodo,
            Guid correlationId,
            Guid causationId)
        {
            var pagamento = new Pagamento
            {
                OsId = osId,
                OrcamentoId = orcamentoId,
                Valor = valor,
                Metodo = metodo,
                Status = StatusPagamento.Confirmado,
                CorrelationId = correlationId,
                CausationId = causationId,
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow
            };
            _context.Pagamentos.Add(pagamento);
            _context.SaveChanges();
            return pagamento;
        }

        public Pagamento? ObterPagamento(long pagamentoId)
        {
            return _context.Pagamentos.FirstOrDefault(p => p.Id == pagamentoId);
        }
    }
}