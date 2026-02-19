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
# Exclude cache and test files
COPY LucasCuadranteParser/ /LucasCuadranteParser/
RUN rm -rf /LucasCuadranteParser/.pytest_cache /LucasCuadranteParser/__pycache__ /LucasCuadranteParser/*/__pycache__ 2>/dev/null || true
RUN pip install --no-cache-dir -r /LucasCuadranteParser/requirements.txt

RUN mkdir -p /app/data

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5261
ENV CuadranteParser__PythonPath=python3
ENV CuadranteParser__ParserProjectPath=/LucasCuadranteParser

EXPOSE 5261
ENTRYPOINT ["dotnet", "LucasWeb.Api.dll"]
