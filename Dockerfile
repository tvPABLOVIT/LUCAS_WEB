FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore
COPY LucasWeb.Api/LucasWeb.Api.csproj LucasWeb.Api/
RUN dotnet restore LucasWeb.Api/LucasWeb.Api.csproj

# Build & publish
COPY LucasWeb.Api/ LucasWeb.Api/
RUN dotnet publish LucasWeb.Api/LucasWeb.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# PDF parser dependencies (Python)
RUN apt-get update \
  && apt-get install -y --no-install-recommends python3 python3-pip python-is-python3 \
  && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Include the parser project inside the image
COPY LucasCuadranteParser/ /app/LucasCuadranteParser/
RUN pip install --no-cache-dir -r /app/LucasCuadranteParser/requirements.txt

RUN mkdir -p /app/data

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5261
ENV CuadranteParser__PythonPath=python3
ENV CuadranteParser__ParserProjectPath=/app/LucasCuadranteParser

EXPOSE 5261
ENTRYPOINT ["dotnet", "LucasWeb.Api.dll"]
