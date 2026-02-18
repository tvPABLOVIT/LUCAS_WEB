/**
 * Lucas Web — Vista Login
 */
(function (global) {
  var auth = global.LUCAS_AUTH;

  function showError(msg) {
    var el = document.getElementById('login-error');
    if (el) {
      el.textContent = msg || '';
      el.classList.toggle('hidden', !msg);
    }
  }

  /** Deja el formulario listo para otro usuario (PIN vacío, botón habilitado, sin error). */
  function resetForm() {
    var inputPin = document.getElementById('input-pin');
    var btn = document.getElementById('btn-login');
    if (inputPin) inputPin.value = '';
    if (btn) btn.disabled = false;
    showError('');
  }

  function initLogin() {
    var form = document.getElementById('form-login');
    var inputPin = document.getElementById('input-pin');
    var btn = document.getElementById('btn-login');
    if (!form || !inputPin) return;

    resetForm();

    form.onsubmit = function (e) {
      e.preventDefault();
      var pin = (inputPin.value || '').trim();
      if (!pin) {
        showError('Introduce el PIN');
        return;
      }
      showError('');
      if (btn) btn.disabled = true;
      auth.loginWithPin(pin)
        .then(function () {
          global.LUCAS_APP && global.LUCAS_APP.onLoginSuccess();
        })
        .catch(function (err) {
          showError(err.message || 'Error al iniciar sesión');
          if (btn) btn.disabled = false;
        });
    };
  }

  global.LUCAS_LOGIN_VIEW = {
    init: initLogin,
    showError: showError,
  };
})(typeof window !== 'undefined' ? window : this);
