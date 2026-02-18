# Lucas Web — Arquitectura y tecnologías

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Arquitectura actual

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Tablet / móvil                                                              │
│  Navegador → https://túnel/feedback  (HTML/JS en wwwroot/feedback)           │
│  Login PIN → Sesión (cookie o Bearer) → Formulario feedback                  │
└───────────────────────────────────────────┬─────────────────────────────────┘
                                            │ HTTPS
                                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  Cloudflare Tunnel (cloudflared)                                             │
└───────────────────────────────────────────┬─────────────────────────────────┘
                                            │ HTTP localhost
                                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  Backend ASP.NET Core (puerto 5261)                                          │
│  • /api/auth, /api/execution, /api/dashboard                                 │
│  • /api/recommendations, /api/predictions, /api/estimaciones                 │
│  • Static files: /feedback/*                                                 │
│  • SQLite: %LocalAppData%/ManagerOS/manageros.db (Cache=Shared)              │
└───────────────────────────────────────────┬─────────────────────────────────┘
                                            │
┌───────────────────────────────────────────┴─────────────────────────────────┐
│  App Windows (WPF) — mismo PC                                               │
│  • Inicia Backend y túnel                                                   │
│  • DbContext → manageros.db                                                 │
│  • Pantallas: Registro, Estimaciones, Configuración                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Arquitectura objetivo (web completa)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Frontend Web (React/Vue/HTML+JS)                                            │
│  Rutas: /login, /registro, /estimaciones, /configuracion                     │
│  Auth: PIN → token Bearer en header                                          │
└───────────────────────────────────────────┬─────────────────────────────────┘
                                            │ HTTPS (o HTTP localhost)
                                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  Backend ASP.NET Core (existente, posible ampliación)                        │
│  • Mismas APIs actuales                                                     │
│  • Posibles nuevos: GET/PATCH settings (si se expone Configuración por web) │
└───────────────────────────────────────────┬─────────────────────────────────┘
                                            │
                                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  SQLite (Modo A: PC local) / PostgreSQL o similar (Modo B: VPS)              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Stack tecnológico

| Componente | Actual | Recomendado para Web |
|------------|--------|----------------------|
| **Backend** | ASP.NET Core 8 | Mantener (ya listo) |
| **BD** | SQLite | SQLite (Modo A) / PostgreSQL (Modo B) |
| **Frontend** | HTML+JS en wwwroot/feedback | HTML+JS (mantener simplicidad) o React/Vue para SPA |
| **Auth** | PIN + BCrypt, sesión + token Bearer | Mantener |
| **Túnel** | Cloudflared | Mantener para tablet remota |

---

## Reutilización del Backend

El Backend actual **ya expone** las APIs necesarias para:

- **Auth:** POST `/api/auth/pin`, GET `/api/auth/me`, POST `/api/auth/logout`
- **Registro:** GET `/api/execution/{date}`, POST `/api/execution`, PATCH `/api/execution/{date}`
- **Dashboard/Estimaciones:** GET `/api/dashboard/week`, GET `/api/estimaciones`, GET `/api/predictions/next-week`, GET `/api/recommendations`, GET `/api/recommendations/version`
- **Estático:** `/feedback/*` para la app actual de feedback

Para una **web completa**:

1. **Opción A:** Ampliar la misma app de feedback (`wwwroot/feedback`) con más páginas (registro, estimaciones, configuración) usando HTML+JS.
2. **Opción B:** Crear un frontend SPA (React/Vue) en otra carpeta, servido desde el mismo Backend o como estáticos.
3. **Opción C:** Mantener la app Windows como administrador y usar la web solo para feedback (tablet) y consulta de estimaciones.

La guía recomienda **Opción A** para minimizar cambios y aprovechar la reutilización total del Backend.
