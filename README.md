# BETLEM — Lucas Web

Proyecto **Lucas Web**: versión web completa de Lucas (Manager OS) con Backend ASP.NET Core, frontend HTML/JS y documentación de especificaciones.

## Estructura

| Carpeta | Descripción |
|---------|-------------|
| **lucas-web/** | Documentación y especificaciones (índice, arquitectura, APIs, pantallas, despliegue) |
| **lucas-web-app/** | Frontend standalone (HTML/CSS/JS) para servir aparte o copiar a wwwroot |
| **LucasWeb.Api/** | Backend ASP.NET Core 8 + frontend en wwwroot (todo en uno) |

## Inicio rápido

1. **Requisitos:** .NET 8 SDK
2. **Ejecutar:** `cd LucasWeb.Api` → `dotnet run`
3. **Abrir:** http://localhost:5261
4. **Login:** PIN **1234** (usuario admin por defecto)

Ver [LucasWeb.Api/README.md](LucasWeb.Api/README.md) para más detalles del API y [DEPLOY.md](DEPLOY.md) para despliegue (Modo A local, Modo B VPS, túnel).

## Documentación

- **documentacion/** — Documentación detallada para Lucas Web: modelo de datos, reglas de negocio (Q1–Q4, scoring, KPIs), APIs, flujos, estimaciones, integraciones, pantallas y despliegue. Índice: [documentacion/00_INDICE_Y_VISION.md](documentacion/00_INDICE_Y_VISION.md).
- **lucas-web/** — Plan e inventarios del proyecto (índice: [lucas-web/00_INDICE_Y_PLAN_GLOBAL.md](lucas-web/00_INDICE_Y_PLAN_GLOBAL.md), estado: [lucas-web/ESTADO_PROYECTO.md](lucas-web/ESTADO_PROYECTO.md)).
- **Despliegue:** [DEPLOY.md](DEPLOY.md).
