using Flight.Application.DTOs;
using MediatR;

namespace Flight.Application.CQRS.Queries
{
    public class SearchFlightsQuery : IRequest<List<object>>
    {
        public SearchFlightDto Dto { get; }

        public SearchFlightsQuery(SearchFlightDto dto)
        {
            Dto = dto;
        }
    }
}
