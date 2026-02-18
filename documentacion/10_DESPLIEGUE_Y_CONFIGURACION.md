# 10 — Despliegue y configuración

Despliegue de la app web, variables de entorno, archivos de configuración, base de datos y backup.

---

## 1. Entorno de producción

- **Hosting compartido (PHP + MySQL):** Subir archivos PHP, CSS, JS; crear base de datos MySQL desde el panel; crear usuario BD con permisos sobre esa BD. Configurar `config.php` (o `config.local.php`) con: host BD, nombre BD, usuario, contraseña, y opcionalmente BASE_PATH, DEBUG, URL base de la app.
- **VPS / Azure (ASP.NET Core):** Publicar con `dotnet publish`. Configurar Kestrel o IIS; cadena de conexión a MySQL o SQL Server en appsettings.json o variables de entorno (ConnectionStrings:DefaultConnection). No usar SQLite en producción multi-nodo salvo que sea un solo servidor y se acepte un único archivo.

---

## 2. Base de datos

- **Creación inicial:** Ejecutar el script SQL equivalente a las tablas descritas en 02_MODELO_DE_DATOS (ExecutionDays, ShiftFeedbacks, Users, DailyAnalyses, DetectedPatterns, DetectedTrends, Recommendations, WeeklyPredictions, Events). Incluir índices y claves foráneas con ON DELETE CASCADE donde corresponda (ShiftFeedbacks → ExecutionDayId).
- **Migraciones:** Si se usa EF Core (ASP.NET), ejecutar `dotnet ef database update` en el servidor o en un paso de despliegue. Si se usa PHP, mantener un script SQL único o versionado que se ejecute a mano o con un comando de setup.
- **Usuario inicial:** Tras crear las tablas, insertar un usuario admin con PinHash = BCrypt del PIN elegido (ej. 1234), Role = admin, Email único. No dejar contraseña/PIN por defecto en producción sin cambiarla.

---

## 3. Configuración (settings)

En el proyecto actual la configuración está en `%LocalAppData%\ManagerOS\settings.json`. Campos relevantes:

- HorasPorTurno, ProductividadIdealEurHora, CostoPersonalPorHora  
- NombreRestaurante, DireccionRestaurante, LatRestaurante, LonRestaurante  
- BarrioInteres, ZonaInteres (para eventos)  
- ClimaApiKey (opcional)  
- GoogleSheetsUrl, GoogleCredentialsPath (opcional)  
- BackendUrl, UseTunnel, CloudflareTunnelToken (para app Windows + túnel)  
- PredictionBiasJson, PredictionMaeJson (aprendizaje predicciones)

En la app web, estos valores deben persistirse en BD (tabla Settings clave-valor o columnas) o en un archivo de configuración en el servidor (no accesible desde el cliente). Variables de entorno para secretos (claves API, cadena de conexión).

---

## 4. Variables de entorno recomendadas

- **DB_HOST, DB_NAME, DB_USER, DB_PASSWORD** (o ConnectionStrings:DefaultConnection en .NET).  
- **APP_URL** o **BASE_URL** (URL base de la app para enlaces y cookies).  
- **SESSION_SECRET** o equivalente para firmar la sesión.  
- **CLIMA_API_KEY** (si se usa).  
- **GOOGLE_CREDENTIALS_JSON** (contenido del JSON) o ruta al archivo si el servidor lo permite.

No subir claves ni cadenas de conexión al repositorio.

---

## 5. HTTPS y cookies

- HTTPS obligatorio en producción. Configurar redirección HTTP → HTTPS.
- Cookies de sesión: Secure, HttpOnly, SameSite=Lax (o Strict si no se necesita cross-site). Dominio y path correctos para la app.

---

## 6. Backup

- **Base de datos:** Copias periódicas (dump MySQL o backup del archivo SQLite). Retención según política (ej. diario 7 días, semanal 4 semanas).
- **Archivos:** Si la configuración o datos se guardan en archivos (ej. estimaciones-cache.json), incluirlos en el backup o replicar en BD para no depender de disco.

---

## 7. Referencia al código actual

- **ManagerOS.Backend Program.cs:** Conexión SQLite, ruta manageros.db, CORS, sesión, reescritura /feedback/api.
- **ManagerOS.Windows ConfiguracionService:** SettingsPath, AppDataService.DataFolder, lectura/escritura settings.json.
- **LucasWeb-archivo:** config.php, config.local.php.example, db.php, sql/install.sql (si existe); DESARROLLO-LOCAL.md, iniciar-local.bat.
