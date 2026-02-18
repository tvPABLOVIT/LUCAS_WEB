(function (global) {
  var auth = global.LUCAS_AUTH;
  function getWeekStart(d) { var date = typeof d === 'string' ? new Date(d) : new Date(d); var day = date.getDay(); var diff = date.getDate() - day + (day === 0 ? -6 : 1); var monday = new Date(date); monday.setDate(diff); return monday.getFullYear() + '-' + String(monday.getMonth() + 1).padStart(2, '0') + '-' + String(monday.getDate()).padStart(2, '0'); }
  function getISOWeekNumber(ymd) { var d = new Date(ymd + 'T12:00:00'); var dayNum = d.getDay() || 7; d.setDate(d.getDate() + 4 - dayNum); var yearStart = new Date(d.getFullYear(), 0, 1); return Math.ceil((((d - yearStart) / 86400000) + 1) / 7); }
  function formatWeekRange(ymd) { var d = new Date(ymd + 'T12:00:00'); var end = new Date(d); end.setDate(end.getDate() + 6); var fmt = function (x) { return String(x.getDate()).padStart(2, '0') + ' ' + ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'][x.getMonth()]; }; return fmt(d) + ' – ' + fmt(end) + ' ' + d.getFullYear(); }
  function isCurrentWeek(weekStartYmd) { return weekStartYmd === getWeekStart(new Date()); }
  function addDays(ymd, delta) { var d = new Date(ymd + 'T12:00:00'); d.setDate(d.getDate() + delta); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
  function render(container) {
    var weekStart = getWeekStart(new Date());
    var state = { mode: 'plan', histWeekStart: weekStart, planWeekStart: null };

    container.innerHTML =
      '<div class="dashboard-title-row">' +
      '<div class="dashboard-title-block">' +
      '<h2 class="view-title">Estimaciones</h2>' +
      '<p id="estim-subtitle" class="dashboard-subtitle">Planificación: predicción y plan para la semana siguiente</p>' +
      '</div>' +
      '<div class="dashboard-week-bar">' +
      '<div class="estim-mode-toggle" role="tablist" aria-label="Modo Estimaciones">' +
      '<button type="button" id="estim-mode-plan" class="estim-mode-btn estim-mode-btn--active" role="tab" aria-selected="true">Planificación (semana siguiente)</button>' +
      '<button type="button" id="estim-mode-hist" class="estim-mode-btn" role="tab" aria-selected="false">Histórico (semana seleccionada)</button>' +
      '</div>' +
      '<button type="button" id="estim-cargar" class="btn-primary dashboard-week-btn-actualizar">Actualizar</button>' +
      '<div class="dashboard-week-nav">' +
      '<button type="button" id="estim-prev" class="dashboard-week-arrow" title="Semana anterior">◀</button>' +
      '<span id="estim-week-range" class="dashboard-week-range">' + formatWeekRange(weekStart) + '</span>' +
      '<button type="button" id="estim-next" class="dashboard-week-arrow" title="Semana siguiente">▶</button>' +
      '</div>' +
      '<span id="estim-week-status" class="dashboard-week-status hidden"></span>' +
      '<span id="estim-source-badge" class="estim-source-badge hidden"></span>' +
      '<input type="date" id="estim-week-start" class="dashboard-week-input-hidden" value="' + weekStart + '" aria-hidden="true" tabindex="-1" />' +
      '</div>' +
      '</div>' +
      '<div id="estim-kpis" class="kpi-grid"></div>' +
      '<div id="estim-days-cards" class="estim-days-cards"></div>' +
      '<div id="estim-prediction" class="card"></div>' +
      '<div id="estim-alertas" class="card"></div>' +
      '<div id="estim-accuracy-history" class="card"></div>' +
      '<div class="estim-actions-weather-row">' +
      '<div id="estim-actions" class="card estim-card-compact"></div>' +
      '<div id="estim-weather-impact" class="card estim-card-compact"></div>' +
      '</div>' +
      '<div id="estim-limite-comodo" class="estim-limite-comodo-row"></div>';

    var weekInput = document.getElementById('estim-week-start');
    var weekRangeEl = document.getElementById('estim-week-range');
    var badgeEl = document.getElementById('estim-week-status');
    var sourceBadgeEl = document.getElementById('estim-source-badge');
    var subtitleEl = document.getElementById('estim-subtitle');
    var kpisEl = document.getElementById('estim-kpis');
    var predEl = document.getElementById('estim-prediction');
    var daysCardsEl = document.getElementById('estim-days-cards');
    var alertasEl = document.getElementById('estim-alertas');
    var weatherImpactEl = document.getElementById('estim-weather-impact');
    var actionsEl = document.getElementById('estim-actions');
    var limiteEl = document.getElementById('estim-limite-comodo');
    var accuracyHistoryEl = document.getElementById('estim-accuracy-history');
    function totalToCocinaSala(n) {
      if (n <= 0) return { sala: 0, cocina: 0 };
      if (n === 1) return { sala: 1, cocina: 0 };
      if (n === 2) return { sala: 1, cocina: 1 };
      if (n === 3) return { sala: 2, cocina: 1 };
      if (n === 4) return { sala: 2, cocina: 2 };
      if (n === 5) return { sala: 3, cocina: 2 };
      return { sala: 3, cocina: 3 };
    }
    /** Tarde: cocina ≤ sala; Mediodía/Noche: cocina ≥ sala (igual que backend). */
    function totalToCocinaSalaByShift(tot, shiftName) {
      if (tot <= 0) return { sala: 0, cocina: 0 };
      if (tot === 1) return { sala: 1, cocina: 0 };
      if (tot === 2) return { sala: 1, cocina: 1 };
      var isTarde = shiftName && String(shiftName).toLowerCase() === 'tarde';
      if (isTarde) {
        if (tot === 3) return { sala: 2, cocina: 1 };
        if (tot === 4) return { sala: 2, cocina: 2 };
        if (tot === 5) return { sala: 3, cocina: 2 };
        return { sala: 3, cocina: 3 };
      }
      if (tot === 3) return { sala: 1, cocina: 2 };
      if (tot === 4) return { sala: 2, cocina: 2 };
      if (tot === 5) return { sala: 2, cocina: 3 };
      return { sala: 3, cocina: 3 };
    }
    function getSalaCocinaScheme(med, tar, noc, prodEurHora, horasTurno, revenueDia) {
      if (!prodEurHora || !horasTurno) return { sala: '—', cocina: '—' };
      var div = prodEurHora * horasTurno;
      function pers(rev) { var n = Math.round(rev / div); return n < 1 ? 1 : Math.min(n, 6); }
      var m = pers(med), t = pers(tar), n = pers(noc);
      var req2 = revenueDia >= 2400, maxC = revenueDia > 3000 ? 3 : 2, maxS = revenueDia >= 3500 ? 3 : 2;
      function minS(rev) { return rev > 600 ? 2 : 1; }
      function aplic(tot, rev, shiftName, out) {
        var r = totalToCocinaSalaByShift(tot, shiftName);
        var s = Math.max(r.sala, req2 ? 2 : minS(rev));
        var c = Math.max(r.cocina, req2 ? 2 : 1);
        out.s = Math.min(s, maxS); out.c = Math.min(c, maxC);
      }
      var sm = {}, st = {}, sn = {};
      aplic(m, med, 'mediodia', sm); aplic(t, tar, 'tarde', st); aplic(n, noc, 'noche', sn);
      return { sala: sm.s + '-' + st.s + '-' + sn.s, cocina: sm.c + '-' + st.c + '-' + sn.c };
    }
    function confianzaLabel(revenue, min, max) {
      if (!revenue || revenue <= 0) return '—';
      var w = (max - min) / revenue;
      if (w < 0.30) return 'Alta';
      if (w < 0.50) return 'Media';
      return 'Baja';
    }
    function comfortLabel(eurPerWaiter, limit) {
      if (limit == null || limit <= 0) return '—';
      if (eurPerWaiter <= limit * 0.9) return 'cómodo';
      if (eurPerWaiter <= limit * 1.1) return 'límite';
      return 'alto';
    }
    /** Combinaciones permitidas Sala (M-T-N). Debe coincidir con backend StaffByTurnoPredictionService. */
    var ALLOWED_SALA = [[1,1,1],[1,1,2],[2,1,2],[2,2,2],[1,2,1],[2,1,1],[3,1,3],[1,2,3],[3,1,2],[3,2,2]];
    /** Combinaciones permitidas Cocina (M-T-N). */
    var ALLOWED_COCINA = [[1,1,1],[2,1,2],[1,1,2],[2,2,2],[3,1,3],[1,2,3],[3,2,2]];
    function snapToAllowed(value, allowed, revMed, revTar, revNoc) {
      var m = value[0], t = value[1], n = value[2];
      function dist(a) { return Math.abs(a[0] - m) + Math.abs(a[1] - t) + Math.abs(a[2] - n); }
      function covers(a) { return a[0] >= m && a[1] >= t && a[2] >= n; }
      function sum(a) { return a[0] + a[1] + a[2]; }
      function consistent(a) {
        if (revMed >= revTar && a[0] < a[1]) return false;
        if (revTar >= revNoc && a[1] < a[2]) return false;
        if (revMed >= revNoc && a[0] < a[2]) return false;
        if (revTar >= revMed && a[1] < a[0]) return false;
        if (revNoc >= revTar && a[2] < a[1]) return false;
        if (revNoc >= revMed && a[2] < a[0]) return false;
        return true;
      }
      var pool = allowed.filter(consistent);
      if (pool.length === 0) pool = allowed;
      var covering = pool.filter(covers);
      if (covering.length > 0) {
        covering.sort(function (a, b) { var sa = sum(a), sb = sum(b); if (sa !== sb) return sa - sb; return dist(a) - dist(b); });
        return covering[0][0] + '-' + covering[0][1] + '-' + covering[0][2];
      }
      pool.sort(function (a, b) { var d = dist(a) - dist(b); if (d !== 0) return d; return sum(a) - sum(b); });
      var best = pool[0];
      return best[0] + '-' + best[1] + '-' + best[2];
    }
    /** Calcula personal por turno (Sala y Cocina) según límite cómodo: por cada turno el menor esquema S-C que cumple límite; luego ajusta a combinaciones permitidas (M-T-N) respetando orden de facturación. */
    function getSalaCocinaSchemeFromComfort(med, tar, noc, comfortBySchema, comfortByCocina) {
      var allowed = ['1-1', '1-2', '2-1', '2-2', '2-3', '3-2', '3-3'];
      var defaultLimitSala = 350, defaultLimitCocina = 350;
      var margin = 1.05;
      function minSchemaForRevenue(rev) {
        for (var i = 0; i < allowed.length; i++) {
          var parts = allowed[i].split('-');
          var S = parseInt(parts[0], 10) || 1, C = parseInt(parts[1], 10) || 1;
          var limitSala = comfortBySchema[allowed[i]] != null ? comfortBySchema[allowed[i]] : defaultLimitSala;
          var limitCocina = comfortByCocina[parts[1]] != null ? comfortByCocina[parts[1]] : defaultLimitCocina;
          if (rev <= 0 || (S > 0 && rev / S <= limitSala * margin && C > 0 && rev / C <= limitCocina * margin))
            return { sala: S, cocina: C };
        }
        return { sala: 3, cocina: 3 };
      }
      var medS = minSchemaForRevenue(med), tarS = minSchemaForRevenue(tar), nocS = minSchemaForRevenue(noc);
      var salaStr = snapToAllowed([medS.sala, tarS.sala, nocS.sala], ALLOWED_SALA, med, tar, noc);
      var cocinaStr = snapToAllowed([medS.cocina, tarS.cocina, nocS.cocina], ALLOWED_COCINA, med, tar, noc);
      return { sala: salaStr, cocina: cocinaStr };
    }
    function load() {
      function safeJson(r, fallback) { if (!r || !r.ok) return Promise.resolve(fallback); return r.json().then(function (d) { return d; }).catch(function () { return fallback; }); }
      function fmtMoney(n) { if (n == null || isNaN(Number(n))) return '—'; return Number(n).toFixed(0).replace(/\B(?=(\d{3})+(?!\d))/g, ',') + ' €'; }
      function fmtNum(n, dec) { if (n == null || isNaN(Number(n))) return '—'; return Number(n).toFixed(dec == null ? 1 : dec); }
      function fmtPct(n) { if (n == null || isNaN(Number(n))) return '—'; var x = Number(n); return (x > 0 ? '+' : '') + x.toFixed(0) + '%'; }
      function setSourceBadge(text, mod) {
        if (!sourceBadgeEl) return;
        if (!text) { sourceBadgeEl.classList.add('hidden'); sourceBadgeEl.textContent = ''; sourceBadgeEl.classList.remove('estim-source-badge--saved'); sourceBadgeEl.classList.remove('estim-source-badge--live'); return; }
        sourceBadgeEl.textContent = text;
        sourceBadgeEl.classList.remove('hidden');
        sourceBadgeEl.classList.toggle('estim-source-badge--saved', mod === 'saved');
        sourceBadgeEl.classList.toggle('estim-source-badge--live', mod === 'live');
      }
      function renderWeatherImpact(weatherImpact) {
        if (!weatherImpactEl) return;
        function fmtPct2(x) { if (x == null || isNaN(Number(x))) return '—'; var n = Number(x); return (n > 0 ? '+' : '') + n.toFixed(0) + '%'; }
        function deltaClass(val) {
          if (val == null || isNaN(Number(val))) return '';
          var n = Number(val);
          return n > 0 ? ' estim-weather-delta--up' : (n < 0 ? ' estim-weather-delta--down' : '');
        }
        function line(title, obj) {
          if (!obj) return '<div class="estim-weather-line"><span class="label">' + title + '</span> —</div>';
          var pctRev = fmtPct2(obj.diffPctRevenue);
          var pctProd = fmtPct2(obj.diffPctProductivity);
          var revClass = deltaClass(obj.diffPctRevenue);
          var prodClass = deltaClass(obj.diffPctProductivity);
          return '<div class="estim-weather-line"><span class="label">' + title + '</span> ' +
            (obj.count != null ? (obj.count + ' muestras') : '—') +
            ' · Δ fact: <strong class="estim-weather-delta' + revClass + '">' + pctRev + '</strong>' +
            ' · Δ prod: <strong class="estim-weather-delta' + prodClass + '">' + pctProd + '</strong>' +
            '</div>';
        }
        if (!weatherImpact || !weatherImpact.sampleCount || weatherImpact.sampleCount < 10) {
          weatherImpactEl.innerHTML =
            '<h3>Impacto del clima (histórico)</h3>' +
            '<p class="dashboard-subtitle">Aún no hay suficientes datos con clima guardado para calcular impacto.</p>' +
            '<button type="button" class="btn-primary btn-sm estim-weather-backfill-btn">Backfill clima (180 días)</button>' +
            '<div id="estim-weather-backfill-status" class="estim-weather-backfill-status"></div>';
        } else {
          var cov = weatherImpact.coverage || null;
          var covHint = '';
          if (cov && cov.withAnyWeather != null && Number(cov.withAnyWeather) < 10) {
            covHint = '<p class="dashboard-subtitle estim-weather-alert"><strong>Faltan datos de clima en el histórico</strong> (solo ' + Number(cov.withAnyWeather) + ' registros con clima).</p>' +
              '<button type="button" class="btn-primary btn-sm estim-weather-backfill-btn">Backfill clima (180 días)</button>' +
              '<div id="estim-weather-backfill-status" class="estim-weather-backfill-status"></div>';
          }
          weatherImpactEl.innerHTML =
            '<h3>Impacto del clima (histórico)</h3>' +
            '<p class="dashboard-subtitle">Comparación de facturación/productividad vs días sin esa condición (últimos meses).</p>' +
            covHint +
            '<div class="estim-weather-lines">' +
            line('Días lluviosos', weatherImpact.rainy) +
            line('Lluvia intensa', weatherImpact.heavyRain) +
            line('Viento fuerte', weatherImpact.windy) +
            line('Temperatura extrema', weatherImpact.extremeTemp) +
            '</div>';
        }
        weatherImpactEl.querySelectorAll('.estim-weather-backfill-btn').forEach(function (btn) {
          btn.onclick = function () {
            var statusEl = document.getElementById('estim-weather-backfill-status');
            if (statusEl) statusEl.textContent = 'Ejecutando backfill…';
            auth.fetchWithAuth('/api/weather/backfill?days=180&force=false', { method: 'POST' })
              .then(function (r) { return (r && r.ok ? r.json() : Promise.resolve(null)).then(function (d) { return d; }).catch(function () { return null; }); })
              .then(function (res) { if (statusEl) statusEl.textContent = (res && res.message ? res.message : 'Backfill completado.'); load(); })
              .catch(function () { if (statusEl) statusEl.textContent = 'Error en backfill.'; });
          };
        });
      }

      function renderAlertas(alertasResp) {
        if (!alertasEl) return;
        var alertasList = (alertasResp && alertasResp.alertas) ? alertasResp.alertas : [];
        var alertasVisibles = alertasList.filter(function (a) {
          if (!a || (a.texto == null || a.texto === '')) return false;
          var t = (a.tipo || '').toLowerCase();
          if (t === 'nsemanas') return false;
          return true;
        });
        if (alertasVisibles.length === 0) {
          alertasEl.innerHTML = '<p class="dashboard-subtitle estim-alertas-subtitle">Sin alertas disponibles.</p>';
          return;
        }
        var TIPOS_FACTORES = ['clima', 'climalluviaintensa', 'climaviento', 'temperatura', 'festivos', 'eventos', 'obras'];
        var TIPOS_CONTEXTO = ['tendencia', 'semanaanterior', 'mismasemanamesanterior', 'mismasemanaanoanterior', 'concentracionfinde', 'costepersonal'];
        var TIPOS_META = ['nsemanas', 'patrones'];
        function grupo(a) {
          var t = (a.tipo || '').toLowerCase().replace(/_/g, '');
          if (TIPOS_FACTORES.indexOf(t) >= 0) return 'factores';
          if (TIPOS_CONTEXTO.indexOf(t) >= 0) return 'contexto';
          if (TIPOS_META.indexOf(t) >= 0) return 'meta';
          return 'contexto';
        }
        var porGrupo = { factores: [], contexto: [], meta: [] };
        alertasVisibles.forEach(function (a) { var g = grupo(a); if (porGrupo[g]) porGrupo[g].push(a); });
        var maxLen = 120;
        function cardHtml(a) {
          var titulo = a.titulo || a.tipo || '';
          var texto = (a.texto || '').replace(/</g, '&lt;').replace(/>/g, '&gt;');
          var ayuda = (a.ayuda || '').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
          var tipo = (a.tipo || '').toLowerCase();
          var cardMod = tipo || 'otro';
          var badge = '';
          if (a.pct != null) {
            var pctNum = Number(a.pct);
            var badgeClass = pctNum > 1 ? 'estim-alerta-badge--up' : (pctNum < -1 ? 'estim-alerta-badge--down' : 'estim-alerta-badge--stable');
            badge = '<span class="estim-alerta-badge ' + badgeClass + '">' + (pctNum > 0 ? '+' : '') + pctNum.toFixed(0) + '%</span>';
            if (pctNum < -1) cardMod = 'tendencia-baja'; else if (pctNum > 1) cardMod = 'tendencia-alza';
          }
          var avisoClass = a.esAviso ? ' estim-alerta-card--aviso' : '';
          var tooltip = ayuda ? ' <span class="estim-alerta-ayuda" title="' + ayuda + '" aria-label="Ayuda">?</span>' : '';
          var long = texto.length > maxLen;
          var textoWrap = long ? '<div class="estim-alerta-texto estim-alerta-texto--collapsed">' + texto + '</div><button type="button" class="estim-alerta-ver-mas">Ver más</button>' : '<div class="estim-alerta-texto">' + texto + '</div>';
          var dates = (a.dates && Array.isArray(a.dates) && a.dates.length) ? (' data-dates="' + a.dates.join(',') + '"') : '';
          return '<div class="estim-alerta-card estim-alerta-card--' + cardMod + avisoClass + '"' + dates + '><div class="estim-alerta-titulo">' + titulo + (badge ? ' ' + badge : '') + tooltip + '</div>' + textoWrap + '</div>';
        }
        var html = '';
        if (porGrupo.factores.length > 0) {
          html += '<div class="estim-alertas-grid">' + porGrupo.factores.map(cardHtml).join('') + '</div>';
        }
        if (porGrupo.contexto.length > 0) {
          html += '<div class="estim-alertas-grid">' + porGrupo.contexto.map(cardHtml).join('') + '</div>';
        }
        if (porGrupo.meta.length > 0) {
          html += '<div class="estim-alertas-meta">' + porGrupo.meta.map(cardHtml).join('') + '</div>';
        }
        alertasEl.innerHTML = html;
        alertasEl.querySelectorAll('.estim-alerta-ver-mas').forEach(function (btn) {
          btn.addEventListener('click', function () {
            var card = btn.closest('.estim-alerta-card');
            var textoEl = card.querySelector('.estim-alerta-texto');
            if (textoEl && textoEl.classList.contains('estim-alerta-texto--collapsed')) {
              textoEl.classList.remove('estim-alerta-texto--collapsed');
              btn.textContent = 'Ver menos';
            } else {
              if (textoEl) textoEl.classList.add('estim-alerta-texto--collapsed');
              btn.textContent = 'Ver más';
            }
          });
        });
        alertasEl.querySelectorAll('.estim-alerta-card[data-dates]').forEach(function (card) {
          card.addEventListener('click', function (e) {
            if (e && e.target && (e.target.classList && e.target.classList.contains('estim-alerta-ver-mas'))) return;
            var dates = card.getAttribute('data-dates');
            if (!dates) return;
            var first = dates.split(',')[0];
            var el = document.getElementById('estim-day-' + first);
            if (el && el.scrollIntoView) {
              el.scrollIntoView({ behavior: 'smooth', block: 'start' });
              el.classList.add('estim-day-card--highlight');
              setTimeout(function () { el.classList.remove('estim-day-card--highlight'); }, 2500);
            }
          });
        });
      }

      function renderAccuracyHistory(weeks) {
        if (!accuracyHistoryEl) return;
        if (!weeks || weeks.length === 0) {
          accuracyHistoryEl.innerHTML = '<h3>Historial de precisión</h3><p class="dashboard-subtitle">Aún no hay semanas evaluadas (predicción vs real). Se rellena al evaluar la predicción de la semana pasada.</p>';
          return;
        }
        function fmtMoney(n) {
          if (n == null || isNaN(Number(n))) return '—';
          return Number(n).toFixed(0).replace(/\B(?=(\d{3})+(?!\d))/g, ',') + ' €';
        }
        var rows = weeks.map(function (w) {
          var err = w.errorPercent != null ? Number(w.errorPercent).toFixed(1) + '%' : '—';
          var acc = w.accuracyPercent != null ? Number(w.accuracyPercent).toFixed(1) + '%' : '—';
          var staffMae = w.staffSalaMae != null ? Number(w.staffSalaMae).toFixed(2) : '—';
          var staffMatch = w.staffExactMatchPct != null ? Number(w.staffExactMatchPct).toFixed(0) + '%' : '—';
          var weekLabel = w.weekStartMonday ? (function () {
            var d = new Date(w.weekStartMonday + 'T12:00:00');
            var end = new Date(d);
            end.setDate(end.getDate() + 6);
            var fmt = function (x) { return String(x.getDate()).padStart(2, '0') + '/' + String(x.getMonth() + 1).padStart(2, '0'); };
            return fmt(d) + ' – ' + fmt(end) + ' ' + d.getFullYear();
          })() : w.weekStartMonday || '—';
          return '<tr><td>' + weekLabel + '</td><td>' + fmtMoney(w.predictedRevenue) + '</td><td>' + fmtMoney(w.actualRevenue) + '</td><td>' + err + '</td><td>' + acc + '</td><td>' + staffMae + '</td><td>' + staffMatch + '</td></tr>';
        }).join('');
        accuracyHistoryEl.innerHTML =
          '<h3>Historial de precisión</h3>' +
          '<p class="dashboard-subtitle">Predicción vs facturación real y personal recomendado vs real (semanas ya evaluadas).</p>' +
          '<div class="estim-accuracy-table-wrap"><table class="config-table estim-accuracy-table">' +
          '<thead><tr><th>Semana</th><th>Predicho</th><th>Real</th><th>Error %</th><th>Precisión %</th><th>MAE personal</th><th>Coinc. personal %</th></tr></thead><tbody>' + rows + '</tbody></table></div>';
      }

      function renderActions(wsA) {
        if (!actionsEl) return;
        actionsEl.innerHTML =
          '<h3>Acciones</h3>' +
          '<p class="dashboard-subtitle estim-actions-subtitle">Operaciones rápidas y crear evento.</p>' +
          '<div class="estim-actions-quick">' +
          '<span class="estim-actions-label">Acciones rápidas</span>' +
          '<div class="estim-actions-grid">' +
          '<button type="button" class="btn-secondary btn-sm" id="estim-action-evaluate">Evaluar predicción semana pasada</button>' +
          '<button type="button" class="btn-secondary btn-sm" id="estim-action-patterns">Recalcular patrones</button>' +
          '<button type="button" class="btn-secondary btn-sm" id="estim-action-backfill">Backfill clima (180 días)</button>' +
          '</div></div>' +
          '<div class="estim-actions-form">' +
          '<span class="estim-actions-label">Crear evento</span>' +
          '<div class="estim-actions-row">' +
          '<input type="date" id="estim-ev-date" value="' + wsA + '" />' +
          '<input type="text" id="estim-ev-name" placeholder="Nombre (ej. concierto, feria...)" />' +
          '<select id="estim-ev-impact"><option value=\"\">Impacto</option><option>Alto</option><option selected>Medio</option><option>Bajo</option></select>' +
          '<button type="button" class="btn-primary btn-sm" id="estim-ev-create">Crear</button>' +
          '</div>' +
          '<div id="estim-actions-status" class="estim-actions-status"></div>' +
          '</div>';
      }

      function attachActionsHandlers() {
        var statusEl = document.getElementById('estim-actions-status');
        function setStatus(msg, ok) { if (!statusEl) return; statusEl.textContent = msg || ''; statusEl.className = 'estim-actions-status' + (ok === true ? ' ok' : ok === false ? ' err' : ''); }
        var btnEval = document.getElementById('estim-action-evaluate');
        var btnPat = document.getElementById('estim-action-patterns');
        var btnBack = document.getElementById('estim-action-backfill');
        var btnEv = document.getElementById('estim-ev-create');
        if (btnEval) btnEval.onclick = function () {
          setStatus('Evaluando…', null);
          auth.fetchWithAuth('/api/estimaciones/evaluate-predictions', { method: 'POST' }).then(function (r) { return safeJson(r, null); }).then(function () { setStatus('Evaluación completada.', true); load(); }).catch(function () { setStatus('Error evaluando.', false); });
        };
        if (btnPat) btnPat.onclick = function () {
          setStatus('Recalculando patrones…', null);
          auth.fetchWithAuth('/api/estimaciones/compute-patterns', { method: 'POST' }).then(function (r) { return safeJson(r, null); }).then(function () { setStatus('Patrones recalculados.', true); load(); }).catch(function () { setStatus('Error recalculando patrones.', false); });
        };
        if (btnBack) btnBack.onclick = function () {
          setStatus('Ejecutando backfill…', null);
          auth.fetchWithAuth('/api/weather/backfill?days=180&force=false', { method: 'POST' }).then(function (r) { return safeJson(r, null); }).then(function (res) { setStatus(res && res.message ? res.message : 'Backfill completado.', true); load(); }).catch(function () { setStatus('Error en backfill.', false); });
        };
        if (btnEv) btnEv.onclick = function () {
          var date = (document.getElementById('estim-ev-date') || {}).value;
          var name = (document.getElementById('estim-ev-name') || {}).value;
          var impact = (document.getElementById('estim-ev-impact') || {}).value;
          setStatus('Creando evento…', null);
          auth.fetchWithAuth('/api/events', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ date: date, name: name, impact: impact || null }) })
            .then(function (r) { return safeJson(r, null); })
            .then(function (res) { if (res && res.id) setStatus('Evento creado.', true); else setStatus('No se pudo crear el evento.', false); load(); })
            .catch(function () { setStatus('Error creando evento.', false); });
        };
      }

      function renderDayCardsFromPrediction(pred, settings, comfortBySchema, comfortByCocina) {
        var prodObj = (settings && settings.ProductividadIdealEurHora != null && settings.ProductividadIdealEurHora !== '') ? parseFloat(settings.ProductividadIdealEurHora) : 50;
        var costeHora = (settings && settings.CostePersonalPorHora != null && settings.CostePersonalPorHora !== '') ? parseFloat(settings.CostePersonalPorHora) : null;
        var horasPorTurno = (settings && settings.HorasPorTurno != null && settings.HorasPorTurno !== '') ? parseFloat(settings.HorasPorTurno) : 4;

        var predRevenue = pred && pred.totalRevenue != null ? pred.totalRevenue : null;
        var horasNecesarias = (predRevenue != null && prodObj > 0) ? predRevenue / prodObj : null;
        var costePersonalEur = (horasNecesarias != null && costeHora != null) ? horasNecesarias * costeHora : null;
        var costePctPred = (predRevenue != null && predRevenue > 0 && costePersonalEur != null) ? (costePersonalEur / predRevenue * 100).toFixed(1) : null;

        // KPIs (plan)
        kpisEl.innerHTML = '';
        var kpis = [
          { label: 'Facturación estimada (semana)', value: fmtMoney(predRevenue) },
          { label: 'Horas necesarias (objetivo)', value: (horasNecesarias != null ? horasNecesarias.toFixed(0) + ' h' : '—') },
          { label: 'Coste personal estimado', value: (costePctPred != null ? costePctPred + '% (' + fmtMoney(costePersonalEur) + ')' : '—') },
          { label: 'Productividad objetivo', value: (prodObj != null ? Number(prodObj).toFixed(0) + ' €/h' : '—') }
        ];
        kpis.forEach(function (k) { var div = document.createElement('div'); div.className = 'kpi-card'; div.innerHTML = '<div class="label">' + k.label + '</div><div class="value">' + k.value + '</div>'; kpisEl.appendChild(div); });

        var nextWeekRange = (pred && pred.weekStartMonday) ? formatWeekRange(pred.weekStartMonday) : null;
        var parrafo = '';
        if (nextWeekRange != null && predRevenue != null) {
          parrafo = 'Para la semana del ' + nextWeekRange + ', se estima una facturación total de ' + Number(predRevenue).toFixed(0) + ' €.';
          if (horasNecesarias != null) parrafo += ' Para alcanzar la productividad objetivo (' + Number(prodObj).toFixed(0) + ' €/h) se estiman ' + horasNecesarias.toFixed(0) + ' horas.';
          if (costePctPred != null) parrafo += ' El coste de personal sería ~' + costePctPred + '% vs facturación.';
        }
        if (pred && pred.isSavedPrediction) setSourceBadge('Guardada', 'saved');
        else if (pred && (pred.dailyPredictionsJson || predRevenue != null)) setSourceBadge('En vivo', 'live');
        predEl.innerHTML = parrafo ? '<div class="estim-pred-parrafo-wrap"><p class="estim-parrafo estim-parrafo--centrado">' + parrafo + '</p></div>' : '<div class="estim-pred-parrafo-wrap"><p class="estim-parrafo estim-parrafo--centrado estim-parrafo--muted">Sin datos de predicción.</p></div>';

        var days = (pred && pred.dailyPredictionsJson) ? (function () { try { return JSON.parse(pred.dailyPredictionsJson); } catch (e) { return null; } })() : null;
        if (!daysCardsEl) return;
        if (!days || !days.length) { daysCardsEl.innerHTML = ''; return; }
        daysCardsEl.innerHTML = '<div class="estim-days-grid"></div>';
        var grid = daysCardsEl.querySelector('.estim-days-grid');
        days.forEach(function (d) {
          var rev = d.revenue != null ? d.revenue : d.predictedRevenue || 0;
          var min = d.min != null ? d.min : rev * 0.85, max = d.max != null ? d.max : rev * 1.15;
          var conf = confianzaLabel(rev, min, max);
          var med = d.mediodia != null ? d.mediodia : rev / 3, tar = d.tarde != null ? d.tarde : rev / 3, noc = d.noche != null ? d.noche : rev / 3;
          var scheme = (d.staffSala != null && d.staffCocina != null) ? { sala: d.staffSala, cocina: d.staffCocina } : (getSalaCocinaSchemeFromComfort(med, tar, noc, comfortBySchema, comfortByCocina) || getSalaCocinaScheme(med, tar, noc, prodObj, horasPorTurno, rev));
          var dateFmt = d.date ? (function () { var x = new Date(d.date + 'T12:00:00'); return String(x.getDate()).padStart(2, '0') + '/' + String(x.getMonth() + 1).padStart(2, '0'); })() : '—';
          var climaPart = (d.weatherDescription != null && d.weatherDescription !== '') ? d.weatherDescription : '';
          if (d.precipMm != null) climaPart = [climaPart, Number(d.precipMm).toFixed(1) + ' mm'].filter(Boolean).join(' · ');
          if (d.windMaxKmh != null) climaPart = [climaPart, Number(d.windMaxKmh).toFixed(0) + ' km/h'].filter(Boolean).join(' · ');
          if (d.tempMax != null || d.tempMin != null) climaPart = [climaPart, (d.tempMax != null ? Number(d.tempMax).toFixed(0) : '—') + ' / ' + (d.tempMin != null ? Number(d.tempMin).toFixed(0) : '—') + ' °C'].filter(Boolean).join(' · ');
          var festivoText = (d.isHoliday && d.holidayName) ? d.holidayName : 'No';
          var factors = d.factors || null;
          var factorsLine = '';
          if (factors && factors.totalFactor != null) {
            factorsLine = '<div class="estim-day-factors">Factores: clima ×' + Number(factors.weatherFactor || 1).toFixed(2) +
              ' · festivo ×' + Number(factors.holidayFactor || 1).toFixed(2) +
              ' · temp ×' + Number(factors.tempFactor || 1).toFixed(2) +
              ' · evento ×' + Number(factors.eventFactor || 1).toFixed(2) +
              ' ⇒ total ×' + Number(factors.totalFactor || 1).toFixed(2) + '</div>';
          }
          var shiftSrc = d.shiftDistributionSource ? ('<div class="estim-day-factors">Distribución turnos: ' + d.shiftDistributionSource + '</div>') : '';
          var staffSrc = d.staffSource ? ('<div class="estim-day-factors">Personal: ' + (d.staffSource === 'historic' ? 'histórico' : d.staffSource === 'mixed' ? 'histórico + heurística' : 'heurística') + '</div>') : '';
          var card = '<div class="estim-day-card" id="estim-day-' + (d.date || '') + '">' +
            '<div class="estim-day-header"><div class="estim-day-name">' + (d.dayName || '—') + '</div><div class="estim-day-date">' + dateFmt + '</div></div>' +
            '<div class="estim-day-revenue">' + Number(rev).toFixed(0).replace(/\B(?=(\d{3})+(?!\d))/g, ',') + ' €</div>' +
            '<div class="estim-day-range">Rango: ' + Number(min).toFixed(0).replace(/\B(?=(\d{3})+(?!\d))/g, ',') + ' – ' + Number(max).toFixed(0).replace(/\B(?=(\d{3})+(?!\d))/g, ',') + ' €</div>' +
            '<div class="estim-day-conf">Confianza: ' + conf + '</div>' +
            '<div class="estim-day-shifts">' +
            '<div class="estim-day-shift-row"><span>Mediodía</span><span>' + Number(med).toFixed(0) + ' €</span></div>' +
            '<div class="estim-day-shift-row"><span>Tarde</span><span>' + Number(tar).toFixed(0) + ' €</span></div>' +
            '<div class="estim-day-shift-row"><span>Noche</span><span>' + Number(noc).toFixed(0) + ' €</span></div>' +
            '</div>' +
            '<div class="estim-day-staff">Sala: ' + scheme.sala + '</div><div class="estim-day-staff">Cocina: ' + scheme.cocina + '</div>' +
            (staffSrc || '') +
            (shiftSrc || '') +
            (factorsLine || '') +
            '<div class="estim-day-contexto-wrap">' +
            '<div class="estim-day-contexto-body">Clima: ' + (climaPart || '—') + '</div>' +
            '<div class="estim-day-contexto-body">Festivo: ' + festivoText + '</div></div></div>';
          grid.insertAdjacentHTML('beforeend', card);
        });
      }

      function setLoading() {
        var mode = state.mode;
        var ws0 = mode === 'hist' ? ((weekInput && weekInput.value) || state.histWeekStart) : (state.planWeekStart || weekStart);
        if (weekRangeEl) weekRangeEl.textContent = formatWeekRange(ws0);
        if (badgeEl) {
          if (mode === 'hist' && isCurrentWeek(ws0)) { badgeEl.textContent = 'En curso'; badgeEl.classList.remove('hidden'); badgeEl.classList.add('dashboard-badge--current'); }
          else { badgeEl.textContent = ''; badgeEl.classList.add('hidden'); badgeEl.classList.remove('dashboard-badge--current'); }
        }
        setSourceBadge('', '');
        kpisEl.innerHTML = '<p class="loading">Cargando…</p>';
        predEl.innerHTML = '<p class="loading">Cargando…</p>';
        if (daysCardsEl) daysCardsEl.innerHTML = '';
        if (alertasEl) alertasEl.innerHTML = mode === 'plan' ? '<p class="loading">Cargando alertas…</p>' : '';
        if (accuracyHistoryEl) accuracyHistoryEl.innerHTML = mode === 'plan' ? '<p class="loading">Cargando historial…</p>' : '';
        if (weatherImpactEl) weatherImpactEl.innerHTML = '<p class="loading">Cargando impacto del clima…</p>';
        if (actionsEl) actionsEl.innerHTML = '<p class="loading">Cargando acciones…</p>';
        if (limiteEl && global.LUCAS_LIMITE_COMODO_VIEW) global.LUCAS_LIMITE_COMODO_VIEW.render(limiteEl, { embedded: true });
      }

      setLoading();

      var mode = state.mode;
      var ws = mode === 'hist' ? ((weekInput && weekInput.value) || state.histWeekStart) : (state.planWeekStart || weekStart);

      if (mode === 'plan') {
        if (subtitleEl) subtitleEl.textContent = 'Planificación: predicción y plan para la semana siguiente';
        document.getElementById('estim-prev').disabled = true;
        document.getElementById('estim-next').disabled = true;

        Promise.all([
          auth.fetchWithAuth('/api/predictions/next-week').then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
          auth.fetchWithAuth('/api/settings').then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
          auth.fetchWithAuth('/api/estimaciones/alertas').then(function (r) { return safeJson(r, { alertas: [] }); }).catch(function () { return { alertas: [] }; }),
          auth.fetchWithAuth('/api/analytics/staff-revenue-comfort?minShifts=1').then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
          auth.fetchWithAuth('/api/analytics/weather-impact?groupBy=day').then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
          auth.fetchWithAuth('/api/predictions/accuracy-history?limit=20').then(function (r) { return safeJson(r, { weeks: [] }); }).catch(function () { return { weeks: [] }; })
        ]).then(function (data) {
          var pred = data[0], settings = data[1], alertasResp = data[2], comfortResp = data[3], weatherImpact = data[4], accuracyHistory = data[5];

          var comfortBySchema = {}; var comfortByCocina = {};
          if (comfortResp && comfortResp.schemas && Array.isArray(comfortResp.schemas)) comfortResp.schemas.forEach(function (s) { if (s && s.schema != null) comfortBySchema[s.schema] = s.comfort_limit_approx; });
          if (comfortResp && comfortResp.cocina_schemas && Array.isArray(comfortResp.cocina_schemas)) comfortResp.cocina_schemas.forEach(function (s) { if (s && s.schema != null) comfortByCocina[s.schema] = s.comfort_limit_approx; });

          var planWs = pred && pred.weekStartMonday ? pred.weekStartMonday : ws;
          state.planWeekStart = planWs;
          if (weekRangeEl) weekRangeEl.textContent = formatWeekRange(planWs);

          renderDayCardsFromPrediction(pred, settings, comfortBySchema, comfortByCocina);
          renderAlertas(alertasResp);
          renderAccuracyHistory(accuracyHistory && accuracyHistory.weeks ? accuracyHistory.weeks : []);
          renderWeatherImpact(weatherImpact);
          renderActions(planWs);
          attachActionsHandlers();
        }).catch(function () {
          kpisEl.innerHTML = '<p class="error-msg">Error al cargar. Comprueba la consola (F12) o que el backend esté en marcha.</p>';
        });
        return;
      }

      // Histórico
      if (subtitleEl) subtitleEl.textContent = 'Histórico: datos reales de la semana seleccionada';
      document.getElementById('estim-prev').disabled = false;
      document.getElementById('estim-next').disabled = false;
      setSourceBadge('', '');

      Promise.all([
        auth.fetchWithAuth('/api/dashboard/week?weekStart=' + ws).then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
        auth.fetchWithAuth('/api/predictions/by-week?weekStart=' + ws).then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
        auth.fetchWithAuth('/api/analytics/weather-impact?groupBy=day').then(function (r) { return safeJson(r, null); }).catch(function () { return null; })
      ]).then(function (data) {
        var dash = data[0], predHist = data[1], weatherImpact = data[2];

        renderWeatherImpact(weatherImpact);
        renderActions(ws);
        attachActionsHandlers();

        kpisEl.innerHTML = '';
        if (dash) {
          var kpis = [
            { label: 'Facturación real (semana)', value: fmtMoney(dash.totalRevenue) },
            { label: 'Productividad media', value: (dash.avgProductivity != null ? Number(dash.avgProductivity).toFixed(1) + ' €/h' : '—') },
            { label: 'Horas totales', value: (dash.totalHours != null ? Number(dash.totalHours).toFixed(0) + ' h' : '—') },
            { label: 'Coste personal', value: (dash.costePersonalPctFacturacion != null ? Number(dash.costePersonalPctFacturacion).toFixed(1) + '% (' + fmtMoney(dash.costePersonalEur) + ')' : '—') }
          ];
          kpis.forEach(function (k) { var div = document.createElement('div'); div.className = 'kpi-card'; div.innerHTML = '<div class="label">' + k.label + '</div><div class="value">' + k.value + '</div>'; kpisEl.appendChild(div); });
          predEl.innerHTML = '<h3>Semana seleccionada</h3>' + (dash.resumenTexto ? '<p class="estim-parrafo">' + dash.resumenTexto + '</p>' : '<p class="dashboard-subtitle">Sin resumen.</p>');
        } else {
          kpisEl.innerHTML = '<p class="loading">No hay datos para esta semana.</p>';
          predEl.innerHTML = '<h3>Semana seleccionada</h3><p class="dashboard-subtitle">No hay datos.</p>';
        }

        if (predHist && predHist.isSavedPrediction) {
          setSourceBadge('Guardada (histórico)', 'saved');
          predEl.innerHTML += '<p class="dashboard-subtitle">Predicción guardada: ' + fmtMoney(predHist.totalRevenue) + (predHist.completedAt ? (' · Evaluada: ' + predHist.completedAt) : '') + '</p>';
        }

        if (daysCardsEl && dash && dash.days && dash.days.length) {
          daysCardsEl.innerHTML = '<h3>Días reales (semana seleccionada)</h3><div class="estim-days-grid"></div>';
          var grid = daysCardsEl.querySelector('.estim-days-grid');
          dash.days.forEach(function (d) {
            var card = '<div class="estim-day-card" id="estim-day-' + d.date + '">' +
              '<div class="estim-day-header"><div class="estim-day-name">' + (d.dayName || '—') + '</div><div class="estim-day-date">' + (d.date || '') + '</div></div>' +
              '<div class="estim-day-revenue">' + fmtMoney(d.revenue) + '</div>' +
              '<div class="estim-day-range">Horas: ' + fmtNum(d.hoursWorked, 1) + ' · Prod: ' + fmtNum(d.productivity, 1) + ' €/h</div>' +
              '<div class="estim-day-staff">Personal: ' + (d.staffTotal != null ? d.staffTotal : '—') + '</div>' +
              '</div>';
            grid.insertAdjacentHTML('beforeend', card);
          });
        } else if (daysCardsEl) daysCardsEl.innerHTML = '';

        if (alertasEl) alertasEl.innerHTML = '';
        if (accuracyHistoryEl) accuracyHistoryEl.innerHTML = '';
      }).catch(function () {
        kpisEl.innerHTML = '<p class="error-msg">Error al cargar histórico.</p>';
      });
    }

    function setMode(newMode) {
      state.mode = newMode;
      var btnPlan = document.getElementById('estim-mode-plan');
      var btnHist = document.getElementById('estim-mode-hist');
      if (btnPlan && btnHist) {
        btnPlan.classList.toggle('estim-mode-btn--active', newMode === 'plan');
        btnHist.classList.toggle('estim-mode-btn--active', newMode === 'hist');
        btnPlan.setAttribute('aria-selected', newMode === 'plan' ? 'true' : 'false');
        btnHist.setAttribute('aria-selected', newMode === 'hist' ? 'true' : 'false');
      }
      if (weekInput) weekInput.style.display = newMode === 'hist' ? '' : 'none';
      load();
    }

    var btnModePlan = document.getElementById('estim-mode-plan');
    var btnModeHist = document.getElementById('estim-mode-hist');
    if (btnModePlan) btnModePlan.addEventListener('click', function () { setMode('plan'); });
    if (btnModeHist) btnModeHist.addEventListener('click', function () { setMode('hist'); });

    document.getElementById('estim-cargar').addEventListener('click', load);
    if (weekInput) weekInput.addEventListener('change', load);
    container.addEventListener('click', function (e) {
      if (e.target && e.target.id === 'estim-guardar-pred') {
        e.target.disabled = true;
        e.target.textContent = 'Guardando…';
        auth.fetchWithAuth('/api/predictions/next-week/save', { method: 'POST' }).then(function (r) {
          if (r && r.ok) return r.json();
          return null;
        }).then(function (res) {
          if (res && res.saved) load();
        }).catch(function () { }).finally(function () {
          var btn = document.getElementById('estim-guardar-pred');
          if (btn) { btn.disabled = false; btn.textContent = 'Guardar predicción semana siguiente'; }
        });
      }
    });
    document.getElementById('estim-prev').addEventListener('click', function () {
      if (state.mode !== 'hist') return;
      var ws = (weekInput && weekInput.value) || weekStart;
      weekInput.value = addDays(ws, -7);
      if (weekRangeEl) weekRangeEl.textContent = formatWeekRange(weekInput.value);
      load();
    });
    document.getElementById('estim-next').addEventListener('click', function () {
      if (state.mode !== 'hist') return;
      var ws = (weekInput && weekInput.value) || weekStart;
      weekInput.value = addDays(ws, 7);
      if (weekRangeEl) weekRangeEl.textContent = formatWeekRange(weekInput.value);
      load();
    });

    // Default: plan mode
    if (weekInput) weekInput.style.display = 'none';
    setMode('plan');
  }
  global.LUCAS_ESTIMACIONES_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
