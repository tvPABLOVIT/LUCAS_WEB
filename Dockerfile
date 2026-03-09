# Dockerfile ligero para Railway: solo API (.NET). La imagen pesa mucho menos y el deploy suele completar.
# Import de PDF de cuadrante NO funcionará (falta Python/parser). Si lo necesitas, usa Dockerfile.full en Settings → Build.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY LucasWeb.Api/LucasWeb.Api.csproj LucasWeb.Api/
RUN dotnet restore LucasWeb.Api/LucasWeb.Api.csproj

COPY LucasWeb.Api/ LucasWeb.Api/
RUN dotnet publish LucasWeb.Api/LucasWeb.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
RUN mkdir -p /app/data

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "LucasWeb.Api.dll"]
