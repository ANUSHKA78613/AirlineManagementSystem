FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Services/Dealer/Dealer.API/Dealer.API.csproj", "Services/Dealer/Dealer.API/"]
COPY ["Services/Dealer/Dealer.Domain/Dealer.Domain.csproj", "Services/Dealer/Dealer.Domain/"]
COPY ["Services/Dealer/Dealer.Application/Dealer.Application.csproj", "Services/Dealer/Dealer.Application/"]
COPY ["Services/Dealer/Dealer.Infrastructure/Dealer.Infrastructure.csproj", "Services/Dealer/Dealer.Infrastructure/"]
COPY ["Services/Shared/", "Services/Shared/"]
RUN dotnet restore "Services/Dealer/Dealer.API/Dealer.API.csproj"
COPY Services/Dealer/ Services/Dealer/
WORKDIR /src/Services/Dealer/Dealer.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Dealer.API.dll"]


