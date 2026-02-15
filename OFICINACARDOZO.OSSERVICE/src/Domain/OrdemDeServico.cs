using System;

namespace OFICINACARDOZO.OSSERVICE.Domain
{
    public enum StatusOrdemServico
    {
        Aberta,
        EmAndamento,
        Finalizada,
        Cancelada
    }

    public class OrdemDeServico
    {
        public int Id { get; set; }
        public DateTime DataSolicitacao { get; set; }
        public int IdVeiculo { get; set; }
        public int IdStatus { get; set; }
        public DateTime? DataFinalizacao { get; set; }
        public DateTime? DataEntrega { get; set; }

        public OrdemDeServico() { }

        public OrdemDeServico(DateTime dataSolicitacao, int idVeiculo, int idStatus)
        {
            DataSolicitacao = dataSolicitacao;
            IdVeiculo = idVeiculo;
            IdStatus = idStatus;
        }
    }
}
