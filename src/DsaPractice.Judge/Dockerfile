FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/DsaPractice.Judge/DsaPractice.Judge.csproj"
RUN dotnet publish "src/DsaPractice.Judge/DsaPractice.Judge.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# NOTE: this container needs access to the host Docker daemon to spin up sandboxed
# execution containers (Docker.DotNet talks to /var/run/docker.sock). In local
# docker-compose, mount the socket: -v /var/run/docker.sock:/var/run/docker.sock
# In AKS, this needs a different approach (Kubernetes Jobs API instead of raw Docker) —
# revisit when this moves past local dev.
ENTRYPOINT ["dotnet", "DsaPractice.Judge.dll"]
