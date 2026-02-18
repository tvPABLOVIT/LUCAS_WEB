(function (global) {
  var STORAGE_TOKEN = 'lucas_token', STORAGE_ROLE = 'lucas_role', STORAGE_USER_ID = 'lucas_userId';
  function getBaseUrl() {
    var b = (global.LUCAS_CONFIG && global.LUCAS_CONFIG.API_BASE) || '';
    b = b.replace(/\/$/, '');
    if (!b && typeof window !== 'undefined' && window.location) b = window.location.origin;
    return b;
  }
  function getToken() { return sessionStorage.getItem(STORAGE_TOKEN); }
  function getRole() { return sessionStorage.getItem(STORAGE_ROLE) || ''; }
  function getUserId() { return sessionStorage.getItem(STORAGE_USER_ID) || ''; }
  function setSession(role, userId, token) { sessionStorage.setItem(STORAGE_ROLE, role || ''); sessionStorage.setItem(STORAGE_USER_ID, userId || ''); sessionStorage.setItem(STORAGE_TOKEN, token || ''); }
  function clearSession() { sessionStorage.removeItem(STORAGE_TOKEN); sessionStorage.removeItem(STORAGE_ROLE); sessionStorage.removeItem(STORAGE_USER_ID); }
  function loginWithPin(pin) {
    return fetch(getBaseUrl() + '/api/auth/pin', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ pin: String(pin).trim() }) })
      .then(function (res) { if (!res.ok) return res.json().then(function (d) { throw new Error(d.message || 'PIN incorrecto'); }).catch(function (e) { throw e.message ? e : new Error('PIN incorrecto'); }); return res.json(); })
      .then(function (data) { setSession(data.role, data.userId, data.token); return data; });
  }
  function logout() { var t = getToken(); if (t) fetch(getBaseUrl() + '/api/auth/logout', { method: 'POST', headers: { 'Authorization': 'Bearer ' + t } }).catch(function () {}); clearSession(); }
  function checkSession() {
    var t = getToken(); if (!t) return Promise.resolve(null);
    return fetch(getBaseUrl() + '/api/auth/me', { headers: { 'Authorization': 'Bearer ' + t } })
      .then(function (res) { if (res.status === 401) { clearSession(); return null; } if (!res.ok) return null; return res.json(); })
      .then(function (data) { if (data) { sessionStorage.setItem(STORAGE_ROLE, data.role || ''); sessionStorage.setItem(STORAGE_USER_ID, data.userId || ''); } return data; }).catch(function () { return null; });
  }
  function fetchWithAuth(url, options) {
    options = options || {}; var fullUrl = url.startsWith('http') ? url : getBaseUrl() + url; var headers = new Headers(options.headers || {}); if (!headers.has('Content-Type') && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json'); var t = getToken(); if (t) headers.set('Authorization', 'Bearer ' + t); options.headers = headers;
    return fetch(fullUrl, options).then(function (res) { if (res.status === 401) { clearSession(); if (global.LUCAS_APP && global.LUCAS_APP.onUnauthorized) global.LUCAS_APP.onUnauthorized(); } return res; });
  }
  global.LUCAS_AUTH = { getToken: getToken, getRole: getRole, getUserId: getUserId, getBaseUrl: getBaseUrl, loginWithPin: loginWithPin, logout: logout, checkSession: checkSession, fetchWithAuth: fetchWithAuth, clearSession: clearSession };
})(typeof window !== 'undefined' ? window : this);
