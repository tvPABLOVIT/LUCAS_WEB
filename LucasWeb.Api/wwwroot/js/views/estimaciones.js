(function (global) {
  var auth = global.LUCAS_AUTH;
  function getWeekStart(d) { var date = typeof d === 'string' ? new Date(d) : new Date(d); var day = date.getDay(); var diff = date.getDate() - day + (day === 0 ? -6 : 1); var monday = new Date(date); monday.setDate(diff); return monday.getFullYear() + '-' + String(monday.getMonth() + 1).padStart(2, '0') + '-' + String(monday.getDate()).padStart(2, '0'); }
  function getISOWeekNumber(ymd) { var d = new Date(ymd + 'T12:00:00'); var dayNum = d.getDay() || 7; d.setDate(d.getDate() + 4 - dayNum); var yearStart = new Date(d.getFullYear(), 0, 1); return Math.ceil((((d - yearStart) / 86400000) + 1) / 7); }
  function formatWeekRange(ymd) { var d = new Date(ymd + 'T12:00:00'); var end = new Date(d); end.setDate(end.getDate() + 6); var fmt = function (x) { return String(x.getDate()).padStart(2, '0') + ' ' + ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'][x.getMonth()]; }; return fmt(d) + ' – ' + fmt(end) + ' ' + d.getFullYear(); }
  function isCurrentWeek(weekStartYmd) { return weekStartYmd === getWeekStart(new Date()); }
  function addDays(ymd, delta) { var d = new Date(ymd + 'T12:00:00'); d.setDate(d.getDate() + delta); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
  function render(container) {
    var weekStart = getWeekStart(new Date());
    var now = new Date();
    var state = {
      mode: 'plan', histWeekStart: weekStart, planWeekStart: null,
      weatherImpactPeriodType: 'last60',
      weatherImpactYear: now.getFullYear(),
      weatherImpactMonth: now.getMonth() + 1,
      weatherImpactQuarter: Math.floor(now.getMonth() / 3) + 1,
      weatherImpactGroupBy: 'day', weatherImpactMetric: 'revenue',
      weatherImpactThresholds: null
    };
    var lastWeatherImpact = null;
    var weatherDataRange = null;

    container.innerHTML =
      '<div class="estimaciones-view">' +
      '<div class="dashboard-title-row estimaciones-title-row">' +
      '<div class="dashboard-title-block">' +
      '<h2 class="view-title">Estimaciones</h2>' +
      '<p id="estim-subtitle" class="dashboard-subtitle">Planificación: predicción y plan para la semana siguiente</p>' +
      '</div>' +
      '<div class="dashboard-week-bar estimaciones-week-bar">' +
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
      '<div id="estim-weather-impact" class="card estim-card-compact estim-weather-impact-card"></div>' +
      '</div>' +
      '</div>';

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
      function getWeatherImpactFromTo() {
        var type = state.weatherImpactPeriodType || 'last60';
        var now = new Date();
        var from, to;
        if (type === 'last60') {
          var end = new Date(now);
          end.setDate(end.getDate() - 1);
          var start = new Date(end);
          start.setDate(start.getDate() - 59);
          from = start.getFullYear() + '-' + String(start.getMonth() + 1).padStart(2, '0') + '-' + String(start.getDate()).padStart(2, '0');
          to = end.getFullYear() + '-' + String(end.getMonth() + 1).padStart(2, '0') + '-' + String(end.getDate()).padStart(2, '0');
          return { from: from, to: to };
        }
        var y = state.weatherImpactYear != null ? Number(state.weatherImpactYear) : now.getFullYear();
        if (isNaN(y) || y < 2020 || y > now.getFullYear()) y = now.getFullYear();
        if (type === 'month') {
          var m = state.weatherImpactMonth != null ? Math.min(12, Math.max(1, Number(state.weatherImpactMonth))) : 1;
          from = y + '-' + String(m).padStart(2, '0') + '-01';
          var lastDay = new Date(y, m, 0).getDate();
          to = y + '-' + String(m).padStart(2, '0') + '-' + String(lastDay).padStart(2, '0');
        } else if (type === 'quarter') {
          var q = state.weatherImpactQuarter != null ? Math.min(4, Math.max(1, Number(state.weatherImpactQuarter))) : 1;
          var startMonth = (q - 1) * 3 + 1;
          var endMonth = q * 3;
          from = y + '-' + String(startMonth).padStart(2, '0') + '-01';
          var endDay = new Date(y, endMonth, 0).getDate();
          to = y + '-' + String(endMonth).padStart(2, '0') + '-' + String(endDay).padStart(2, '0');
        } else {
          from = y + '-01-01';
          to = y + '-12-31';
        }
        return { from: from, to: to };
      }
      function buildWeatherImpactUrl() {
        var range = getWeatherImpactFromTo();
        var gb = state.weatherImpactGroupBy || 'day';
        var url = '/api/analytics/weather-impact?groupBy=' + encodeURIComponent(gb) + '&from=' + encodeURIComponent(range.from) + '&to=' + encodeURIComponent(range.to);
        var th = state.weatherImpactThresholds;
        if (th) {
          if (th.rainyPrecipMm != null && th.rainyPrecipMm !== '') url += '&rainyPrecipMm=' + encodeURIComponent(th.rainyPrecipMm);
          if (th.heavyRainMm != null && th.heavyRainMm !== '') url += '&heavyRainMm=' + encodeURIComponent(th.heavyRainMm);
          if (th.windyKmh != null && th.windyKmh !== '') url += '&windyKmh=' + encodeURIComponent(th.windyKmh);
          if (th.coldC != null && th.coldC !== '') url += '&coldC=' + encodeURIComponent(th.coldC);
          if (th.hotC != null && th.hotC !== '') url += '&hotC=' + encodeURIComponent(th.hotC);
        }
        return url;
      }
      function applySettingsToWeatherThresholds(settings) {
        if (!settings) return;
        state.weatherImpactThresholds = {
          rainyPrecipMm: (settings.WeatherImpactRainyPrecipMm != null && settings.WeatherImpactRainyPrecipMm !== '') ? parseFloat(settings.WeatherImpactRainyPrecipMm) : null,
          heavyRainMm: (settings.WeatherImpactHeavyRainMm != null && settings.WeatherImpactHeavyRainMm !== '') ? parseFloat(settings.WeatherImpactHeavyRainMm) : null,
          windyKmh: (settings.WeatherImpactWindyKmh != null && settings.WeatherImpactWindyKmh !== '') ? parseFloat(settings.WeatherImpactWindyKmh) : null,
          coldC: (settings.WeatherImpactColdC != null && settings.WeatherImpactColdC !== '') ? parseFloat(settings.WeatherImpactColdC) : null,
          hotC: (settings.WeatherImpactHotC != null && settings.WeatherImpactHotC !== '') ? parseFloat(settings.WeatherImpactHotC) : null
        };
      }
      function loadWeatherImpact(cb) {
        auth.fetchWithAuth(buildWeatherImpactUrl()).then(function (r) { return (r && r.ok ? r.json() : Promise.resolve(null)).catch(function () { return null; }); })
          .then(function (data) { lastWeatherImpact = data; renderWeatherImpact(data); if (typeof cb === 'function') cb(); });
      }
      function renderWeatherImpact(weatherImpact) {
        if (!weatherImpactEl) return;
        try {
        if (weatherImpact && typeof weatherImpact !== 'object') weatherImpact = null;
        var sampleCount = (weatherImpact && weatherImpact.sampleCount != null) ? Number(weatherImpact.sampleCount) : 0;
        function fmtPct(x) { if (x == null || isNaN(Number(x))) return '—'; var n = Number(x); return (n > 0 ? '+' : '') + n.toFixed(0) + '%'; }
        function cellClass(val) {
          if (val == null || isNaN(Number(val))) return '';
          var n = Number(val);
          return n > 0 ? ' estim-weather-delta--up' : (n < 0 ? ' estim-weather-delta--down' : '');
        }
        var metric = state.weatherImpactMetric || 'revenue';
        var periodType = state.weatherImpactPeriodType || 'last60';
        var yearMin = 2020;
        var yearMax = new Date().getFullYear();
        var periodYear = state.weatherImpactYear != null ? Number(state.weatherImpactYear) : yearMax;
        if (isNaN(periodYear) || periodYear < yearMin || periodYear > yearMax) { periodYear = yearMax; state.weatherImpactYear = yearMax; }
        var periodMonth = Math.min(12, Math.max(1, state.weatherImpactMonth != null ? state.weatherImpactMonth : 1));
        var periodQuarter = Math.min(4, Math.max(1, state.weatherImpactQuarter != null ? state.weatherImpactQuarter : 1));
        var monthNames = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
        var quarterLabels = ['T1 (Ene–Mar)', 'T2 (Abr–Jun)', 'T3 (Jul–Sep)', 'T4 (Oct–Dic)'];
        var dataMin = weatherDataRange && weatherDataRange.minDate ? weatherDataRange.minDate : null;
        var dataMax = weatherDataRange && weatherDataRange.maxDate ? weatherDataRange.maxDate : null;
        function periodHasData(type, year, monthOrQuarter) {
          if (!dataMin || !dataMax) return true;
          var periodStart, periodEnd;
          function pad2(n) { return String(n).padStart(2, '0'); }
          if (type === 'month') {
            var m = monthOrQuarter;
            periodStart = year + '-' + pad2(m) + '-01';
            var lastD = new Date(year, m, 0).getDate();
            periodEnd = year + '-' + pad2(m) + '-' + pad2(lastD);
          } else if (type === 'quarter') {
            var q = monthOrQuarter;
            var startM = (q - 1) * 3 + 1, endM = q * 3;
            periodStart = year + '-' + pad2(startM) + '-01';
            var endLastD = new Date(year, endM, 0).getDate();
            periodEnd = year + '-' + pad2(endM) + '-' + pad2(endLastD);
          } else {
            periodStart = year + '-01-01';
            periodEnd = year + '-12-31';
          }
          return periodStart <= dataMax && periodEnd >= dataMin;
        }
        var validYears = [];
        for (var yr = yearMax; yr >= yearMin; yr--) {
          if (periodHasData('year', yr)) validYears.push(yr);
        }
        var validMonths = [];
        for (var m = 1; m <= 12; m++) {
          if (periodHasData('month', periodYear, m)) validMonths.push(m);
        }
        var validQuarters = [];
        for (var q = 1; q <= 4; q++) {
          if (periodHasData('quarter', periodYear, q)) validQuarters.push(q);
        }
        var snapped = false;
        if (validYears.length > 0) {
          if (validYears.indexOf(periodYear) === -1) {
            state.weatherImpactYear = periodYear = validYears[0];
            snapped = true;
          }
          if (periodType === 'month' && validMonths.length > 0 && validMonths.indexOf(periodMonth) === -1) {
            state.weatherImpactMonth = periodMonth = validMonths[0];
            snapped = true;
          }
          if (periodType === 'quarter' && validQuarters.length > 0 && validQuarters.indexOf(periodQuarter) === -1) {
            state.weatherImpactQuarter = periodQuarter = validQuarters[0];
            snapped = true;
          }
        }
        var yearOpts = (validYears.length > 0 ? validYears : (function () { var a = []; for (var y = yearMax; y >= yearMin; y--) a.push(y); return a; })()).map(function (yr) {
          return '<option value="' + yr + '"' + (periodYear === yr ? ' selected' : '') + '>' + yr + '</option>';
        }).join('');
        var monthOpts = (validMonths.length > 0 ? validMonths : [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]).map(function (m) {
          return '<option value="' + m + '"' + (periodMonth === m ? ' selected' : '') + '>' + monthNames[m - 1] + '</option>';
        }).join('');
        var quarterOpts = (validQuarters.length > 0 ? validQuarters : [1, 2, 3, 4]).map(function (q) {
          return '<option value="' + q + '"' + (periodQuarter === q ? ' selected' : '') + '>' + quarterLabels[q - 1] + '</option>';
        }).join('');

        if (!weatherImpact || sampleCount < 10) {
          if (snapped && validYears.length > 0) {
            auth.fetchWithAuth(buildWeatherImpactUrl()).then(function (r) { return (r && r.ok ? r.json() : Promise.resolve(null)).catch(function () { return null; }); })
              .then(function (data) { lastWeatherImpact = data; renderWeatherImpact(data); });
            return;
          }
          var periodLabelNoData = periodType === 'last60' ? 'Últimos 60 días' : (periodType === 'month' ? (monthNames[periodMonth - 1] + ' ' + periodYear) : (periodType === 'quarter' ? ('T' + periodQuarter + ' ' + periodYear) : ('' + periodYear)));
          var periodSelectorsNoData =
            '<label class="estim-weather-selector-label">Período: <select id="estim-weather-period-type" class="estim-weather-select"><option value="last60"' + (periodType === 'last60' ? ' selected' : '') + '>Últimos 60 días</option><option value="month"' + (periodType === 'month' ? ' selected' : '') + '>Mes</option><option value="quarter"' + (periodType === 'quarter' ? ' selected' : '') + '>Trimestre</option><option value="year"' + (periodType === 'year' ? ' selected' : '') + '>Anual</option></select></label>' +
            '<label class="estim-weather-selector-label estim-weather-period-year" id="estim-weather-year-wrap"><span>Año:</span> <select id="estim-weather-year" class="estim-weather-select">' + yearOpts + '</select></label>' +
            '<label class="estim-weather-selector-label estim-weather-period-month" id="estim-weather-month-wrap"><span>Mes:</span> <select id="estim-weather-month" class="estim-weather-select">' + monthOpts + '</select></label>' +
            '<label class="estim-weather-selector-label estim-weather-period-quarter" id="estim-weather-quarter-wrap"><span>Trimestre:</span> <select id="estim-weather-quarter" class="estim-weather-select">' + quarterOpts + '</select></label>';
          weatherImpactEl.innerHTML =
            '<div class="estim-weather-header"><h3 class="estim-weather-title">☁ Impacto del clima</h3></div>' +
            '<p class="dashboard-subtitle">Para el período <strong>' + periodLabelNoData + '</strong> no hay suficientes datos (mín. 10 días con facturación). Cambia el período a continuación o ejecuta backfill.</p>' +
            '<div class="estim-weather-selectors">' + periodSelectorsNoData + '</div>' +
            '<button type="button" class="btn-primary btn-sm estim-weather-backfill-btn">Backfill clima (180 días)</button>' +
            '<div id="estim-weather-backfill-status" class="estim-weather-backfill-status"></div>';
          var yearWrapNd = document.getElementById('estim-weather-year-wrap');
          var monthWrapNd = document.getElementById('estim-weather-month-wrap');
          var quarterWrapNd = document.getElementById('estim-weather-quarter-wrap');
          if (yearWrapNd) yearWrapNd.style.display = periodType === 'last60' ? 'none' : '';
          if (monthWrapNd) monthWrapNd.style.display = periodType === 'month' ? '' : 'none';
          if (quarterWrapNd) quarterWrapNd.style.display = periodType === 'quarter' ? '' : 'none';
          function refetchWeatherImpactNoData() {
            auth.fetchWithAuth(buildWeatherImpactUrl()).then(function (r) { return (r && r.ok ? r.json() : Promise.resolve(null)).catch(function () { return null; }); })
              .then(function (data) { lastWeatherImpact = data; renderWeatherImpact(data); });
          }
          var ptSel = document.getElementById('estim-weather-period-type');
          if (ptSel) ptSel.onchange = function () { state.weatherImpactPeriodType = ptSel.value || 'last60'; refetchWeatherImpactNoData(); };
          var ySel = document.getElementById('estim-weather-year');
          if (ySel) ySel.onchange = function () { state.weatherImpactYear = parseInt(ySel.value, 10); refetchWeatherImpactNoData(); };
          var mSel = document.getElementById('estim-weather-month');
          if (mSel) mSel.onchange = function () { state.weatherImpactMonth = parseInt(mSel.value, 10); refetchWeatherImpactNoData(); };
          var qSel = document.getElementById('estim-weather-quarter');
          if (qSel) qSel.onchange = function () { state.weatherImpactQuarter = parseInt(qSel.value, 10); refetchWeatherImpactNoData(); };
        } else {
          var cov = weatherImpact.coverage || null;
          var covHint = '';
          if (cov && cov.withAnyWeather != null && Number(cov.withAnyWeather) < 10) {
            covHint = '<p class="dashboard-subtitle estim-weather-alert"><strong>Faltan datos de clima en el histórico</strong> (solo ' + Number(cov.withAnyWeather) + ' registros con clima).</p>' +
              '<button type="button" class="btn-primary btn-sm estim-weather-backfill-btn">Backfill clima (180 días)</button>' +
              '<div id="estim-weather-backfill-status" class="estim-weather-backfill-status"></div>';
          }
          var th = weatherImpact.thresholdsUsed || {};
          var hotC = th.hotC != null ? Number(th.hotC) : 30;
          var coldC = th.coldC != null ? Number(th.coldC) : 5;
          var rainyPrecipMm = th.rainyPrecipMm != null ? Number(th.rainyPrecipMm) : 0.5;
          var rainyMm = th.heavyRainMm != null ? Number(th.heavyRainMm) : 5;
          var windKmh = th.windyKmh != null ? Number(th.windyKmh) : 35;
          var groupLabel = (weatherImpact.groupBy === 'shift') ? 'turno' : 'día';
          var fromTo = (weatherImpact.from && weatherImpact.to) ? (weatherImpact.from + ' – ' + weatherImpact.to) : '';
          var rainyByDow = Array.isArray(weatherImpact.rainyByDow) ? weatherImpact.rainyByDow : [];
          var heavyRainByDow = Array.isArray(weatherImpact.heavyRainByDow) ? weatherImpact.heavyRainByDow : [];
          var windyByDow = Array.isArray(weatherImpact.windyByDow) ? weatherImpact.windyByDow : [];
          var extremeHighByDow = Array.isArray(weatherImpact.extremeTempHighByDow) ? weatherImpact.extremeTempHighByDow : [];
          var extremeLowByDow = Array.isArray(weatherImpact.extremeTempLowByDow) ? weatherImpact.extremeTempLowByDow : [];

          var currentMetric = state.weatherImpactMetric || 'revenue';
          function getPct(obj) { return obj ? (currentMetric === 'productivity' ? obj.diffPctProductivity : obj.diffPctRevenue) : null; }
          function cellHtml(rowIndex, arr) {
            var item = arr[rowIndex];
            if (!item) return '<td>—</td>';
            var val = getPct(item);
            var txt = fmtPct(val);
            var n = item.count != null ? item.count : 0;
            var ref = item.baselineCount != null ? item.baselineCount : 0;
            var totalDow = n + ref;
            var countLabel = n + ' día' + (n !== 1 ? 's' : '');
            var title = countLabel + ' con esta condición · ' + totalDow + ' ' + (totalDow !== 1 ? 'días' : 'día') + ' de este día en la muestra';
            var countText = totalDow > 0 ? '(' + n + ' vs ' + totalDow + ')' : '(' + n + ')';
            var cls = cellClass(val);
            var pocosDatos = n > 0 && n < 5 ? ' <span class="estim-weather-pocos-datos" title="Menos de 5 días: resultado poco representativo">⚠</span>' : '';
            var cellContent = txt + ' <span class="estim-weather-cell-count" title="' + title + '">' + countText + '</span>' + pocosDatos;
            return '<td class="estim-weather-cell' + cls + '">' + cellContent + '</td>';
          }
          var agg = {
            rainy: weatherImpact.rainy,
            heavyRain: weatherImpact.heavyRain,
            windy: weatherImpact.windy,
            extremeHigh: weatherImpact.extremeTempHigh,
            extremeLow: weatherImpact.extremeTempLow
          };
          var condLabels = ['Días lluviosos', 'Lluvia intensa', 'Viento fuerte', 'Más de ' + hotC + ' °C', 'Menos de ' + coldC + ' °C'];
          var condKeys = ['rainy', 'heavyRain', 'windy', 'extremeHigh', 'extremeLow'];
          var barChartHtml = '<div class="estim-weather-bars">';
          condKeys.forEach(function (k, i) {
            var o = agg[k];
            var pct = o ? getPct(o) : null;
            var num = (pct != null && !isNaN(Number(pct))) ? Number(pct) : 0;
            var width = Math.min(100, Math.abs(num) * 2);
            var barCls = num > 0 ? 'estim-weather-bar--up' : (num < 0 ? 'estim-weather-bar--down' : '');
            var pctCls = num > 0 ? ' estim-weather-delta--up' : (num < 0 ? ' estim-weather-delta--down' : '');
            barChartHtml += '<div class="estim-weather-bar-row"><span class="estim-weather-bar-label">' + condLabels[i] + '</span><div class="estim-weather-bar-track"><div class="estim-weather-bar-fill ' + barCls + '" style="width:' + width + '%"></div></div><span class="estim-weather-bar-pct' + pctCls + '">' + fmtPct(pct) + '</span></div>';
          });
          barChartHtml += '</div>';

          var thead = '<thead><tr><th class="estim-weather-th-day">Día</th><th>Días lluviosos</th><th>Lluvia intensa</th><th>Viento fuerte</th><th>Más de ' + hotC + ' °C</th><th>Menos de ' + coldC + ' °C</th></tr></thead>';
          var tbody = '<tbody>';
          var dayNames = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
          for (var r = 0; r < 7; r++) {
            var dowName = (rainyByDow[r] && rainyByDow[r].dowName) || (heavyRainByDow[r] && heavyRainByDow[r].dowName) || (windyByDow[r] && windyByDow[r].dowName) || (extremeHighByDow[r] && extremeHighByDow[r].dowName) || (extremeLowByDow[r] && extremeLowByDow[r].dowName) || dayNames[r];
            tbody += '<tr><td class="estim-weather-td-day">' + dowName + '</td>';
            tbody += cellHtml(r, rainyByDow) + cellHtml(r, heavyRainByDow) + cellHtml(r, windyByDow) + cellHtml(r, extremeHighByDow) + cellHtml(r, extremeLowByDow);
            tbody += '</tr>';
          }
          tbody += '</tbody>';

          var metricLabel = metric === 'productivity' ? 'Productividad' : 'Facturación';
          var chartBlock = '<div class="estim-weather-chart-col"><div class="estim-weather-chart-title">Resumen por condición (promedio)</div>' + barChartHtml + '</div>';
          var periodSelectors =
            '<label class="estim-weather-selector-label">Período: <select id="estim-weather-period-type" class="estim-weather-select"><option value="last60"' + (periodType === 'last60' ? ' selected' : '') + '>Últimos 60 días</option><option value="month"' + (periodType === 'month' ? ' selected' : '') + '>Mes</option><option value="quarter"' + (periodType === 'quarter' ? ' selected' : '') + '>Trimestre</option><option value="year"' + (periodType === 'year' ? ' selected' : '') + '>Anual</option></select></label>' +
            '<label class="estim-weather-selector-label estim-weather-period-year" id="estim-weather-year-wrap">Año: <select id="estim-weather-year" class="estim-weather-select">' + yearOpts + '</select></label>' +
            '<label class="estim-weather-selector-label estim-weather-period-month" id="estim-weather-month-wrap">Mes: <select id="estim-weather-month" class="estim-weather-select">' + monthOpts + '</select></label>' +
            '<label class="estim-weather-selector-label estim-weather-period-quarter" id="estim-weather-quarter-wrap">Trimestre: <select id="estim-weather-quarter" class="estim-weather-select">' + quarterOpts + '</select></label>';
          var periodLabelText = periodType === 'last60' ? 'Últimos 60 días' : (periodType === 'month' ? (monthNames[periodMonth - 1] + ' ' + periodYear) : (periodType === 'quarter' ? ('T' + periodQuarter + ' ' + periodYear) : ('' + periodYear)));
          var headerBlock =
            '<div class="estim-weather-header">' +
            '<h3 class="estim-weather-title">☁ Impacto del clima</h3>' +
            '<span class="estim-weather-badge">Por día de la semana</span></div>' +
            '<div class="estim-weather-selectors">' +
            periodSelectors +
            '<span class="estim-weather-metric-toggle" role="tablist"><button type="button" class="estim-weather-metric-btn' + (metric === 'revenue' ? ' estim-weather-metric-btn--active' : '') + '" data-metric="revenue">Facturación</button><button type="button" class="estim-weather-metric-btn' + (metric === 'productivity' ? ' estim-weather-metric-btn--active' : '') + '" data-metric="productivity">Productividad</button></span>' +
            '</div>' +
            '<p class="estim-weather-range">' + periodLabelText + (fromTo ? ' · ' + fromTo : '') + '</p>' +
            '<p class="estim-weather-baseline">Basado en <strong>' + (weatherImpact.sampleCount || 0) + '</strong> ' + groupLabel + 's del período seleccionado. Cada fila compara solo ese día de la semana (lunes con lunes, martes con martes, etc.). Mostrando: <strong>' + metricLabel + '</strong>.</p>' +
            covHint;
          weatherImpactEl.innerHTML =
            '<div class="estim-weather-body">' +
            '<div class="estim-weather-top-section">' +
            '<div class="estim-weather-header-zone">' + headerBlock + '</div>' +
            '<div class="estim-weather-summary-above-table">' + chartBlock + '</div>' +
            '</div>' +
            '<div class="estim-weather-table-wrap"><table class="estim-weather-table">' + thead + tbody + '</table></div>' +
            '<p class="estim-weather-explicacion">Un % negativo indica que la facturación (o productividad) fue menor en días con esa condición respecto al mismo día de la semana sin ella.</p>' +
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

        function refetchWeatherImpact() {
          auth.fetchWithAuth(buildWeatherImpactUrl()).then(function (r) { return (r && r.ok ? r.json() : Promise.resolve(null)).catch(function () { return null; }); })
            .then(function (data) { lastWeatherImpact = data; renderWeatherImpact(data); });
        }
        var periodTypeSel = document.getElementById('estim-weather-period-type');
        if (periodTypeSel) {
          periodTypeSel.onchange = function () {
            state.weatherImpactPeriodType = periodTypeSel.value || 'last60';
            refetchWeatherImpact();
          };
        }
        var yearSel = document.getElementById('estim-weather-year');
        if (yearSel) {
          yearSel.onchange = function () {
            state.weatherImpactYear = parseInt(yearSel.value, 10);
            refetchWeatherImpact();
          };
        }
        var monthSel = document.getElementById('estim-weather-month');
        if (monthSel) {
          monthSel.onchange = function () {
            state.weatherImpactMonth = parseInt(monthSel.value, 10);
            refetchWeatherImpact();
          };
        }
        var quarterSel = document.getElementById('estim-weather-quarter');
        if (quarterSel) {
          quarterSel.onchange = function () {
            state.weatherImpactQuarter = parseInt(quarterSel.value, 10);
            refetchWeatherImpact();
          };
        }
        var yearWrap = document.getElementById('estim-weather-year-wrap');
        var monthWrap = document.getElementById('estim-weather-month-wrap');
        var quarterWrap = document.getElementById('estim-weather-quarter-wrap');
        if (yearWrap) yearWrap.style.display = periodType === 'last60' ? 'none' : '';
        if (monthWrap) monthWrap.style.display = periodType === 'month' ? '' : 'none';
        if (quarterWrap) quarterWrap.style.display = periodType === 'quarter' ? '' : 'none';
        weatherImpactEl.querySelectorAll('.estim-weather-metric-btn').forEach(function (btn) {
          btn.onclick = function () {
            var m = btn.getAttribute('data-metric');
            if (!m) return;
            state.weatherImpactMetric = m;
            weatherImpactEl.querySelectorAll('.estim-weather-metric-btn').forEach(function (b) { b.classList.toggle('estim-weather-metric-btn--active', b.getAttribute('data-metric') === m); });
            if (lastWeatherImpact) renderWeatherImpact(lastWeatherImpact);
          };
        });
        } catch (e) {
          if (typeof console !== 'undefined' && console.error) console.error('renderWeatherImpact:', e.message || e, e);
          weatherImpactEl.innerHTML = '<div class="estim-weather-header"><h3 class="estim-weather-title">☁ Impacto del clima</h3></div><p class="dashboard-subtitle">Error al cargar el bloque. Revisa la consola (F12).</p><button type="button" class="btn-secondary btn-sm" id="estim-weather-retry">Reintentar</button>';
          var retryBtn = document.getElementById('estim-weather-retry');
          if (retryBtn) retryBtn.onclick = function () { load(); };
        }
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
        var predContent = parrafo ? '<div class="estim-pred-parrafo-wrap"><p class="estim-parrafo estim-parrafo--centrado">' + parrafo + '</p></div>' : '<div class="estim-pred-parrafo-wrap"><p class="estim-parrafo estim-parrafo--centrado estim-parrafo--muted">Sin datos de predicción.</p></div>';
        if (nextWeekRange) predContent += '<p class="estim-parrafo estim-parrafo--centrado estim-parrafo--muted estim-parrafo--small">Esta es la <strong>semana siguiente</strong>. En Dashboard, selecciona esta misma semana en el selector de fechas para ver la misma predicción y comparar con realidad.</p>';
        predEl.innerHTML = '<h3>Predicción</h3>' + predContent;

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
        predEl.innerHTML = '<h3>Predicción</h3><p class="loading">Cargando…</p>';
        if (daysCardsEl) daysCardsEl.innerHTML = '';
        if (alertasEl) alertasEl.innerHTML = mode === 'plan' ? '<p class="loading">Cargando alertas…</p>' : '';
        if (accuracyHistoryEl) accuracyHistoryEl.innerHTML = mode === 'plan' ? '<p class="loading">Cargando historial…</p>' : '';
        if (weatherImpactEl) weatherImpactEl.innerHTML = '<p class="loading">Cargando impacto del clima…</p>';
        if (actionsEl) actionsEl.innerHTML = '<p class="loading">Cargando acciones…</p>';
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
          auth.fetchWithAuth(buildWeatherImpactUrl()).then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
          auth.fetchWithAuth('/api/predictions/accuracy-history?limit=20').then(function (r) { return safeJson(r, { weeks: [] }); }).catch(function () { return { weeks: [] }; }),
          auth.fetchWithAuth('/api/analytics/data-range').then(function (r) { return safeJson(r, null); }).catch(function () { return null; })
        ]).then(function (data) {
          var pred = data[0], settings = data[1], alertasResp = data[2], weatherImpact = data[3], accuracyHistory = data[4];
          weatherDataRange = data[5] && (data[5].minDate || data[5].maxDate) ? data[5] : null;
          applySettingsToWeatherThresholds(settings);

          var comfortBySchema = {}; var comfortByCocina = {};

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
        auth.fetchWithAuth(buildWeatherImpactUrl()).then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
        auth.fetchWithAuth('/api/analytics/data-range').then(function (r) { return safeJson(r, null); }).catch(function () { return null; }),
        auth.fetchWithAuth('/api/settings').then(function (r) { return safeJson(r, null); }).catch(function () { return null; })
      ]).then(function (data) {
        var dash = data[0], predHist = data[1], weatherImpact = data[2];
        weatherDataRange = data[3] && (data[3].minDate || data[3].maxDate) ? data[3] : null;
        applySettingsToWeatherThresholds(data[4]);

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
