using MediatR;

namespace Flight.Application.CQRS.Queries
{
    public class GetAllFlightsQuery : IRequest<List<object>>
    {
    }
}
