# Dockerfile completo: API + Python + LucasCuadranteParser (import PDF cuadrante).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY LucasWeb.Api/LucasWeb.Api.csproj LucasWeb.Api/
RUN dotnet restore LucasWeb.Api/LucasWeb.Api.csproj

COPY LucasWeb.Api/ LucasWeb.Api/
RUN dotnet publish LucasWeb.Api/LucasWeb.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Dependencias del parser PDF (Python)
RUN apt-get update \
  && apt-get install -y --no-install-recommends python3 python3-pip python3-full python-is-python3 \
  && rm -rf /var/lib/apt/lists/* \
  && python3 --version \
  && pip3 --version

COPY --from=build /app/publish .

COPY LucasCuadranteParser/ /LucasCuadranteParser/
RUN rm -rf /LucasCuadranteParser/.pytest_cache /LucasCuadranteParser/__pycache__ /LucasCuadranteParser/*/__pycache__ 2>/dev/null || true
RUN pip3 install --break-system-packages --no-cache-dir -r /LucasCuadranteParser/requirements.txt

RUN mkdir -p /app/data

ENV ASPNETCORE_ENVIRONMENT=Production
ENV CuadranteParser__PythonPath=python3
ENV CuadranteParser__ParserProjectPath=/LucasCuadranteParser

EXPOSE 8080
ENTRYPOINT ["dotnet", "LucasWeb.Api.dll"]
