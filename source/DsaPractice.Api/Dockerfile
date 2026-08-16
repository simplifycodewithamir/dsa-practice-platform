FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore "source/DsaPractice.Api/DsaPractice.Api.csproj"
RUN dotnet publish "source/DsaPractice.Api/DsaPractice.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "DsaPractice.Api.dll"]
