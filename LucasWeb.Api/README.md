# Lucas Web — Backend API

Backend ASP.NET Core 8 para **Lucas (Manager OS)**. Sirve la API y el frontend estático desde `wwwroot`.

## Requisitos

- .NET 8 SDK

## Ejecutar

```bash
cd LucasWeb.Api
dotnet run
```

La aplicación escucha en **http://localhost:5261**.  
Abre el navegador en esa URL: verás el login de Lucas Web.

## Usuario por defecto

Tras el primer arranque se crea un usuario **admin** con PIN **1234**.  
Puedes cambiarlo en `appsettings.json` → `Lucas:DefaultPin` (solo afecta al primer seed).

## Base de datos

- **SQLite** en `manageros.db` (en la carpeta del proyecto).
- Se crea automáticamente al iniciar si no existe.
- Connection string en `appsettings.json` → `ConnectionStrings:DefaultConnection`.

## APIs

| Ruta | Descripción |
|------|-------------|
| GET `/api/health` | Estado del servicio (sin auth) |
| POST `/api/auth/pin` | Login por PIN. Body: `{ "pin": "1234" }` |
| GET `/api/auth/me` | Usuario actual (header `Authorization: Bearer {token}`) |
| POST `/api/auth/logout` | Cerrar sesión |
| GET/POST/PATCH `/api/execution` | Días de ejecución (registro) |
| GET `/api/dashboard/week?weekStart=yyyy-MM-dd` | Resumen semanal |
| GET `/api/estimaciones` | Cache de estimaciones |
| POST `/api/estimaciones/cache` | Guardar cache (app Windows) |
| GET `/api/predictions/next-week` | Predicción semana siguiente |
| GET `/api/recommendations` | Recomendaciones |
| PATCH `/api/recommendations/{id}` | Actualizar estado (body: `{ "status": "accepted" }`) |
| GET `/api/recommendations/version` | Versión para refresco |
| GET `/api/settings` | Parámetros (ProductividadIdealEurHora, HorasPorTurno, NombreRestaurante, etc.) |
| PATCH `/api/settings` | Actualizar parámetros (body: `{ "ProductividadIdealEurHora": "80", ... }`) |

## Frontend

El frontend está en `wwwroot/`. Se sirve en la misma URL que la API (mismo origen).  
No hace falta configurar CORS si usas solo este servidor.
