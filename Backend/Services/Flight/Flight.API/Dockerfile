FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Services/Flight/Flight.API/Flight.API.csproj", "Services/Flight/Flight.API/"]
COPY ["Services/Flight/Flight.Domain/Flight.Domain.csproj", "Services/Flight/Flight.Domain/"]
COPY ["Services/Flight/Flight.Application/Flight.Application.csproj", "Services/Flight/Flight.Application/"]
COPY ["Services/Flight/Flight.Infrastructure/Flight.Infrastructure.csproj", "Services/Flight/Flight.Infrastructure/"]
COPY ["Services/Shared/", "Services/Shared/"]
RUN dotnet restore "Services/Flight/Flight.API/Flight.API.csproj"
COPY Services/Flight/ Services/Flight/
WORKDIR /src/Services/Flight/Flight.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Flight.API.dll"]


