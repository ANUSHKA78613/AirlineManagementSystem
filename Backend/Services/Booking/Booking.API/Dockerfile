FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Services/Booking/Booking.API/Booking.API.csproj", "Services/Booking/Booking.API/"]
COPY ["Services/Booking/Booking.Domain/Booking.Domain.csproj", "Services/Booking/Booking.Domain/"]
COPY ["Services/Booking/Booking.Application/Booking.Application.csproj", "Services/Booking/Booking.Application/"]
COPY ["Services/Booking/Booking.Infrastructure/Booking.Infrastructure.csproj", "Services/Booking/Booking.Infrastructure/"]
COPY ["Services/Shared/EventBus/EventBus.csproj", "Services/Shared/EventBus/"]
RUN dotnet restore "Services/Booking/Booking.API/Booking.API.csproj"
COPY Services/Booking/ Services/Booking/
COPY Services/Shared/ Services/Shared/
WORKDIR /src/Services/Booking/Booking.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Booking.API.dll"]


