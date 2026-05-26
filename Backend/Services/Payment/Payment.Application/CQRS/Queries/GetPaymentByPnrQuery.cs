using MediatR;

namespace Payment.Application.CQRS.Queries
{
    public class GetPaymentByPnrQuery : IRequest<List<Payment.Domain.Entities.Payment>>
    {
        public string PNR { get; }

        public GetPaymentByPnrQuery(string pnr)
        {
            PNR = pnr;
        }
    }
}
