# Constantes y configuración (menú Configuración)

Las constantes del programa que pueden definirse desde el menú **Configuración → Parámetros del sistema** se guardan en la tabla **Settings** (clave-valor) y se exponen en `GET/PATCH /api/settings`.

## Ya configurables desde Configuración

| Clave | Descripción | Valor por defecto | Uso |
|-------|-------------|-------------------|-----|
| **ProductividadIdealEurHora** | Productividad ideal (€/h) | 50 | Dashboard, Estimaciones, predicciones: objetivo de productividad por hora trabajada. |
| **CostePersonalPorHora** / CostoPersonalPorHora | Coste por hora de personal (€/h) | 15,73 | Dashboard (KPI coste personal), Estimaciones: horas × €/h. |
| **HorasPorTurno** | Duración de cada turno (h) | 4 | Cálculo de horas de equipo (personal × horas por turno), reparto sala/cocina. |
| **FacturacionObjetivoSemanal** | Facturación objetivo (€/semana) | — | Dashboard: % vs objetivo de facturación semanal. |
| **DescuentoFacturacionManualPorcentaje** | Descuento aplicado a la facturación ingresada manualmente (%) | 9,1 | Al guardar facturación desde Preguntas/Registro se aplica este %; la facturación del Excel no se descuenta. |
| **PrediccionConservadoraFactor** | Factor predicción conservadora (0,01–1) | — | Todas las predicciones × este factor (ej. 0,97 para bajar estimación). |
| **NombreRestaurante**, **DireccionRestaurante**, **LatRestaurante**, **LonRestaurante**, **CountryCode** | Ubicación (clima) | — | Clima, geocodificación. |
| **Empleados** | JSON: lista de empleados con horas semanales de contrato | — | Coste de personal total, % vs facturación. |
| **GoogleSheetsUrl**, **GoogleCredentialsPath** | Integración Google Sheet | — | Sincronización con la hoja. |
| **GeminiApiKey**, **WeatherApiKey**, **BackendUrl**, **UsarTunnelCloudflared**, etc. | Otros | — | APIs y túnel. |

## Constantes aún fijas en código (candidatas a configuración futura)

- **StaffByTurnoPredictionService**: `DefaultComfortLimit = 350` € (límite cómodo €/persona), `MinRevenueFor3PerShift = 3000` € (umbral para 3 personas por turno). Productividad por defecto 50 €/h se lee ya de Settings.
- **SalaCocinaService** / estimaciones frontend: umbrales 2400 € (mín. 2 sala+cocina), 3000 € (máx. cocina 3), 3500 € (máx. sala 3). Podrían ser claves tipo `UmbralRevenue2Personas`, `UmbralMaxCocina3`, `UmbralMaxSala3`.
- **StaffRevenueComfortService**: bandas de facturación por camarero (400, 500, 600, …). Menos prioritario para exponer en Configuración.

Para añadir una nueva constante configurable:

1. Añadir la clave en `SettingsController.AllowedKeys`.
2. Leer el valor donde se use (p. ej. `_db.Settings` o inyección de opciones) con valor por defecto si no existe.
3. Añadir el campo en `wwwroot/js/views/configuracion.js` (HTML, carga en `loadSettingsForParametros`, envío en `saveParametros`).
