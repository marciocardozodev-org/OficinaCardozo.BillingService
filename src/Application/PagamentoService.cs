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

        public Pagamento RegistrarPagamento(int ordemServicoId, decimal valor, string metodo)
        {
            var pagamento = new Pagamento
            {
                OrdemServicoId = ordemServicoId,
                Valor = valor,
                Metodo = metodo,
                Status = StatusPagamento.Confirmado,
                CriadoEm = DateTime.UtcNow
            };
            _context.Pagamentos.Add(pagamento);
            _context.SaveChanges();
            return pagamento;
        }

        public Pagamento? ObterPagamento(int pagamentoId)
        {
            return _context.Pagamentos.FirstOrDefault(p => p.Id == pagamentoId);
        }
    }
}