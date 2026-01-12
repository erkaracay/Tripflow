FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./Tripflow.Api.csproj
RUN dotnet publish ./Tripflow.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
CMD ["sh", "-c", "dotnet Tripflow.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
