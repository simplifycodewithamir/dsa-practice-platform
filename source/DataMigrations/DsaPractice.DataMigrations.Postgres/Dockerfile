# One-shot migrator: applies pending EF Core migrations, seeds question content, then exits.
# Runs the project as an app (see Program.cs) rather than invoking `dotnet ef`, so the final
# image is the runtime, not the SDK, and no design-time tooling ships to a deployment.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore "source/DataMigrations/DsaPractice.DataMigrations.Postgres/DsaPractice.DataMigrations.Postgres.csproj"
RUN dotnet publish "source/DataMigrations/DsaPractice.DataMigrations.Postgres/DsaPractice.DataMigrations.Postgres.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Authored questions travel with the image so a deploy carries its own content. docker-compose
# mounts the working copy over this for local dev.
COPY content ./content
ENTRYPOINT ["dotnet", "DsaPractice.DataMigrations.Postgres.dll"]
