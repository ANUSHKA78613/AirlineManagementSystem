FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Services/ApiGateway/ApiGateway.csproj", "Services/ApiGateway/"]
RUN dotnet restore "Services/ApiGateway/ApiGateway.csproj"
COPY Services/ApiGateway/ Services/ApiGateway/
WORKDIR /src/Services/ApiGateway
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ["Services/ApiGateway/ocelot.Docker.json", "ocelot.json"]
ENTRYPOINT ["dotnet", "ApiGateway.dll"]




