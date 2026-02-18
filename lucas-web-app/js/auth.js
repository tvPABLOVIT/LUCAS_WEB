/**
 * Lucas Web — Autenticación
 * Token Bearer en sessionStorage, helpers para fetch autenticado.
 */
(function (global) {
  var STORAGE_TOKEN = 'lucas_token';
  var STORAGE_ROLE = 'lucas_role';
  var STORAGE_USER_ID = 'lucas_userId';

  function getBaseUrl() {
    var base = (global.LUCAS_CONFIG && global.LUCAS_CONFIG.API_BASE) || '';
    return base.replace(/\/$/, '');
  }

  function getToken() {
    return sessionStorage.getItem(STORAGE_TOKEN);
  }

  function getRole() {
    return sessionStorage.getItem(STORAGE_ROLE) || '';
  }

  function getUserId() {
    return sessionStorage.getItem(STORAGE_USER_ID) || '';
  }

  function setSession(role, userId, token) {
    sessionStorage.setItem(STORAGE_ROLE, role || '');
    sessionStorage.setItem(STORAGE_USER_ID, userId || '');
    sessionStorage.setItem(STORAGE_TOKEN, token || '');
  }

  function clearSession() {
    sessionStorage.removeItem(STORAGE_TOKEN);
    sessionStorage.removeItem(STORAGE_ROLE);
    sessionStorage.removeItem(STORAGE_USER_ID);
  }

  /**
   * Login por PIN. POST /api/auth/pin
   * @param {string} pin
   * @returns {Promise<{ role: string, userId: string, token: string }>}
   */
  function loginWithPin(pin) {
    var base = getBaseUrl();
    return fetch(base + '/api/auth/pin', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pin: String(pin).trim() }),
    }).then(function (res) {
      if (!res.ok) {
        return res.json().then(function (data) { throw new Error(data.message || 'PIN incorrecto'); }).catch(function (e) {
          if (e.message !== 'PIN incorrecto') throw e;
          throw new Error('PIN incorrecto');
        });
      }
      return res.json();
    }).then(function (data) {
      setSession(data.role, data.userId, data.token);
      return data;
    });
  }

  /**
   * Cerrar sesión. POST /api/auth/logout (opcional) y borrar sesión local.
   */
  function logout() {
    var base = getBaseUrl();
    var token = getToken();
    if (token) {
      fetch(base + '/api/auth/logout', {
        method: 'POST',
        headers: { 'Authorization': 'Bearer ' + token },
      }).catch(function () {});
    }
    clearSession();
  }

  /**
   * Comprobar sesión. GET /api/auth/me
   * @returns {Promise<{ userId: string, role: string }|null>}
   */
  function checkSession() {
    var token = getToken();
    if (!token) return Promise.resolve(null);
    var base = getBaseUrl();
    return fetch(base + '/api/auth/me', {
      headers: { 'Authorization': 'Bearer ' + token },
    }).then(function (res) {
      if (res.status === 401) {
        clearSession();
        return null;
      }
      if (!res.ok) return null;
      return res.json();
    }).then(function (data) {
      if (data) {
        sessionStorage.setItem(STORAGE_ROLE, data.role || '');
        sessionStorage.setItem(STORAGE_USER_ID, data.userId || '');
      }
      return data;
    }).catch(function () { return null; });
  }

  /**
   * fetch con Authorization Bearer
   * @param {string} url - Ruta relativa al API_BASE (ej: '/api/execution/2026-02-06')
   * @param {RequestInit} options
   * @returns {Promise<Response>}
   */
  function fetchWithAuth(url, options) {
    options = options || {};
    var base = getBaseUrl();
    var fullUrl = url.startsWith('http') ? url : base + url;
    var headers = new Headers(options.headers || {});
    if (!headers.has('Content-Type') && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json');
    var token = getToken();
    if (token) headers.set('Authorization', 'Bearer ' + token);
    options.headers = headers;
    return fetch(fullUrl, options).then(function (res) {
      if (res.status === 401) {
        clearSession();
        if (global.LUCAS_APP && global.LUCAS_APP.onUnauthorized) global.LUCAS_APP.onUnauthorized();
      }
      return res;
    });
  }

  global.LUCAS_AUTH = {
    getToken: getToken,
    getRole: getRole,
    getUserId: getUserId,
    getBaseUrl: getBaseUrl,
    loginWithPin: loginWithPin,
    logout: logout,
    checkSession: checkSession,
    fetchWithAuth: fetchWithAuth,
    clearSession: clearSession,
  };
})(typeof window !== 'undefined' ? window : this);
