FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data
ENV ASPNETCORE_ENVIRONMENT=Production
# Railway inyecta PORT (ej. 8080); la app usa UseUrls en Program.cs
EXPOSE 8080
ENTRYPOINT ["dotnet", "LucasWeb.Api.dll"]
