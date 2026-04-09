FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV PATH="${PATH}:/root/.dotnet/tools"
COPY . .
RUN dotnet tool install --global dotnet-ef --version 8.*
RUN dotnet restore ./Tripflow.Api.csproj
RUN dotnet publish ./Tripflow.Api.csproj -c Release -o /app/publish /p:UseAppHost=false
RUN ConnectionStrings__TripflowDb="Host=localhost;Port=5432;Database=tripflow_bundle;Username=tripflow;Password=tripflow" \
    JWT_ISSUER="tripflow-bundle" \
    JWT_AUDIENCE="tripflow-bundle" \
    JWT_SECRET="tripflow-bundle-secret-key-tripflow-bundle-secret-key" \
    dotnet ef migrations bundle \
    --project ./Tripflow.Api.csproj \
    --startup-project ./Tripflow.Api.csproj \
    --configuration Release \
    --output /app/efbundle \
    --force

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/efbundle ./efbundle
RUN chmod +x ./efbundle
CMD ["sh", "-c", "dotnet Tripflow.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
