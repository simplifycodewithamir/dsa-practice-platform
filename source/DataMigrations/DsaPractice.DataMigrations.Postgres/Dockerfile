# One-shot migrator: applies pending EF Core migrations, then exits.
# Needs the SDK (not just the runtime) because `dotnet ef` is a design-time CLI tool.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /source
COPY . .
RUN dotnet tool restore
RUN dotnet restore "source/DataMigrations/DsaPractice.DataMigrations.Postgres/DsaPractice.DataMigrations.Postgres.csproj"
ENTRYPOINT ["dotnet", "ef", "database", "update", \
    "--project", "source/DataMigrations/DsaPractice.DataMigrations.Postgres", \
    "--startup-project", "source/DataMigrations/DsaPractice.DataMigrations.Postgres"]
