# Lucas Web — Especificación pantalla Configuración

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Objetivo

Permitir configurar parámetros básicos, ubicación del restaurante e integraciones (Google Sheets, túnel Cloudflare) desde la web.

---

## Parámetros (settings.json)

| Parámetro | Descripción | Uso |
|-----------|-------------|-----|
| ProductividadIdealEurHora | €/h ideal para calcular personal sugerido | Predicción |
| HorasPorTurno | Horas por turno (Mediodía, Tarde, Noche) | Predicción |
| NombreRestaurante | Nombre del restaurante | Display |
| DireccionRestaurante | Dirección (para geocodificar) | Clima, festivos |
| LatRestaurante, LonRestaurante | Coordenadas (rellenadas por geocodificación) | Clima, festivos |
| GoogleSheetsUrl | URL de la hoja de cálculo | Sincronización |
| GoogleCredentialsPath | Ruta a credenciales JSON | Sincronización |
| CloudflareTunnelToken | Token del túnel con nombre | Túnel fijo |
| UseTunnel | Usar túnel Cloudflare | Inicio app |

---

## APIs actuales

- **No existe API de configuración** en el Backend actual. La app Windows usa `ConfiguracionService` leyendo/escribiendo `%LocalAppData%\ManagerOS\settings.json` directamente.
- Para web: habría que crear un `SettingsController` con:
  - GET `/api/settings` — Devuelve parámetros editables (sin credenciales sensibles).
  - PATCH `/api/settings` — Actualiza parámetros (validación, persistencia en settings.json).

---

## Consideraciones

- La ruta de settings.json es fija en el servidor; en Modo A (PC local) el Backend comparte carpeta con la app Windows.
- En Modo B (VPS), settings.json estaría en el servidor y la app Windows no podría escribir directamente; la configuración pasaría a gestionarse solo por web o por API.
- **Prioridad media:** La web puede funcionar sin pantalla de Configuración; la mayoría de usuarios sigue usando la app Windows para configurar.
