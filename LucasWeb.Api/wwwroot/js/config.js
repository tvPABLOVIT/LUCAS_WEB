(function (global) {
  // API_BASE vacío = mismas peticiones al mismo origen (app y API en localhost:5261).
  // Si abres la app en otro puerto, define API_BASE: 'http://localhost:5261'
  var origin = (typeof window !== 'undefined' && window.location && window.location.origin) ? window.location.origin : '';
  global.LUCAS_CONFIG = { API_BASE: '', API_ORIGIN: origin };
})(typeof window !== 'undefined' ? window : this);
