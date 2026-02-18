/**
 * Lucas Web — Configuración
 * Base URL del API Backend (ASP.NET Core). Ajustar según entorno.
 */
(function (global) {
  global.LUCAS_CONFIG = {
    // Mismo origen si la web se sirve desde el Backend (wwwroot). Si no, ej: 'http://localhost:5261'
    API_BASE: '',
  };
})(typeof window !== 'undefined' ? window : this);
