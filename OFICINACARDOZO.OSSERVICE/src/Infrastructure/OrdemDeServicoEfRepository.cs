using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OFICINACARDOZO.OSSERVICE.Domain;
using OFICINACARDOZO.OSSERVICE.InfraDb;

namespace OFICINACARDOZO.OSSERVICE.Infrastructure
{
    public class OrdemDeServicoEfRepository
    {
        private readonly OsDbContext _context;
        public OrdemDeServicoEfRepository(OsDbContext context)
        {
            _context = context;
        }

        public async Task<OrdemDeServico> AddAsync(OrdemDeServico ordem)
        {
            _context.OrdensDeServico.Add(ordem);
            await _context.SaveChangesAsync();
            return ordem;
        }

        public async Task<bool> UpdateStatusAsync(int id, int novoStatus)
        {
            var ordem = await _context.OrdensDeServico.FindAsync(id);
            if (ordem == null) return false;
            ordem.IdStatus = novoStatus;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<OrdemDeServico>> GetByStatusAsync(int status)
        {
            return await _context.OrdensDeServico.Where(o => o.IdStatus == status).ToListAsync();
        }

        public async Task<List<OrdemDeServico>> GetByDateAsync(DateTime data)
        {
            return await _context.OrdensDeServico.Where(o => o.DataSolicitacao.Date == data.Date).ToListAsync();
        }

        public async Task<List<OrdemDeServico>> GetAllAsync()
        {
            return await _context.OrdensDeServico.ToListAsync();
        }

        public async Task<OrdemDeServico> GetByIdAsync(int id)
        {
            return await _context.OrdensDeServico.FindAsync(id);
        }
    }
}
