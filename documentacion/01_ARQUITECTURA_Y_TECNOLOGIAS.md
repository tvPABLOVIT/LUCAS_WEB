# 01 — Arquitectura y tecnologías

Documentación para llevar Lucas a una **app web** desde cero. Arquitectura general, stack y despliegue.

---

## 1. Arquitectura actual (referencia)

- **Cliente:** App Windows (WPF) + navegador (vista tablet en `/feedback`).
- **Backend:** ASP.NET Core (Kestrel), puerto 5261, misma máquina que la app Windows.
- **Base de datos:** SQLite, archivo único en `%LocalAppData%\ManagerOS\manageros.db`.
- **Túnel:** Cloudflared (opcional) para exponer `/feedback` con URL fija (token).
- **Configuración:** `%LocalAppData%\ManagerOS\settings.json` (parámetros, ubicación, claves API).

La app Windows inicia el Backend y el túnel; la tablet accede por la URL del túnel (o `http://localhost:5261/feedback` en local).

---

## 2. Arquitectura objetivo (app web)

- **Cliente:** Navegador (SPA o páginas servidas por el backend).
- **Backend:** Servidor web (PHP o ASP.NET Core) en hosting compartido o VPS.
- **Base de datos:** MySQL (o MariaDB) en el mismo hosting; o SQL Server si se usa ASP.NET en VPS.
- **Sin app de escritorio obligatoria:** toda la funcionalidad accesible desde la web. Opcionalmente se mantiene una “vista tablet” (solo preguntas) con la misma base.

---

## 3. Opciones de stack

### Opción A — PHP + MySQL (hosting compartido, 0 €/mes)

- **Backend:** PHP 8.x, sesiones nativas o JWT.
- **BD:** MySQL 5.7+ / MariaDB 10.x.
- **Frontend:** HTML, CSS, JavaScript (vanilla o framework ligero). Opcionalmente Vue/React si el hosting permite build estático.
- **Ventajas:** Compatible con IONOS, 1&1, etc.; sin necesidad de .NET en el servidor.
- **Desventajas:** Reimplementar toda la lógica (ya documentada en esta carpeta).

### Opción B — ASP.NET Core + MySQL/SQL Server

- **Backend:** ASP.NET Core 8, mismos controladores y DTOs que el Backend actual (adaptando conexión a MySQL/SQL Server).
- **BD:** MySQL (vía Pomelo.EntityFrameworkCore.MySql) o SQL Server.
- **Frontend:** Igual que Opción A; o Blazor si se prefiere.
- **Ventajas:** Reutilizar código C# del Backend y de la lógica de negocio (InteligenciaService, etc.) si se extrae a un proyecto compartido.
- **Desventajas:** Hosting compartido típico no suele soportar .NET; hace falta VPS o Azure.

### Recomendación

- **Presupuesto 0 €/mes y hosting compartido:** Opción A (PHP + MySQL).
- **Si ya se usa .NET y hay VPS o Azure:** Opción B (ASP.NET Core + MySQL/SQL Server).

---

## 4. Componentes de la app web

| Componente | Descripción |
|------------|-------------|
| **Autenticación** | Login por PIN; sesión por cookie (y opcionalmente token Bearer para tablet/API). |
| **Registro de ejecución** | CRUD días de ejecución: fecha, facturación total, horas, notas, 3 turnos (Mediodía, Tarde, Noche) con feedback Q1–Q4 y personal sala/cocina. |
| **Dashboard** | Resumen semanal: facturación, productividad, horas, comparativa con semana anterior e histórico. |
| **Estimaciones** | Predicción semana siguiente: facturación por día/turno, KPIs históricos, alertas (tendencia, clima, festivos, misma semana mes anterior, eventos/obras). |
| **Configuración** | Parámetros (horas por turno, productividad objetivo, coste/hora), ubicación (coordenadas, dirección), integraciones (clima, Google Sheets, etc.). |
| **Usuarios y roles** | Alta de usuarios con PIN y rol (user, admin, manager, master). Solo admin/manager/master acceden a Registro, Estimaciones y Configuración. |
| **Vista tablet** | Pantalla reducida: solo preguntas de feedback por turno (y opcionalmente registro/estimaciones si el PIN es admin). |

---

## 5. Seguridad

- **PIN:** Almacenar hash BCrypt del PIN; nunca el PIN en claro.
- **Contraseña (si se usa login email/contraseña):** Igual, BCrypt.
- **Sesión:** Cookie HttpOnly, Secure en HTTPS, SameSite=Lax. Timeout recomendado: 24 h.
- **API:** Todas las rutas de registro, dashboard, estimaciones y configuración requieren sesión (o Bearer token) y comprobación de rol donde aplique.
- **CORS:** Configurar orígenes permitidos en producción (no `*` con credenciales).
- **HTTPS:** Obligatorio en producción.

---

## 6. Hosting y despliegue

- **Hosting compartido (PHP):** Subir archivos PHP, CSS, JS; crear BD MySQL desde panel; configurar `config.php` con credenciales y rutas.
- **VPS / Azure (ASP.NET Core):** Publicar con `dotnet publish`; configurar Kestrel o IIS; conexión a MySQL/SQL Server vía cadena de conexión.
- **Base de datos:** Ejecutar script SQL de creación de tablas (equivalente a migraciones EF del proyecto actual); ver 02_MODELO_DE_DATOS y 10_DESPLIEGUE_Y_CONFIGURACION.

---

## 7. Archivos de referencia en el proyecto actual

- `ManagerOS.sln` — Solución .NET.
- `src/ManagerOS.Backend/Program.cs` — Configuración Kestrel, CORS, sesión, reescritura `/feedback/api` → `/api`, rutas estáticas.
- `src/ManagerOS.Backend/Controllers/*.cs` — Endpoints API.
- `src/ManagerOS.Infrastructure/Data/ApplicationDbContext.cs` — Modelo EF y configuración de tablas.
- `src/ManagerOS.Windows/Services/*.cs` — Lógica de negocio (Registro, Inteligencia, Configuración, Clima, Festivos, Open Data BCN, etc.).
