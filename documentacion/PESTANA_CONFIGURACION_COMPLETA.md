# Pestaña Configuración: campos, uso y funcionamiento

Documento que describe **cada campo** de la pestaña Configuración: nombre, para qué sirve, cómo se usa el dato ingresado y cómo funciona toda la pestaña. La vista está en `ConfiguracionView.xaml` y la lógica en `ConfiguracionViewModel` y `ConfiguracionService`; la persistencia es `settings.json` (Parámetros e Integraciones) y la base de datos SQLite (Usuarios).

---

## 1. Estructura general de la pestaña Configuración

- **Vista:** `ConfiguracionView.xaml`. Título: "Configuración" y texto "Semana N" (número de semana ISO del año).
- **Tres subpestañas (TabControl):**
  1. **Usuarios** — Crear, editar y eliminar usuarios (nombre, email, PIN, rol); tabla de usuarios.
  2. **Parámetros** — Horas por turno, productividad ideal, coste por hora, empleados, ubicación del restaurante, base de datos, copias de seguridad, limpiar BD, guardar parámetros.
  3. **Integraciones** — Backend/túnel, clima API, Gemini, Google Sheets; guardar integraciones.

Al abrir la vista se ejecuta **LoadAsync**: se cargan todos los valores desde `ConfiguracionService` (y BD para usuarios) y se rellenan las propiedades enlazadas a los controles.

---

## 2. Pestaña Usuarios

### 2.1. Bloque "Añadir usuario"

| Campo (etiqueta) | Nombre en binding / código | Tipo | Para qué sirve | Cómo se usa el dato |
|------------------|----------------------------|------|----------------|----------------------|
| **Nombre** | `NombreNuevo` | TextBox | Nombre completo del usuario. | Se guarda en la tabla `Users` como `FullName`. Obligatorio al crear. |
| **Email** | `EmailNuevo` | TextBox | Email del usuario (identificador de cuenta). | Se guarda en `Users.Email`. Obligatorio; debe ser único. |
| **PIN (mín. 4 caracteres)** | `PinNuevoUsuario` (desde `PinNuevoUsuarioBox.Password` en code-behind) | PasswordBox | PIN para entrar en Lucas (tablet). | Se hashea con BCrypt y se guarda en `Users.PinHash`. Mínimo 4 caracteres; máximo 12. |
| **Rol** | `RolNuevo` | ComboBox | Rol del usuario: user, manager, admin. | Se guarda en `Users.Role`. Opciones: "user", "manager", "admin". En Lucas, admin/manager/master ven Preguntas + Registro + Estimaciones; user solo Preguntas. |

- **Botón "Añadir usuario":** Al hacer clic, el code-behind asigna `PinNuevoUsuario = PinNuevoUsuarioBox.Password` y ejecuta `CrearUsuarioAsync`. Se llama a `ConfiguracionService.CreateUserWithPinAsync(NombreNuevo, EmailNuevo, PinNuevoUsuario, RolNuevo)`. Si todo va bien: usuario creado en BD, mensaje en `MensajeUsuario`, se limpian los campos y se dispara `ClearPasswordRequested` para vaciar el PasswordBox. Los usuarios se leen de la BD; no se guardan en settings.json.

### 2.2. Bloque "Editar usuario" (visible al seleccionar Editar en la tabla)

| Campo | Binding | Para qué sirve | Uso del dato |
|-------|---------|----------------|--------------|
| **Nombre** | `NombreEdit` | Nombre del usuario en edición. | Se actualiza en `Users.FullName` al guardar. |
| **Email** | `EmailEdit` | Email del usuario. | Se actualiza en `Users.Email` (debe seguir siendo único). |
| **Rol** | `RolEdit` | Rol (user, manager, admin). | Se actualiza en `Users.Role`. |
| **Nuevo PIN (opcional)** | `PinNuevo` | Nuevo PIN para el usuario. | Si no está vacío, se hashea y se guarda en `Users.PinHash` al guardar (`SetUserPinAsync`). |
| **Activo** | `IsActiveEdit` | Si el usuario puede entrar. | Se actualiza en `Users.IsActive`. |

- **Botones Guardar / Cancelar:** Guardar ejecuta `GuardarEdicionUsuarioAsync` (actualiza usuario y opcionalmente PIN); Cancelar limpia `UsuarioEditando` y mensaje.

### 2.3. Tabla "Usuarios del sistema"

- **ItemsSource:** `Users` (lista de `UserItem`: Id, FullName, Email, Role, IsActive).
- **Columnas:** Nombre, Email, Rol, Activo, Acciones (Editar, Eliminar).
- **Editar:** Asigna el usuario a `UsuarioEditando` y rellena NombreEdit, EmailEdit, RolEdit, IsActiveEdit, PinNuevo = "".
- **Eliminar:** Llama a `DeleteUserAsync(Id)`. No se puede eliminar el último administrador activo.

---

## 3. Pestaña Parámetros

Todos los valores de esta pestaña se leen y guardan en **settings.json** (carpeta de datos de la app, p. ej. `%LocalAppData%\ManagerOS\settings.json`). Al guardar se usa el botón **"Guardar parámetros"** (comando `GuardarParametrosCommand`).

### 3.1. Horas por turno

| Etiqueta / descripción | Binding | Para qué sirve | Uso del dato |
|------------------------|---------|----------------|--------------|
| **Horas por turno** — "Cálculo de horas trabajadas: (Sala + Cocina) × este valor" | `HorasPorTurno` | Número de horas que se asumen por turno por persona (ej. 6). | En **Registro**: cálculo de horas trabajadas cuando no se introducen manualmente. En **Estimaciones**: horas necesarias = (facturación estimada por turno / productividad ideal) / horas por turno → número de personas; también `EstimatedStaffHours` = suma (personal por turno × horas por turno). InteligenciaService usa este valor en predicción y esquema sala/cocina. |

- **Validación al guardar:** Entre 1 y 24. Por defecto 6.

### 3.2. Productividad ideal (€/h)

| Etiqueta / descripción | Binding | Para qué sirve | Uso del dato |
|------------------------|---------|----------------|--------------|
| **Productividad ideal (€/h)** — "Objetivo de facturación por hora trabajada; se usará en Estimaciones para comparar con la productividad real." | `ProductividadIdealEurHora` | Objetivo de facturación por hora de trabajo. | En **Estimaciones**: cálculo de personal sugerido por turno: `techo(facturación estimada turno / productividad ideal / horas por turno)`. En **InteligenciaService**: mismo cálculo en predicción y en `GetSalaCocinaScheme` (umbrales por facturación). En **RecomendacionesViewModel**: productividad estimada = facturación estimada / horas estimadas. Comparación con productividad real en Registro. |

- **Validación:** ≥ 0.

### 3.3. Coste por hora de personal (€/h)

| Etiqueta / descripción | Binding | Para qué sirve | Uso del dato |
|------------------------|---------|----------------|--------------|
| **Coste por hora de personal (€/h)** — "Coste medio por hora de personal; se usa en Estimaciones para el KPI Costo de personal (horas × €/h)." | `CostoPersonalPorHora` | Coste medio por hora trabajada. | En **Estimaciones (RecomendacionesViewModel)**: KPI "Coste de personal" = total horas de contrato (suma de empleados) × coste por hora; y % respecto a facturación promedio histórica. No se usa el personal estimado por turno para el coste; se usan las **horas de contrato** de la lista de empleados. |

- **Validación:** ≥ 0.

### 3.4. Empleados

| Etiqueta / descripción | Binding | Para qué sirve | Uso del dato |
|------------------------|---------|----------------|--------------|
| **Empleados** — "Lista de empleados con horas semanales de contrato (ej. 40 = 8h×5 días + 2 libres). Se usa para calcular el coste de personal total y el % sobre facturación estimada." | `Employees` (colección), `EmployeeNombreNuevo`, `EmployeeHorasNuevo` | Lista nombre + horas semanales de contrato. | Se persiste en **settings.json** como `EmployeesJson` (array de `{ Name, HorasSemanales }`). En **Estimaciones**: `totalHorasContrato = Sum(empleados.HorasSemanales)`; coste total = totalHorasContrato × CostoPersonalPorHora; % = coste / facturación promedio histórica. Al añadir o quitar empleado se llama a `SetEmployees` (persistir en settings). |

- **Añadir:** Nombre (obligatorio) y Horas/sem (1–80). Por defecto 40.
- **Quitar:** Elimina el elemento de la lista y vuelve a guardar empleados.

### 3.5. Ubicación del restaurante (para el clima)

| Etiqueta | Binding | Para qué sirve | Uso del dato |
|----------|---------|----------------|--------------|
| **Nombre del restaurante** | `NombreRestaurante` | Nombre del local (referencia). | Se guarda en settings como `NombreRestaurante`. Se puede usar en etiquetas o en integraciones; el clima se obtiene principalmente por **Dirección**. |
| **Dirección (ciudad o dirección completa)** | `DireccionRestaurante` | Dirección para geocodificar y obtener coordenadas. | Se guarda en settings. **ClimaService** usa esta dirección (y opcionalmente `LatRestaurante`/`LonRestaurante` si ya están guardados) para llamar a la API de clima (Open-Meteo). Al guardar parámetros se llama a `ClimaService.RefreshRestaurantLocationAsync()`: geocodifica la dirección y guarda lat/lon en settings (`LatRestaurante`, `LonRestaurante`). Esas coordenadas las usan Registro (clima del día), Estimaciones (clima semana siguiente) e InteligenciaService (factores por día). |

- **Mensaje tras guardar:** "Ubicación para el clima: encontrada." o "No se pudo localizar la dirección…" según el resultado de la geocodificación.

### 3.6. Ruta de la base de datos

| Etiqueta | Binding | Para qué sirve | Uso del dato |
|----------|---------|----------------|--------------|
| **Ruta de la base de datos (solo lectura)** | `RutaBaseDatos` | Mostrar dónde está el archivo SQLite. | Solo lectura. Valor = `AppDataService.DatabasePath`. No se edita desde aquí. |

### 3.7. Copias de seguridad

| Elemento | Función | Uso |
|----------|---------|-----|
| **Texto** | "Cree una copia antes de vaciar la BD. Las copias se guardan en %LocalAppData%\ManagerOS\Backups\" | Información al usuario. |
| **Botón "Crear copia de seguridad"** | `CrearCopiaSeguridadCommand` | Llama a `RegistroService.CreateBackupAsync()`: copia el archivo de la BD a la carpeta Backups con nombre con fecha/hora. Actualiza la lista de copias. |
| **Botón "Actualizar lista"** | `CargarListaBackupsCommand` | Recarga la lista de rutas de copias desde la carpeta Backups. |
| **ComboBox "Restaurar desde copia"** | `BackupPaths`, `SelectedBackupPath` | Lista de rutas de archivos de copia. El usuario elige una. |
| **Botón "Restaurar desde copia"** | `RestaurarDesdeCopiaCommand` | Pide confirmación; llama a `RegistroService.RestoreFromBackupAsync(SelectedBackupPath)`: reemplaza la BD actual por la copia. Se indica reiniciar la app. |
| **Mensaje** | `MensajeRestaurar` | Resultado de restaurar o error. |

### 3.8. Limpiar base de datos

| Elemento | Función | Uso |
|----------|---------|-----|
| **Texto** | "Vacía toda la BD (días, análisis, patrones…). Los usuarios se mantienen. Se recomienda crear antes una copia." | Aviso. |
| **Botón "Limpiar base de datos"** | `LimpiarBaseDatosCommand` | Primero pregunta si crear copia (Sí/No/Cancelar). Luego pide confirmación para vaciar. Llama a `RegistroService.ClearAllDataAsync()`: borra días de ejecución, turnos, análisis, patrones, tendencias, recomendaciones, predicciones, etc.; **no** borra usuarios. Mensaje en `MensajeBaseDatos`. |
| **Mensaje** | `MensajeBaseDatos` | Resultado de copia/limpiar o error. |

### 3.9. Guardar parámetros

- **Botón "Guardar parámetros":** Ejecuta `GuardarParametrosAsync`. Valida HorasPorTurno (1–24), ProductividadIdealEurHora ≥ 0, CostoPersonalPorHora ≥ 0. Llama a SetHorasPorTurno, SetProductividadIdealEurHora, SetCostoPersonalPorHora, SetEmployees, SetNombreRestaurante, SetDireccionRestaurante; luego `ClimaService.RefreshRestaurantLocationAsync()` si hay dirección. Mensaje de éxito o error en `MensajeParametros`.

**Nota:** BarrioInteres y ZonaInteres existen en `ConfiguracionService` y se guardan en settings.json; se usan en **InteligenciaService** para alertas de eventos/obras (Open Data BCN: barrio o zona de interés). En la vista actual **no hay campos** para editarlos; si se necesitan, habría que añadir dos TextBox en Parámetros o Integraciones y enlazarlos a propiedades que llamen a SetBarrioInteres/SetZonaInteres.

---

## 4. Pestaña Integraciones

Todos los valores se persisten en **settings.json**. Se aplican al pulsar **"Guardar integraciones"** (`GuardarIntegracionesCommand`).

### 4.1. Backend (tablet / feedback)

| Campo / control | Binding | Para qué sirve | Uso del dato |
|-----------------|---------|----------------|--------------|
| **URL del backend** — "URL del backend al que se conecta la tablet (Modo B) o la URL pública del túnel (Modo A con cloudflared)." | `BackendUrl` | URL base del API (ej. http://localhost:5261 o https://xxx.trycloudflare.com). | Se guarda en settings. La app Windows la usa para **enviar datos a la tablet** (push de estimaciones, etc.): si está configurada, se hace POST a BackendUrl + ruta correspondiente. |
| **Usar túnel (cloudflared) en este equipo** | `UseTunnel` | Activar o no el túnel cloudflared. | Se guarda en settings. **BackendTunnelLauncher** usa este valor para decidir si inicia/reinicia el proceso cloudflared (quick tunnel o token). |
| **Estado** | `CloudflaredStatus` | Mensaje "cloudflared: instalado" o instrucción para instalar. | Solo lectura; viene de `BackendTunnelLauncher.IsCloudflaredAvailable()`. |
| **URL del túnel (quick tunnel)** | `TunnelUrlActual` | URL que devuelve cloudflared (quick tunnel). | Solo lectura. Se actualiza al cargar la vista o al reiniciar túnel. El usuario la copia y la abre en la tablet para acceder a Lucas. |
| **Botón "Copiar URL"** | `CopiarUrlTunnelCommand` | Copia `TunnelUrlActual` al portapapeles. | Para pegar en el navegador de la tablet. |
| **Token del túnel (URL fija)** — "Si lo rellenas, la URL no cambiará al reiniciar. Crea un túnel en Cloudflare Zero Trust…" | `CloudflareTunnelToken` | Token del túnel con nombre de Cloudflare. | Se guarda en settings. Si está definido, BackendTunnelLauncher usa este token en lugar del quick tunnel; la URL del túnel es estable. |
| **Botón "Reiniciar backend"** | `ReiniciarBackendCommand` | Reinicia el proceso del Backend (ManagerOS.Backend). | Útil tras cambiar configuración o para recuperar el servicio. |
| **Botón "Reiniciar túnel"** | `ReiniciarTunnelCommand` | Reinicia el proceso cloudflared (quick o con token). | Actualiza `TunnelUrlActual` cuando el túnel esté listo. |

### 4.2. Clima (API)

| Campo | Binding | Para qué sirve | Uso del dato |
|-------|---------|----------------|--------------|
| **Clave API para obtener tiempo (opcional; Open-Meteo no requiere clave)."** | `ClimaApiKey` | Clave de API de clima si se usara un proveedor que la exija. | Se guarda en settings. **ClimaService** actualmente usa Open-Meteo, que no requiere clave; este campo queda disponible por si se integra otro proveedor. |

- Texto de ayuda: "Si el clima no aparece en Registro: compruebe la dirección del restaurante en Parámetros y la conexión a Internet."

### 4.3. Gemini (IA / resúmenes)

| Campo | Binding | Para qué sirve | Uso del dato |
|-------|---------|----------------|--------------|
| **Clave API de Google AI Studio** — "Para generar resúmenes de turno con IA en Registro." | `GeminiApiKey` | Clave de Google AI Studio (Gemini). | Se guarda en settings. **ResumenIAService** la usa para llamar a la API de Gemini y generar el resumen de turno en lenguaje natural a partir de V/R/M/D (y contexto). Si está vacía, no se generan resúmenes con IA. |

### 4.4. Google Sheets

| Campo / control | Binding | Para qué sirve | Uso del dato |
|-----------------|---------|----------------|--------------|
| **URL de la hoja** — "URL donde exportar resúmenes." | `GoogleSheetsUrl` | URL del documento de Google Sheets (ej. https://docs.google.com/spreadsheets/d/ID/...). | Se guarda en settings. Se usa en: **RegistroService** (exportar todo, importar Excel); **ExecutionController** (Backend) tras guardar feedback desde tablet: SyncToGoogleSheet(day) escribe una fila por día en la hoja del mes. **GoogleSheetSyncService** extrae el ID del spreadsheet de la URL. |
| **Archivo de credenciales (cuenta de servicio)** | `GoogleCredentialsPath` | Nombre de archivo (ej. manager-os-xxx.json) o ruta completa. Vacío = google-credentials.json en la carpeta de datos. | Se guarda en settings. **GoogleSheetSyncService** y **RegistroService** usan esta ruta para autenticar con la API de Google Sheets (cuenta de servicio). |
| **Botón "Abrir Google Sheets"** | `AbrirGoogleSheetsCommand` | Abre la URL en el navegador; si hay credenciales y sheetId, intenta abrir en la última hoja mensual. | Solo navegación. |
| **Botón "Exportar todo al Google Sheet"** | `ExportarTodoGoogleSheetCommand` | Llama a `RegistroService.ExportAllDaysToGoogleSheetAsync()`: envía todos los días guardados al sheet (una fila por día). | Usa GoogleSheetsUrl y GoogleCredentialsPath. |
| **Botón "Importar archivo de estimaciones (Excel)"** | `ImportarEstimacionExcelCommand` | Abre diálogo para elegir un Excel (sN_AAAA.xlsx); llama a `RegistroService.ImportFromEstimacionExcelAsync`: extrae facturaciones de 2 semanas antes y actualiza BD y Google Sheet. | Para cargar datos históricos desde un Excel de estimaciones. |

- **Guardar integraciones:** Ejecuta SetClimaApiKey, SetGoogleSheetsUrl, SetGoogleCredentialsPath, SetBackendUrl, SetUseTunnel, SetCloudflareTunnelToken, SetGeminiApiKey. Mensaje en `MensajeIntegraciones`.

---

## 5. Dónde se guarda cada cosa

| Origen | Persistencia |
|--------|---------------|
| **Usuarios** (nombre, email, PIN, rol, activo) | Base de datos SQLite (tabla `Users`). |
| **Parámetros** (horas por turno, productividad ideal, coste por hora, empleados, nombre/dirección restaurante, lat/lon tras geocodificar) | Archivo **settings.json** (carpeta de datos ManagerOS). |
| **Integraciones** (backend URL, túnel, clima, Gemini, Google Sheets URL y credenciales) | **settings.json**. |
| **Predicción (bias/MAE por día de semana)** | **settings.json** (PredictionBiasJson, PredictionMaeJson); lo actualiza **InteligenciaService** al evaluar predicciones, no el usuario desde Configuración. |
| **BarrioInteres / ZonaInteres** | **settings.json**; usados por InteligenciaService para eventos/obras; en la vista actual no hay campos para editarlos. |

---

## 6. Flujo al abrir y al guardar

- **Al abrir la pestaña Configuración:** El ViewModel se construye con `ConfiguracionService`, `ClimaService`, `RegistroService`. En el constructor se llama a `LoadAsync()`: se leen de ConfiguracionService todos los parámetros e integraciones, se cargan los empleados, la ruta de la BD, el estado de cloudflared y la URL del túnel; se cargan los usuarios desde la BD y se rellenan las propiedades. La vista enlaza esas propiedades a los controles (TwoWay donde corresponde).
- **Guardar parámetros:** Solo afecta a la pestaña Parámetros. Escribe en settings.json y opcionalmente actualiza lat/lon con ClimaService.
- **Guardar integraciones:** Solo afecta a la pestaña Integraciones. Escribe en settings.json.
- **Usuarios:** Se crean/actualizan/eliminan en la BD al pulsar Añadir usuario, Guardar (edición) o Eliminar; no hay un botón global "Guardar usuarios".

Con esto queda documentado cada campo de la pestaña Configuración, su nombre, para qué sirve, cómo se usa el dato y cómo funciona la pestaña en conjunto.
