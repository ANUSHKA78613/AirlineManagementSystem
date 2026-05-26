FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Services/Operations/Operations.API/Operations.API.csproj", "Services/Operations/Operations.API/"]
COPY ["Services/Operations/Operations.Domain/Operations.Domain.csproj", "Services/Operations/Operations.Domain/"]
COPY ["Services/Operations/Operations.Application/Operations.Application.csproj", "Services/Operations/Operations.Application/"]
COPY ["Services/Operations/Operations.Infrastructure/Operations.Infrastructure.csproj", "Services/Operations/Operations.Infrastructure/"]
COPY ["Services/Shared/", "Services/Shared/"]
RUN dotnet restore "Services/Operations/Operations.API/Operations.API.csproj"
COPY Services/Operations/ Services/Operations/
WORKDIR /src/Services/Operations/Operations.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Operations.API.dll"]


