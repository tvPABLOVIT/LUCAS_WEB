(function (global) {
  var auth = global.LUCAS_AUTH;
  var DAY_NAMES = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
  function escapeHtml(s) {
    if (s == null || s === undefined) return '';
    var div = document.createElement('div');
    div.textContent = String(s);
    return div.innerHTML;
  }
  function getWeekStart(d) {
    var date = typeof d === 'string' ? new Date(d + 'T12:00:00') : new Date(d);
    var day = date.getDay();
    var diff = date.getDate() - day + (day === 0 ? -6 : 1);
    var monday = new Date(date);
    monday.setDate(diff);
    return monday.getFullYear() + '-' + String(monday.getMonth() + 1).padStart(2, '0') + '-' + String(monday.getDate()).padStart(2, '0');
  }
  function addDays(ymd, delta) {
    var d = new Date(ymd + 'T12:00:00');
    d.setDate(d.getDate() + delta);
    return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  }
  function dayNameFromDate(ymd) {
    var d = new Date(ymd + 'T12:00:00');
    return DAY_NAMES[d.getDay()];
  }
  function formatDateShort(ymd) {
    var d = new Date(ymd + 'T12:00:00');
    return String(d.getDate()).padStart(2, '0') + '/' + String(d.getMonth() + 1).padStart(2, '0');
  }
  function formatWeekRange(weekStartYmd) {
    var start = new Date(weekStartYmd + 'T12:00:00');
    var end = new Date(start);
    end.setDate(end.getDate() + 6);
    var fmt = function (d) { return String(d.getDate()).padStart(2, '0') + ' ' + ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'][d.getMonth()]; };
    return fmt(start) + ' – ' + fmt(end) + ' ' + start.getFullYear();
  }
  function isCurrentWeek(weekStartYmd) {
      var mon = new Date(weekStartYmd + 'T12:00:00');
      var today = new Date();
      var todayMon = getWeekStart(today);
      return weekStartYmd === todayMon;
    }
  function getISOWeekNumber(ymd) {
    var d = new Date(ymd + 'T12:00:00');
    var dayNum = d.getDay() || 7;
    d.setDate(d.getDate() + 4 - dayNum);
    var yearStart = new Date(d.getFullYear(), 0, 1);
    return Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
  }
  function render(container) {
    var today = new Date();
    var weekStart = getWeekStart(today);
    function weekEndFromWeekStart(ws) {
      var d = new Date(ws + 'T12:00:00');
      d.setDate(d.getDate() + 6);
      return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }
    container.innerHTML =
      '<div class="dashboard-title-row">' +
      '<div class="dashboard-title-block">' +
      '<h2 class="view-title">Dashboard</h2>' +
      '<p id="dashboard-subtitle" class="dashboard-subtitle">Datos de la semana seleccionada</p>' +
      '</div>' +
      '<div class="dashboard-week-bar">' +
      '<button type="button" id="dashboard-cargar" class="btn-primary dashboard-week-btn-actualizar">Actualizar</button>' +
      '<div class="dashboard-week-nav">' +
      '<button type="button" id="dashboard-prev" class="dashboard-week-arrow" title="Semana anterior">◀</button>' +
      '<span id="dashboard-week-range" class="dashboard-week-range dashboard-week-range--clickable" title="Seleccionar semana">' + formatWeekRange(weekStart) + '</span>' +
      '<button type="button" id="dashboard-next" class="dashboard-week-arrow" title="Semana siguiente">▶</button>' +
      '</div>' +
      '<span id="dashboard-semana-en-curso" class="dashboard-week-status hidden"></span>' +
      '<input type="date" id="dashboard-week-start" class="dashboard-week-input-hidden" value="' + weekStart + '" aria-hidden="true" tabindex="-1" />' +
      '</div>' +
      '</div>' +
      '<div id="dashboard-kpis" class="kpi-grid"></div>' +
      '<div id="dashboard-week-triad" class="card dashboard-week-triad-card"></div>' +
      '<div id="dashboard-days-wrap" class="card"><h3>Días de la semana</h3><div id="dashboard-days-table-wrap"></div><div id="dashboard-days-cards-wrap" class="dashboard-days-cards-wrap"></div></div>' +
      '<div id="dashboard-resumen" class="card"></div>' +
      '<div class="card dashboard-import-card">' +
      '<h3>Importar datos</h3>' +
      '<div class="dashboard-import-row">' +
      '<div class="dashboard-import-item">' +
      '<label class="dashboard-import-label">Excel (facturación + horas reales por turno). Para varios archivos: mantén Ctrl (Cmd en Mac) y haz clic en cada archivo.</label>' +
      '<input type="file" id="dashboard-excel-file" accept=".xlsx,.xls" class="dashboard-import-input" multiple="multiple" />' +
      '<button type="button" id="dashboard-import-excel" class="btn-secondary btn-sm">Cargar Excel (uno o varios)</button>' +
      '<span id="dashboard-excel-status" class="dashboard-import-status"></span>' +
      '</div>' +
      '<div class="dashboard-import-item">' +
      '<label class="dashboard-import-label">PDF cuadrante (personal y horas programadas por turno)</label>' +
      '<input type="file" id="dashboard-pdf-file" accept=".pdf,application/pdf" class="dashboard-import-input" />' +
      '<button type="button" id="dashboard-import-pdf" class="btn-secondary btn-sm">Cargar PDF</button>' +
      '<span id="dashboard-pdf-status" class="dashboard-import-status"></span>' +
      '</div>' +
      '</div>' +
      '</div>' +
      '<div id="dashboard-bloque-ampliacion" class="card dashboard-bloque-ampliacion">' +
      '<h3 class="dashboard-bloque-ampliacion-title">Facturación últimos 30 días</h3>' +
      '<div id="dashboard-chart-30d" class="dashboard-chart-30d"><p class="loading">Cargando…</p></div>' +
      '</div>';
    var weekInput = document.getElementById('dashboard-week-start');
    var weekRangeEl = document.getElementById('dashboard-week-range');
    var badgeEl = document.getElementById('dashboard-semana-en-curso');
    var kpisEl = document.getElementById('dashboard-kpis');
    var triadEl = document.getElementById('dashboard-week-triad');
    var resumenEl = document.getElementById('dashboard-resumen');
    var daysWrap = document.getElementById('dashboard-days-table-wrap');
    var daysCardsWrap = document.getElementById('dashboard-days-cards-wrap');
    var loading = false;

    function isDashboardVisible() { return !!document.getElementById('dashboard-cargar'); }

    // Asegurar 4 KPIs en una fila (evita depender de caché CSS).
    if (kpisEl) {
      kpisEl.style.gridTemplateColumns = 'repeat(4, minmax(0, 1fr))';
    }

    // Selector de semana: click sobre el rango abre el date picker, como en Registro/Preguntas.
    if (weekRangeEl && weekInput) {
      weekRangeEl.addEventListener('click', function () {
        try {
          if (typeof weekInput.showPicker === 'function') weekInput.showPicker();
          else weekInput.click();
        } catch (e) { try { weekInput.click(); } catch (e2) { } }
      });
    }
    function load() {
      if (loading) return;
      loading = true;
      var btnCargar = document.getElementById('dashboard-cargar');
      if (btnCargar) btnCargar.disabled = true;

      if (!kpisEl || !resumenEl || !daysWrap) {
        loading = false;
        if (btnCargar) btnCargar.disabled = false;
        return;
      }

      var ws = (weekInput && weekInput.value) || weekStart;
      if (weekRangeEl) { weekRangeEl.textContent = 'Cargando…'; weekRangeEl.classList.add('dashboard-week-range--loading'); }
      if (badgeEl) {
        if (isCurrentWeek(ws)) { badgeEl.textContent = 'En curso'; badgeEl.classList.remove('hidden'); badgeEl.classList.add('dashboard-badge--current'); }
        else { badgeEl.textContent = ''; badgeEl.classList.add('hidden'); badgeEl.classList.remove('dashboard-badge--current'); }
      }
      kpisEl.innerHTML = '<p class="loading">Cargando…</p>';
      if (triadEl) triadEl.innerHTML = '';
      resumenEl.innerHTML = '';
      daysWrap.innerHTML = '';
      var todayYmd = (function () { var t = new Date(); return t.getFullYear() + '-' + String(t.getMonth() + 1).padStart(2, '0') + '-' + String(t.getDate()).padStart(2, '0'); })();
      var we = weekEndFromWeekStart(ws);
      var currentMon = getWeekStart(new Date());
      var prevMon = addDays(currentMon, -7);
      var nextMon = addDays(currentMon, 7);
      auth.fetchWithAuth('/api/estimaciones/comparativas?weekStart=' + encodeURIComponent(ws) + '&asOf=' + encodeURIComponent(todayYmd) + '&mode=plan').then(function (r) {
        if (!r || !r.ok) return null;
        return r.json();
      }).catch(function () { return null; }).then(function (comparativas) {
        return Promise.all([
          auth.fetchWithAuth('/api/dashboard/week?weekStart=' + encodeURIComponent(ws) + '&asOf=' + encodeURIComponent(todayYmd)).then(function (r) {
            if (r.status === 401) {
              if (global.LUCAS_APP && global.LUCAS_APP.onUnauthorized) global.LUCAS_APP.onUnauthorized();
              return null;
            }
            if (!r.ok) throw new Error('Error al cargar');
            return r.json();
          }),
          auth.fetchWithAuth('/api/events?from=' + encodeURIComponent(ws) + '&to=' + encodeURIComponent(we)).then(function (r2) {
            if (r2.status === 401) return null;
            if (r2.status === 403) return [];
            if (!r2.ok) return [];
            return r2.json();
          }).catch(function () { return []; }),
          Promise.resolve(comparativas),
          auth.fetchWithAuth('/api/predictions/by-week?weekStart=' + encodeURIComponent(ws)).then(function (r) {
            if (!r || !r.ok) return null;
            return r.json();
          }).catch(function () { return null; }),
          auth.fetchWithAuth('/api/dashboard/week?weekStart=' + encodeURIComponent(currentMon) + '&asOf=' + encodeURIComponent(todayYmd)).then(function (r) { return (r && r.ok) ? r.json() : null; }).catch(function () { return null; }),
          auth.fetchWithAuth('/api/dashboard/week?weekStart=' + encodeURIComponent(prevMon) + '&asOf=' + encodeURIComponent(addDays(prevMon, 6))).then(function (r) { return (r && r.ok) ? r.json() : null; }).catch(function () { return null; }),
          auth.fetchWithAuth('/api/predictions/by-week?weekStart=' + encodeURIComponent(nextMon)).then(function (r) { return (r && r.ok) ? r.json() : null; }).catch(function () { return null; })
        ]).then(function (arr) {
          return [arr[0], arr[1], arr[2], arr[3], arr[4], arr[5], arr[6]];
        });
      }).then(function (arr) {
        if (!arr) {
          loading = false;
          var b = document.getElementById('dashboard-cargar');
          if (b) b.disabled = false;
          return;
        }
        if (!isDashboardVisible()) {
          loading = false;
          var b = document.getElementById('dashboard-cargar');
          if (b) b.disabled = false;
          return;
        }
        var data = arr[0];
        var events = arr[1] || [];
        var comparativas = arr[2] || null;
        var byWeek = arr[3] || null;
        var currentWeekData = arr[4] || null;
        var prevWeekFullData = arr[5] || null;
        var nextWeekPred = arr[6] || null;
        var predByDate = {};
        function toYmd(s) { if (!s || typeof s !== 'string') return ''; return s.substring(0, 10); }
        function parseToYmd(s) {
          if (!s) return '';
          var str = typeof s === 'string' ? s.trim() : String(s);
          if (/^\d{4}-\d{2}-\d{2}/.test(str)) return str.substring(0, 10);
          var d = new Date(str);
          if (isNaN(d.getTime())) return toYmd(str);
          var y = d.getFullYear(), m = String(d.getMonth() + 1).padStart(2, '0'), day = String(d.getDate()).padStart(2, '0');
          return y + '-' + m + '-' + day;
        }
        function safePct(n) { if (n == null || typeof n !== 'number' || !Number.isFinite(n)) return '—'; return (n >= 0 ? '+' : '') + n.toFixed(1) + '%'; }
        var predDayDetails = {};
        var sumPredFromDailyJson = 0;
        if (byWeek && byWeek.dailyPredictionsJson) {
          try {
            var dailyArr = JSON.parse(byWeek.dailyPredictionsJson);
            if (Array.isArray(dailyArr)) {
              dailyArr.forEach(function (day) {
                var dateStr = parseToYmd(day.date || day.dateStr || day.Date || '');
                var rev = day.revenue != null ? day.revenue : (day.predictedRevenue != null ? day.predictedRevenue : null);
                if (dateStr && rev != null) {
                  predByDate[dateStr] = rev;
                  sumPredFromDailyJson += Number(rev);
                }
                if (dateStr) {
                  predDayDetails[dateStr] = {
                    weatherCode: day.weatherCode != null ? day.weatherCode : null,
                    weatherTempMax: day.weatherTempMax != null ? day.weatherTempMax : (day.tempMax != null ? day.tempMax : null),
                    weatherTempMin: day.weatherTempMin != null ? day.weatherTempMin : (day.tempMin != null ? day.tempMin : null),
                    weatherPrecipMm: day.weatherPrecipMm != null ? day.weatherPrecipMm : (day.precipMm != null ? day.precipMm : null),
                    weatherWindMaxKmh: day.weatherWindMaxKmh != null ? day.weatherWindMaxKmh : (day.windMaxKmh != null ? day.windMaxKmh : null)
                  };
                }
              });
            }
          } catch (e) { }
        }
        // Una sola fuente: total = suma exacta del JSON diario que usamos para la tabla, para que KPI, bloque y suma por días coincidan siempre.
        var predSemanaUnified = (sumPredFromDailyJson > 0) ? sumPredFromDailyJson : ((byWeek && byWeek.totalRevenue != null && Number(byWeek.totalRevenue) > 0) ? Number(byWeek.totalRevenue) : (comparativas && comparativas.baseRevenue != null && Number.isFinite(Number(comparativas.baseRevenue)) ? Number(comparativas.baseRevenue) : null));
        var ajustePct = (data && data.ajusteFacturacionManualPct != null && Number.isFinite(Number(data.ajusteFacturacionManualPct))) ? Number(data.ajusteFacturacionManualPct) : 9.1;
        var factorManual = 1 - (ajustePct / 100);
        var realAdjustedForComparison = null;
        // El backend ya envía en day.revenue el valor para comparaciones (real o ajustado). No aplicar factorManual de nuevo.
        if (data && data.isCurrentWeek && data.days && data.days.length > 0) {
          var sumFromBackend = 0;
          for (var ai = 0; ai < data.days.length; ai++) {
            var ra = (data.days[ai].revenue != null ? Number(data.days[ai].revenue) : (data.days[ai].Revenue != null ? Number(data.days[ai].Revenue) : 0));
            if (ra > 0) sumFromBackend += ra;
          }
          if (sumFromBackend > 0) realAdjustedForComparison = sumFromBackend;
        }
        if (!data) {
          loading = false;
          var b2 = document.getElementById('dashboard-cargar');
          if (b2) b2.disabled = false;
          return;
        }
        if (weekRangeEl) { weekRangeEl.textContent = formatWeekRange((weekInput && weekInput.value) || weekStart); weekRangeEl.classList.remove('dashboard-week-range--loading'); }
        var subtitleEl = document.getElementById('dashboard-subtitle');
        if (subtitleEl) {
          var wsForSub = (weekInput && weekInput.value) || weekStart;
          var isCurrent = data.isCurrentWeek === true || (data.isCurrentWeek !== false && isCurrentWeek(wsForSub));
          var n = (data.days && data.days.length) ? data.days.length : (data.daysIncludedCount != null ? data.daysIncludedCount : 0);
          var lastDayName = (data.days && data.days.length > 0 && data.days[data.days.length - 1].dayName) ? data.days[data.days.length - 1].dayName : null;
          var subtitleText = isCurrent
            ? (n > 0 ? ('Datos hasta el último día con facturación' + (lastDayName ? ' (' + lastDayName + ')' : '') + ' — ' + n + ' día' + (n !== 1 ? 's' : '')) : 'Sin datos de facturación esta semana')
            : 'Semana cerrada — datos completos';
          subtitleEl.textContent = subtitleText;
        }
        if (kpisEl) kpisEl.innerHTML = '';
        // Para cálculos y display: usar siempre totalRevenueForComparisons (real o ajustado). La manual bruta es solo informativa.
        var realForKpiComparisons = (data.totalRevenueForComparisons != null && Number(data.totalRevenueForComparisons) > 0)
          ? Number(data.totalRevenueForComparisons)
          : (data.isCurrentWeek && realAdjustedForComparison != null ? realAdjustedForComparison : (data.totalRevenue != null ? Number(data.totalRevenue) : null));
        var revValue = realForKpiComparisons != null
          ? Number(realForKpiComparisons).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €'
          : (data.totalRevenue != null ? Number(data.totalRevenue).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €' : '—');
        if (data.isCurrentWeek && data.totalRevenueManual != null && Number(data.totalRevenueManual) > 0 && Math.abs(Number(data.totalRevenueManual) - (realForKpiComparisons || 0)) > 0.01) {
          revValue += ' <span class="kpi-card-sub kpi-card-sub--muted">(' + Number(data.totalRevenueManual).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' € manual, informativo)</span>';
        }
        var pctVsPrev = '';
        if (data.isCurrentWeek && data.days && data.days.length > 0 && data.days.length < 7)
          pctVsPrev += '<div class="kpi-card-sub kpi-card-sub--muted">Datos hasta el último día con facturación</div>';
        if (realForKpiComparisons != null && data.prevWeekRevenue != null && data.prevWeekRevenue > 0) {
          var pct = ((realForKpiComparisons - data.prevWeekRevenue) / data.prevWeekRevenue) * 100;
          if (pct > 0) pctVsPrev += '<div class="kpi-card-sub kpi-card-sub--up">+' + pct.toFixed(1) + '% vs sem. ant.</div>';
          else if (pct < 0) pctVsPrev += '<div class="kpi-card-sub kpi-card-sub--down">' + pct.toFixed(1) + '% vs sem. ant.</div>';
          else pctVsPrev += '<div class="kpi-card-sub">0% vs sem. ant.</div>';
        }
        // % vs objetivo (facturación) — en semana actual usar real ajustado (ajuste configurable en días manual)
        var objRaw = data.facturacionObjetivo != null ? data.facturacionObjetivo : data.FacturacionObjetivo;
        var objNum = objRaw != null && objRaw !== '' ? Number(objRaw) : NaN;
        if (objNum > 0 && realForKpiComparisons != null) {
          var pctObj = ((realForKpiComparisons - objNum) / objNum) * 100;
          var objetivoClass = pctObj > 0 ? 'kpi-card-sub--up' : (pctObj < 0 ? 'kpi-card-sub--down' : '');
          var pctObjStr = pctObj > 0 ? ('+' + pctObj.toFixed(1)) : pctObj.toFixed(1);
          pctVsPrev += '<div class="kpi-card-sub ' + objetivoClass + '">' + pctObjStr + '% vs fact. objetivo</div>';
        } else {
          pctVsPrev += '<div class="kpi-card-sub kpi-card-sub--muted">vs fact. objetivo: —</div>';
        }
        if (predSemanaUnified != null && predSemanaUnified > 0) {
          var predFormatted = Number(predSemanaUnified).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
          pctVsPrev += '<div class="kpi-card-sub">Predicción' + (data.isCurrentWeek ? ' (semana)' : '') + ': ' + predFormatted + '</div>';
          pctVsPrev += '<div class="kpi-card-sub kpi-card-sub--muted" title="La predicción es de la semana seleccionada arriba. En Estimaciones > Planificación se planifica la semana siguiente.">Semana seleccionada arriba</div>';
        }
        var prodValue = data.avgProductivity != null ? data.avgProductivity.toFixed(1) + ' €/h' : '—';
        var pctVsPrevProd = '';
        if (data.isCurrentWeek && data.days && data.days.length > 0 && data.days.length < 7)
          pctVsPrevProd += '<div class="kpi-card-sub kpi-card-sub--muted">Datos hasta el último día con facturación</div>';
        if (data.avgProductivity != null && data.prevWeekProductivity != null && data.prevWeekProductivity > 0) {
          var pctProd = ((data.avgProductivity - data.prevWeekProductivity) / data.prevWeekProductivity) * 100;
          if (pctProd > 0) pctVsPrevProd += '<div class="kpi-card-sub kpi-card-sub--up">+' + pctProd.toFixed(1) + '% vs sem. ant.</div>';
          else if (pctProd < 0) pctVsPrevProd += '<div class="kpi-card-sub kpi-card-sub--down">' + pctProd.toFixed(1) + '% vs sem. ant.</div>';
          else pctVsPrevProd += '<div class="kpi-card-sub">0% vs sem. ant.</div>';
        }
        // % vs objetivo (productividad)
        var prodObjRaw = data.productividadObjetivo != null ? data.productividadObjetivo : (data.ProductividadObjetivo != null ? data.ProductividadObjetivo : data.productividadIdealEurHora);
        var prodObjNum = prodObjRaw != null && prodObjRaw !== '' ? Number(prodObjRaw) : NaN;
        if (prodObjNum > 0 && data.avgProductivity != null) {
          var pctProdObj = ((data.avgProductivity - prodObjNum) / prodObjNum) * 100;
          var prodObjClass = pctProdObj > 0 ? 'kpi-card-sub--up' : (pctProdObj < 0 ? 'kpi-card-sub--down' : '');
          var pctProdObjStr = pctProdObj > 0 ? ('+' + pctProdObj.toFixed(1)) : pctProdObj.toFixed(1);
          pctVsPrevProd += '<div class="kpi-card-sub ' + prodObjClass + '">' + pctProdObjStr + '% vs prod. objetivo</div>';
        } else {
          pctVsPrevProd += '<div class="kpi-card-sub kpi-card-sub--muted">vs prod. objetivo: —</div>';
        }
        var costeValue = data.costePersonalEur != null ? data.costePersonalEur.toFixed(0) + ' €' : '—';
        var costeSub = '';
        if (data.costePersonalPctFacturacion != null) {
          var pctCoste = data.costePersonalPctFacturacion;
          var costeClass = pctCoste < 30 ? 'kpi-card-sub--coste-bueno' : (pctCoste <= 35 ? 'kpi-card-sub--coste-asumible' : 'kpi-card-sub--coste-alto');
          costeSub = '<div class="kpi-card-sub ' + costeClass + '">' + pctCoste.toFixed(1) + '% vs facturación</div>';
        }
        if (data.costePersonalPctVsHistoric != null) {
          costeSub += '<div class="kpi-card-sub">vs histórico: ' + Number(data.costePersonalPctVsHistoric).toFixed(1) + '%</div>';
        }
        if (data.costePersonalEurFromContrato != null) {
          costeSub += '<div class="kpi-card-sub">Contrato: ' + Number(data.costePersonalEurFromContrato).toFixed(0) + ' €</div>';
        }
        var hoursSub = '';
        if (data.avgHoursHistoric != null) hoursSub = '<div class="kpi-card-sub">Histórico: ' + Number(data.avgHoursHistoric).toFixed(1) + ' h</div>';

        var kpis = [
          { label: 'Facturación total', value: revValue, sub: pctVsPrev },
          { label: 'Productividad media', value: prodValue, sub: pctVsPrevProd },
          { label: 'Horas totales', value: data.totalHours != null ? data.totalHours.toFixed(1) : '—', sub: hoursSub },
          { label: 'Coste personal', value: costeValue, sub: costeSub }
        ];
        kpis.forEach(function (k) {
          var div = document.createElement('div');
          div.className = 'kpi-card';
          div.innerHTML = '<div class="label">' + k.label + '</div><div class="value">' + k.value + '</div>' + (k.sub || '');
          kpisEl.appendChild(div);
        });
        // BLOQUE ÚNICO: Semana pasada (real) vs semana actual (real) vs semana siguiente (predicción)
        if (triadEl) {
          function fmtEur(n) { return Number(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €'; }
          function fmtPct(p) { return (p >= 0 ? '+' : '') + p.toFixed(1) + '%'; }

          var nextPredByDate = {};
          var nextPredTotal = 0;
          if (nextWeekPred && nextWeekPred.dailyPredictionsJson) {
            try {
              var dailyArr2 = JSON.parse(nextWeekPred.dailyPredictionsJson);
              if (Array.isArray(dailyArr2)) {
                dailyArr2.forEach(function (day) {
                  var dateStr = parseToYmd(day.date || day.dateStr || day.Date || '');
                  var rev = day.revenue != null ? day.revenue : (day.predictedRevenue != null ? day.predictedRevenue : null);
                  if (dateStr && rev != null) {
                    nextPredByDate[dateStr] = Number(rev);
                    nextPredTotal += Number(rev);
                  }
                });
              }
            } catch (e) { }
          }

          var prevDaysArr = (prevWeekFullData && Array.isArray(prevWeekFullData.days)) ? prevWeekFullData.days : [];
          var curDaysArr = (currentWeekData && Array.isArray(currentWeekData.days)) ? currentWeekData.days : [];
          var curIsCurrent = currentWeekData && currentWeekData.isCurrentWeek === true;
          var curDaysCount = currentWeekData && currentWeekData.daysIncludedCount != null ? Number(currentWeekData.daysIncludedCount) : curDaysArr.length;

          var prevTotal = prevWeekFullData && (prevWeekFullData.totalRevenueForComparisons != null) ? Number(prevWeekFullData.totalRevenueForComparisons) : (prevWeekFullData ? Number(prevWeekFullData.totalRevenue || 0) : 0);
          var curTotal = currentWeekData && (currentWeekData.totalRevenueForComparisons != null) ? Number(currentWeekData.totalRevenueForComparisons) : (currentWeekData ? Number(currentWeekData.totalRevenue || 0) : 0);
          var nextTotal = nextPredTotal || 0;

          var vsPrevDelta = curTotal - prevTotal;
          var vsPrevPct = prevTotal > 0 ? (vsPrevDelta / prevTotal) * 100 : null;
          var nextVsPrevDelta = nextTotal - prevTotal;
          var nextVsPrevPct = prevTotal > 0 ? (nextVsPrevDelta / prevTotal) * 100 : null;

          var runRate = (curDaysCount > 0) ? (curTotal / curDaysCount) * 7 : 0;
          var runRateDeltaVsPrev = runRate - prevTotal;
          var runRatePctVsPrev = prevTotal > 0 ? (runRateDeltaVsPrev / prevTotal) * 100 : null;

          var rows = [];
          for (var i = 0; i < 7; i++) {
            var dateCur = addDays(currentMon, i);
            var dayName = dayNameFromDate(dateCur);

            var prevDate = addDays(prevMon, i);
            var prevDay = prevDaysArr.find(function (x) { return x && x.date && x.date.substring(0, 10) === prevDate; }) || null;
            var prevVal = prevDay ? Number(prevDay.revenue || 0) : 0;

            var curDay = curDaysArr.find(function (x) { return x && x.date && x.date.substring(0, 10) === dateCur; }) || null;
            var curVal = curDay ? Number(curDay.revenue || 0) : null;

            var nextDate = addDays(nextMon, i);
            var nextVal = nextPredByDate[nextDate] != null ? Number(nextPredByDate[nextDate]) : 0;

            var dCur = (curVal != null) ? (curVal - prevVal) : null;
            var pCur = (curVal != null && prevVal > 0) ? (dCur / prevVal) * 100 : null;
            var dNext = nextVal - prevVal;
            var pNext = prevVal > 0 ? (dNext / prevVal) * 100 : null;

            rows.push({ day: dayName, prev: prevVal, cur: curVal, next: nextVal, dCur: dCur, pCur: pCur, dNext: dNext, pNext: pNext });
          }

          var driversCur = rows.filter(function (r) { return r.cur != null; }).slice().sort(function (a, b) { return Math.abs(b.dCur || 0) - Math.abs(a.dCur || 0); }).slice(0, 3);
          var driversNext = rows.slice().sort(function (a, b) { return Math.abs(b.dNext || 0) - Math.abs(a.dNext || 0); }).slice(0, 3);
          function driverLine(r, kind) {
            var delta = kind === 'cur' ? (r.dCur || 0) : (r.dNext || 0);
            var pct = kind === 'cur' ? r.pCur : r.pNext;
            return r.day + ': ' + (delta >= 0 ? '+' : '−') + fmtEur(Math.abs(delta)).replace(' €', '€') + (pct != null ? ' (' + fmtPct(pct) + ')' : '');
          }

          var kpiHtml = '';
          kpiHtml += '<div class="dashboard-compare-kpi"><div class="label">Semana pasada (real)</div><div class="value">' + fmtEur(prevTotal) + '</div><div class="sub">7 días</div></div>';
          kpiHtml += '<div class="dashboard-compare-kpi"><div class="label">Semana actual (real)</div><div class="value">' + fmtEur(curTotal) + '</div><div class="sub">' + (curIsCurrent ? ('hasta hoy · ' + (curDaysCount || 0) + '/7 días') : 'semana cerrada') + '</div></div>';
          var clsCur = (vsPrevDelta >= 0) ? 'dashboard-compare-kpi--up' : 'dashboard-compare-kpi--down';
          kpiHtml += '<div class="dashboard-compare-kpi ' + clsCur + '"><div class="label">Actual vs pasada</div><div class="value">' + (vsPrevDelta >= 0 ? '+' : '−') + fmtEur(Math.abs(vsPrevDelta)) + '</div><div class="sub">' + (vsPrevPct != null ? fmtPct(vsPrevPct) : '—') + '</div></div>';
          var clsRR = (runRateDeltaVsPrev >= 0) ? 'dashboard-compare-kpi--up' : 'dashboard-compare-kpi--down';
          kpiHtml += '<div class="dashboard-compare-kpi ' + clsRR + '"><div class="label">Run-rate semana actual</div><div class="value">' + fmtEur(runRate) + '</div><div class="sub">vs pasada: ' + (runRatePctVsPrev != null ? fmtPct(runRatePctVsPrev) : '—') + '</div></div>';
          kpiHtml += '<div class="dashboard-compare-kpi"><div class="label">Semana siguiente (pred.)</div><div class="value">' + (nextTotal > 0 ? fmtEur(nextTotal) : '—') + '</div><div class="sub">vs pasada: ' + (nextVsPrevPct != null ? fmtPct(nextVsPrevPct) : '—') + '</div></div>';

          var driversHtml = '';
          if (driversCur.length || driversNext.length) {
            driversHtml += '<div class="dashboard-compare-drivers"><div class="dashboard-compare-drivers-title">Qué días mueven la semana</div>';
            if (driversCur.length) {
              driversHtml += '<div class="dashboard-triad-driver-group"><div class="dashboard-triad-driver-title">Actual vs pasada</div><ul>' +
                driversCur.map(function (r) { return '<li>' + escapeHtml(driverLine(r, 'cur')) + '</li>'; }).join('') +
                '</ul></div>';
            }
            if (driversNext.length) {
              driversHtml += '<div class="dashboard-triad-driver-group"><div class="dashboard-triad-driver-title">Siguiente (pred.) vs pasada</div><ul>' +
                driversNext.map(function (r) { return '<li>' + escapeHtml(driverLine(r, 'next')) + '</li>'; }).join('') +
                '</ul></div>';
            }
            driversHtml += '</div>';
          }

          var tableRowsTriad = rows.map(function (r) {
            function cellDelta(delta, pct) {
              if (delta == null) return '<span class="dashboard-triad-muted">—</span>';
              var cls = (delta < 0) ? 'dashboard-pct--down' : 'dashboard-pct--up';
              var pctStr = (pct != null && Number.isFinite(pct)) ? fmtPct(pct) : '—';
              return '<div class="' + cls + '"><strong>' + (delta >= 0 ? '+' : '−') + fmtEur(Math.abs(delta)) + '</strong><div class="dashboard-triad-sub">' + pctStr + '</div></div>';
            }
            return '<tr>' +
              '<td class="dashboard-compare-td-day"><div class="dashboard-compare-day">' + r.day + '</div></td>' +
              '<td>' + fmtEur(r.prev) + '</td>' +
              '<td>' + (r.cur != null ? fmtEur(r.cur) : '<span class="dashboard-triad-muted">—</span>') + '</td>' +
              '<td>' + (r.next > 0 ? fmtEur(r.next) : '<span class="dashboard-triad-muted">—</span>') + '</td>' +
              '<td>' + cellDelta(r.dCur, r.pCur) + '</td>' +
              '<td>' + cellDelta(r.dNext, r.pNext) + '</td>' +
              '</tr>';
          }).join('');

          triadEl.innerHTML =
            '<h3 class="dashboard-week-triad-title">Semana pasada vs actual vs siguiente</h3>' +
            '<div class="dashboard-week-triad-body">' +
            '<p class="dashboard-compare-subtitle">Bloque único de planificación: contrasta el <strong>real</strong> de la semana pasada, el <strong>real acumulado</strong> de la actual y la <strong>predicción</strong> de la siguiente. La semana seleccionada en el selector sigue controlando el resto del Dashboard.</p>' +
            '<div class="dashboard-compare-kpis">' + kpiHtml + '</div>' +
            driversHtml +
            '<div class="dashboard-compare-table-wrap">' +
            '<table class="dashboard-compare-table dashboard-triad-table">' +
            '<thead><tr><th>Día</th><th>Pasada (real)</th><th>Actual (real)</th><th>Siguiente (pred.)</th><th>Δ Actual vs pasada</th><th>Δ Siguiente vs pasada</th></tr></thead>' +
            '<tbody>' + tableRowsTriad + '</tbody>' +
            '</table>' +
            '</div>' +
            '</div>';
        }
        if (resumenEl) {
          resumenEl.innerHTML = '<h3>Resumen</h3>' +
            (data.resumenClasificacion ? '<p class="dashboard-clasificacion">' + data.resumenClasificacion + '</p>' : '') +
            '<p>' + (data.resumenTexto || 'Sin datos para esta semana.') + '</p>';
        }
        var eventsByDate = {};
        (events || []).forEach(function (e) {
          if (!e || !e.date) return;
          if (!eventsByDate[e.date]) eventsByDate[e.date] = [];
          eventsByDate[e.date].push(e);
        });

        var days = data.days || [];
        var daysToShow = days;
        if (data.isCurrentWeek && days.length >= 0) {
          var ws = (weekInput && weekInput.value) || weekStart;
          daysToShow = [];
          for (var i = 0; i < 7; i++) {
            var dateStr = addDays(ws, i);
            var found = null;
            for (var j = 0; j < days.length; j++) { if (days[j].date === dateStr) { found = days[j]; break; } }
            if (found) daysToShow.push(found);
            else daysToShow.push({ date: dateStr, dayName: dayNameFromDate(dateStr), isPlaceholder: true });
          }
        } else if (!data.isCurrentWeek) {
          var wsPast = (weekInput && weekInput.value) || weekStart;
          daysToShow = [];
          for (var i = 0; i < 7; i++) {
            var dateStrPast = addDays(wsPast, i);
            var foundPast = null;
            for (var j = 0; j < days.length; j++) { if (days[j].date === dateStrPast) { foundPast = days[j]; break; } }
            if (foundPast) daysToShow.push(foundPast);
            else daysToShow.push({ date: dateStrPast, dayName: dayNameFromDate(dateStrPast), isPlaceholder: true });
          }
        }
        if (daysToShow.length === 0) {
          var wsForEmpty = (weekInput && weekInput.value) || weekStart;
          var isFutureWeek = getWeekStart(new Date()) < wsForEmpty;
          if (isFutureWeek) {
            daysWrap.innerHTML = '<p class="dashboard-empty">Semana futura: aún no hay datos.</p>';
          } else {
            daysWrap.innerHTML = '<p class="dashboard-empty">Aún no hay días registrados para esta semana. Puedes añadirlos desde <a href="#registro" class="dashboard-link-registro">Registro de ejecución</a>.</p><p class="dashboard-empty-hint">Si acabas de cargar datos de muestra, asegúrate de que el selector de semana es la <strong>semana actual</strong> (lunes de esta semana) y pulsa <strong>Actualizar</strong>.</p>';
          }
          if (daysCardsWrap) daysCardsWrap.innerHTML = '';
        } else {
          var todayObj = new Date();
          var todayYmd = todayObj.getFullYear() + '-' + String(todayObj.getMonth() + 1).padStart(2, '0') + '-' + String(todayObj.getDate()).padStart(2, '0');
          function weatherEmoji(code) {
            if (code == null) return '—';
            code = Number(code);
            if (code === 0) return '☀️';
            if (code >= 1 && code <= 3) return '⛅';
            if (code === 45 || code === 48) return '🌫️';
            if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return '🌧️';
            if (code >= 71 && code <= 77) return '❄️';
            if (code >= 95) return '⛈️';
            return '🌦️';
          }
          function weatherText(d) {
            if (!d) return '—';
            var hasAny = (d.weatherCode != null) || (d.weatherTempMax != null) || (d.weatherTempMin != null) || (d.weatherPrecipMm != null) || (d.weatherWindMaxKmh != null);
            if (!hasAny) return '—';
            var parts = [];
            var icon = weatherEmoji(d.weatherCode);
            parts.push(icon);
            if (d.weatherTempMax != null || d.weatherTempMin != null) {
              var tMax = d.weatherTempMax != null ? Number(d.weatherTempMax).toFixed(0) : '—';
              var tMin = d.weatherTempMin != null ? Number(d.weatherTempMin).toFixed(0) : '—';
              parts.push(tMax + '/' + tMin + '°C');
            }
            if (d.weatherPrecipMm != null) parts.push(Number(d.weatherPrecipMm).toFixed(0) + 'mm');
            if (d.weatherWindMaxKmh != null) parts.push(Number(d.weatherWindMaxKmh).toFixed(0) + 'km/h');
            return parts.join(' · ');
          }
          /** Construye el párrafo de observaciones: apertura (facturación + productividad + estado), conclusión narrativa del día y frase de comparación con la media. */
          function buildDayObservationsParagraph(d) {
            if (!d) return '';
            var revNum = d.revenue != null ? Number(d.revenue) : null;
            var hoursNum = d.effectiveHours != null ? Number(d.effectiveHours) : (d.hoursWorked != null ? Number(d.hoursWorked) : null);
            var prodNum = d.effectiveProductivity != null ? Number(d.effectiveProductivity) : (d.productivity != null ? Number(d.productivity) : null);
            var dayConclusion = (d.dayConclusion != null && String(d.dayConclusion).trim() !== '') ? String(d.dayConclusion).trim() : '';
            var dayNameLabel = d.dayName || 'día';
            var dayEstado = (d.dayEstado != null && String(d.dayEstado).trim() !== '') ? String(d.dayEstado).trim() : null;
            var parts = [];
            var opening = '';
            var revFormatted = revNum != null ? Number(revNum).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '';
            if (revNum != null && revNum > 0 && prodNum != null) {
              opening = 'El ' + dayNameLabel + ' se facturaron ' + revFormatted + ' € y la productividad del día fue de ' + Number(prodNum).toFixed(1) + ' €/h.';
            } else if (revNum != null && revNum > 0 && hoursNum != null && hoursNum > 0) {
              opening = 'El ' + dayNameLabel + ' se facturaron ' + revFormatted + ' € con ' + Number(hoursNum).toLocaleString('es-ES', { minimumFractionDigits: 1, maximumFractionDigits: 1 }) + ' horas trabajadas.';
            } else if (revNum != null && revNum > 0) {
              opening = 'El ' + dayNameLabel + ' se facturaron ' + revFormatted + ' €; no hay horas registradas.';
            } else if (revNum != null && revNum === 0 && (hoursNum == null || hoursNum === 0)) {
              opening = 'El ' + dayNameLabel + ' no hay datos de facturación ni horas.';
            } else {
              opening = 'El ' + dayNameLabel + ' no hay datos suficientes para un resumen.';
            }
            // El estado del día se cuenta en dayConclusion (\"Fue un día tranquilo...\");
            // no lo repetimos aquí para evitar \"Día tranquilo. Fue un día tranquilo...\".
            parts.push(opening);
            if (dayConclusion !== '') parts.push(escapeHtml(dayConclusion));
            var conclusionExtra = [];
            // Productividad: solo juicio (alto/normal/bajo), sin repetir el valor numérico ya mostrado en la apertura
            if (prodNum != null && prodNum > 0) {
              var prodFrase;
              if (prodNum >= 80) prodFrase = 'En términos de productividad, fue un nivel alto de rendimiento por hora.';
              else if (prodNum >= 50) prodFrase = 'En términos de productividad, el nivel se mantuvo dentro de la normalidad.';
              else prodFrase = 'En términos de productividad, el nivel quedó algo por debajo de lo deseable.';
              conclusionExtra.push(prodFrase);
            }
            // Facturación frente a la media histórica del mismo día de la semana
            if (d.avgRevenueHistoric != null && d.pctVsAvgHistoric != null && typeof d.pctVsAvgHistoric === 'number') {
              var pct = d.pctVsAvgHistoric;
              var absPct = Math.abs(pct);
              var diaMedia = (d.dayName || 'día').toLowerCase();
              var fraseFacturacion = '';
              if (pct >= 0) {
                if (absPct <= 5) fraseFacturacion = 'La facturación se mantuvo en línea con la media de los ' + diaMedia + '.';
                else if (absPct <= 15) fraseFacturacion = 'La facturación estuvo ligeramente por encima de la media de los ' + diaMedia + ', pero sin que eso se tradujera en presión operativa.';
                else fraseFacturacion = 'La facturación estuvo claramente por encima de la media de los ' + diaMedia + '.';
              } else {
                if (absPct <= 5) fraseFacturacion = 'La facturación se mantuvo en línea con la media de los ' + diaMedia + '.';
                else if (absPct <= 15) fraseFacturacion = 'La facturación estuvo ligeramente por debajo de la media de los ' + diaMedia + '.';
                else fraseFacturacion = 'La facturación estuvo por debajo de la media de los ' + diaMedia + '.';
              }
              conclusionExtra.push(fraseFacturacion);
            }
            // Tendencia: 5 niveles (solo texto, sin mostrar el % en el párrafo).
            if (d.trendLabel != null && String(d.trendLabel).trim() !== '') {
              var trend = String(d.trendLabel);
              var trendLower = trend.toLowerCase();
              var diaTendencia = (d.dayName || 'día').toLowerCase();
              var frase = '';
              if (trendLower.indexOf('levemente al alza') !== -1) {
                frase = 'Los ' + diaTendencia + ' suben levemente en las últimas semanas.';
              } else if (trendLower.indexOf('al alza') !== -1) {
                frase = 'Los ' + diaTendencia + ' llevan varias semanas al alza.';
              } else if (trendLower.indexOf('levemente a la baja') !== -1) {
                frase = 'Los ' + diaTendencia + ' bajan un poco en las últimas semanas.';
              } else if (trendLower.indexOf('a la baja') !== -1) {
                frase = 'Los ' + diaTendencia + ' vienen algo más flojos en las últimas semanas.';
              } else if (trendLower.indexOf('estable') !== -1) {
                frase = 'Los ' + diaTendencia + ' se mantienen estables en las últimas semanas.';
              }
              if (frase) conclusionExtra.push(frase);
            }
            if (conclusionExtra.length > 0) parts.push(conclusionExtra.map(function (s) { return escapeHtml(s); }).join(' '));
            return parts.join(' ');
          }

          var headers = ['Día', 'Fecha', 'Clima', 'Facturación', 'Predicho', 'Horas', 'Productividad (€/h)', 'Personal', 'Observaciones del Día'];
          var rows = daysToShow.map(function (d) {
            if (d.isPlaceholder) {
              var dayName = d.dayName || dayNameFromDate(d.date);
              var dateShort = formatDateShort(d.date);
              var isFuture = d.date > todayYmd;
              var dayKey = toYmd(d.date || '');
              var predValPlaceholder = predByDate[dayKey] != null ? Number(predByDate[dayKey]) : NaN;
              var predCellPlaceholder = (predValPlaceholder != null && Number.isFinite(predValPlaceholder)) ? '<span class="dashboard-day-pred">' + predValPlaceholder.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €</span>' : '—';
              var climaPlaceholder = '—';
              if (predDayDetails[dayKey]) {
                climaPlaceholder = '<span class="dashboard-weather">' + weatherText(predDayDetails[dayKey]) + '</span>';
              }
              var observacionesPlaceholder = isFuture ? '—' : '<span class="dashboard-observaciones-text">Sin datos.</span>';
              return [dayName, dateShort, climaPlaceholder, '—', predCellPlaceholder, '—', '—', '—', observacionesPlaceholder];
            }
            var dayName = d.dayName || dayNameFromDate(d.date);
            var dateShort = formatDateShort(d.date);
            var clima = '<span class="dashboard-weather">' + weatherText(d) + '</span>';
            var revNum = d.revenue != null ? Number(d.revenue) : null;
            var rev = '—';
            if (revNum != null) {
              var revFormatted = revNum.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
              var showManualHint = data.isCurrentWeek && revNum > 0 && d.revenueFromManual !== false;
              if (showManualHint) {
                var estFinal = revNum * factorManual;
                rev = revFormatted + ' (' + estFinal.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €)';
              } else rev = revFormatted;
            }
            var dayKey = toYmd(d.date || '');
            var predValNum = predByDate[dayKey] != null ? Number(predByDate[dayKey]) : NaN;
            var predCell = (predValNum != null && Number.isFinite(predValNum)) ? '<span class="dashboard-day-pred">' + predValNum.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €</span>' : '—';
            var hoursVal = d.effectiveHours != null ? d.effectiveHours : d.hoursWorked;
            var hours = hoursVal != null ? hoursVal.toFixed(1) : '—';
            var prodVal = d.effectiveProductivity != null ? d.effectiveProductivity : d.productivity;
            var prod = prodVal != null ? prodVal.toFixed(1) : '—';
            var staffParts = [];
            if (d.staffSummarySala != null && d.staffSummarySala !== '' && d.staffSummaryCocina != null && d.staffSummaryCocina !== '') {
              staffParts.push('Sala: ' + d.staffSummarySala + ' | Cocina: ' + d.staffSummaryCocina);
              if (d.plannedHoursFromPdf != null && d.plannedHoursFromPdf > 0) {
                if (d.plannedHoursBreakdown != null && d.plannedHoursBreakdown !== '') staffParts.push(d.plannedHoursBreakdown);
                staffParts.push('Horas (cuadrante): ' + Number(d.plannedHoursFromPdf).toFixed(1) + ' h');
              } else if (d.calculatedStaffHours != null) {
                staffParts.push('Horas calc.: ' + Number(d.calculatedStaffHours).toFixed(1) + ' h (Sala+Cocina × h/turno)');
              }
            }
            if (staffParts.length === 0 && d.staffTotal != null) staffParts.push(String(d.staffTotal));
            var staff = staffParts.length ? staffParts.join('<br/>') : '—';
            var dateStr = d.date || '';
            var observacionesParagraph = buildDayObservationsParagraph(d);
            var observacionesCell = '<div class="dashboard-observaciones-wrap">' +
              '<div class="dashboard-observaciones-text">' + observacionesParagraph + '</div></div>';
            return [dayName, dateShort, clima, rev, predCell, hours, prod, staff, observacionesCell];
          });
          var personalTitle = 'Sala y cocina por turno (Mediodía-Tarde-Noche). Con PDF: se muestran las horas del cuadrante por turno (reales). Sin PDF (dato manual): Horas calc. = (Sala+Cocina) × h/turno (Config.).';
          var thead = '<thead><tr><th>Día</th><th>Fecha</th><th>Clima</th><th>Facturación</th><th class="dashboard-th-pred">Predicho</th><th>Horas</th><th>Productividad (€/h)</th><th title="' + personalTitle + '">Personal</th><th>Observaciones del Día</th></tr></thead>';
          var tbody = '<tbody>' + rows.map(function (row) { return '<tr>' + row.map(function (c) { return '<td>' + c + '</td>'; }).join('') + '</tr>'; }).join('') + '</tbody>';
          daysWrap.innerHTML = '<table class="dashboard-table">' + thead + tbody + '</table>';

          function bindEventAddButtons(container) {
            if (!container) return;
            container.querySelectorAll('.dashboard-event-add').forEach(function (btn) {
              btn.addEventListener('click', function () {
                var dateStr = btn.getAttribute('data-date') || '';
                var name = prompt('Nombre del evento', '');
                if (!name || !name.trim()) return;
                var impact = prompt('Impacto (Alto/Medio/Bajo o vacío)', '') || '';
                var desc = prompt('Descripción (opcional)', '') || '';
                auth.fetchWithAuth('/api/events', {
                  method: 'POST',
                  body: JSON.stringify({ date: dateStr, name: name, impact: impact, description: desc })
                }).then(function (r) {
                  if (!r.ok) return r.json().then(function (d) { throw new Error(d.message || 'Error'); });
                  return r.json();
                }).then(function () { load(); }).catch(function (e) { alert(e.message || 'Error al crear evento'); });
              });
            });
          }
          bindEventAddButtons(daysWrap);

          // Cards (móvil): más legible que una tabla de 7 columnas.
          if (daysCardsWrap) {
            daysCardsWrap.innerHTML = '<div class="dashboard-days-cards">' + daysToShow.map(function (d) {
              if (d.isPlaceholder) {
                var dayName = d.dayName || dayNameFromDate(d.date);
                var dateShort = formatDateShort(d.date);
                var isFutureCard = d.date > todayYmd;
                var trendLabelCard = isFutureCard ? 'Pendiente' : 'Sin datos';
                var actionsHtml = isFutureCard ? '' : (
                  '<div class="dashboard-day-card-actions">' +
                  '<a class="dashboard-action-link" href="#registro?date=' + encodeURIComponent(d.date) + '">Abrir registro</a> ' +
                  '<a class="dashboard-action-link" href="#preguntas?date=' + encodeURIComponent(d.date) + '">Feedback</a>' +
                  '</div>'
                );
                return '<div class="dashboard-day-card dashboard-day-card--pending">' +
                  '<div class="dashboard-day-card-head"><div class="dashboard-day-card-title">' + dayName + '</div><div class="dashboard-day-card-date">' + dateShort + '</div></div>' +
                  '<div class="dashboard-day-card-meta"><span class="dashboard-weather">' + trendLabelCard + '</span></div>' + actionsHtml + '</div>';
              }
              var dayName = d.dayName || dayNameFromDate(d.date);
              var dateShort = formatDateShort(d.date);
              var revNumCard = d.revenue != null ? Number(d.revenue) : null;
              var rev = '—';
              if (revNumCard != null) {
                var revFormattedCard = revNumCard.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
                var showManualHintCard = data.isCurrentWeek && revNumCard > 0 && d.revenueFromManual !== false;
                if (showManualHintCard) {
                  var estFinalCard = revNumCard * factorManual;
                  rev = revFormattedCard + ' (' + estFinalCard.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €)';
                } else rev = revFormattedCard;
              }
              var dayKeyCard = toYmd(d.date || '');
              var predValCard = predByDate[dayKeyCard] != null ? Number(predByDate[dayKeyCard]) : NaN;
              var predLineHtml = (predValCard != null && Number.isFinite(predValCard)) ? '<div class="dashboard-day-card-kpi dashboard-day-card-pred"><span class="label">Predicho</span><span class="value">' + predValCard.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €</span></div>' : '';
              var hoursVal = d.effectiveHours != null ? d.effectiveHours : d.hoursWorked;
              var hours = hoursVal != null ? hoursVal.toFixed(1) + ' h' : '—';
              var prodVal = d.effectiveProductivity != null ? d.effectiveProductivity : d.productivity;
              var prod = prodVal != null ? prodVal.toFixed(1) + ' €/h' : '—';
              var staffLine = '—';
              if (d.staffSummarySala != null && d.staffSummarySala !== '' && d.staffSummaryCocina != null && d.staffSummaryCocina !== '')
                staffLine = 'Sala ' + d.staffSummarySala + ' | Cocina ' + d.staffSummaryCocina;
              else if (d.staffTotal != null) staffLine = 'Total: ' + d.staffTotal;
              var trendParts = [];
              if (d.avgRevenueHistoric != null && d.avgRevenueHistoric !== '') {
                var mediaStr = 'vs media ' + Number(d.avgRevenueHistoric).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
                if (d.pctVsAvgHistoric != null && typeof d.pctVsAvgHistoric === 'number') mediaStr += ' (hoy ' + (d.pctVsAvgHistoric >= 0 ? '+' : '') + d.pctVsAvgHistoric + '%)';
                trendParts.push(mediaStr);
              }
              if (d.trendLabel) trendParts.push(String(d.trendLabel).replace(/\s*\([+-]?\d+%\)\s*$/, '').trim());
              if (d.trendVsPrevWeek) trendParts.push(d.trendVsPrevWeek);
              var trendLine = trendParts.length ? trendParts.join(' · ') : '—';
              var climaLine = weatherText(d);
              var dateStr = d.date || '';
              var evs = eventsByDate[dateStr] || [];
              var eventsHtml = '';
              if (evs.length > 0) {
                eventsHtml = '<div class="dashboard-day-card-events"><div class="label">Eventos</div>' +
                  evs.slice(0, 2).map(function (e) {
                    return '<div class="dashboard-event-item">' +
                      '<span class="dashboard-event-name">' + (e.name || '') + '</span>' +
                      (e.impact ? '<span class="dashboard-event-impact">' + e.impact + '</span>' : '') +
                      '</div>';
                  }).join('') +
                  (evs.length > 2 ? '<div class="dashboard-events-more">+' + (evs.length - 2) + ' más</div>' : '') +
                  '</div>';
              }
              return '' +
                '<div class="dashboard-day-card">' +
                '<div class="dashboard-day-card-head"><div class="dashboard-day-card-title">' + dayName + '</div><div class="dashboard-day-card-date">' + dateShort + '</div></div>' +
                '<div class="dashboard-day-card-kpis">' +
                '<div class="dashboard-day-card-kpi"><span class="label">Facturación</span><span class="value">' + rev + '</span></div>' +
                (predLineHtml || '') +
                '<div class="dashboard-day-card-kpi"><span class="label">Horas</span><span class="value">' + hours + '</span></div>' +
                '<div class="dashboard-day-card-kpi"><span class="label">Prod.</span><span class="value">' + prod + '</span></div>' +
                '</div>' +
                '<div class="dashboard-day-card-meta">' +
                '<div><span class="label">Clima</span> <span class="dashboard-weather">' + climaLine + '</span></div>' +
                '<div><span class="label">Personal</span> ' + staffLine + '</div>' +
                (d.calculatedStaffHours != null ? '<div><span class="label">Horas equipo</span> ' + Number(d.calculatedStaffHours).toFixed(1) + ' h</div>' : '') +
                '<div><span class="label">Tendencia</span> ' + trendLine + '</div>' +
                '</div>' +
                '<div class="dashboard-day-card-feedback"><span class="label">Observaciones del día</span> ' + buildDayObservationsParagraph(d) + '</div>' +
                eventsHtml +
                '</div>';
            }).join('') + '</div>';

            bindEventAddButtons(daysCardsWrap);
          }
        }
        var chartEl = document.getElementById('dashboard-chart-30d');
        if (chartEl) {
          var rawItems = data.last30Days || [];
          if (rawItems.length === 0) { chartEl.innerHTML = '<p class="dashboard-empty">Sin datos para los últimos 30 días.</p><p class="dashboard-empty-hint">Puedes cargar <a href="#configuracion">datos de muestra</a> en Configuración o importar desde Excel arriba.</p>'; }
          else {
            var today = new Date();
            var start = new Date(today);
            start.setDate(start.getDate() - 29);
            var byDate = {};
            rawItems.forEach(function (x) {
              if (x.date && x.date.length >= 10) byDate[x.date] = x.revenue != null ? x.revenue : 0;
            });
            var items = [];
            for (var i = 0; i < 30; i++) {
              var d = new Date(start);
              d.setDate(start.getDate() + i);
              var y = d.getFullYear();
              var m = String(d.getMonth() + 1).padStart(2, '0');
              var day = String(d.getDate()).padStart(2, '0');
              var dateStr = y + '-' + m + '-' + day;
              items.push({ date: dateStr, revenue: byDate[dateStr] != null ? byDate[dateStr] : 0 });
            }
            var scaleMaxReal = Math.max.apply(null, items.map(function (x) { return x.revenue || 0; }));
            // La escala máxima debe ser 25% superior al máximo real.
            var scaleMax = Math.ceil((scaleMaxReal > 0 ? scaleMaxReal : 1) * 1.25);
            var avg30 = items.reduce(function (s, it) { return s + (it.revenue || 0); }, 0) / (items.length || 1);
            var scaleLabel = 'Escala: 0 – ' + Number(scaleMax).toLocaleString('es-ES', { maximumFractionDigits: 0 }) + ' € · Media 30d: ' + Number(avg30).toLocaleString('es-ES', { maximumFractionDigits: 0 }) + ' €';
            var barsArr = [];
            var lastWeekStartKey = null;
            var weekIndex = -1;
            items.forEach(function (x, idx) {
              var rev = x.revenue || 0;
              var pct = scaleMax > 0 ? Math.min(100, (rev / scaleMax) * 100) : 0;
              var dateShort = (x.date && x.date.length >= 10) ? (x.date.slice(8, 10) + '/' + x.date.slice(5, 7)) : '—';
              var dayName = (x.date && x.date.length >= 10) ? dayNameFromDate(x.date) : '—';
              var dt = x.date && x.date.length >= 10 ? new Date(x.date + 'T12:00:00') : null;

              // Fin de semana = viernes, sábado y domingo.
              var isWeekend = dt ? (dt.getDay() === 0 || dt.getDay() === 5 || dt.getDay() === 6) : false;

              // Resaltado por semanas: alternar fondo según semana (lunes como inicio).
              var weekStartKey = x.date && x.date.length >= 10 ? getWeekStart(x.date) : null;
              if (weekStartKey && weekStartKey !== lastWeekStartKey) {
                weekIndex++;
                lastWeekStartKey = weekStartKey;
              }
              var isAltWeek = weekIndex % 2 === 1;
              var isWeekBoundary = dt ? (dt.getDay() === 1 && idx !== 0) : false; // lunes

              var cls = 'dashboard-chart-bar-wrap' +
                (isWeekend ? ' is-weekend' : '') +
                (isAltWeek ? ' week-alt' : '') +
                (isWeekBoundary ? ' week-boundary' : '');

              barsArr.push(
                '<div class="' + cls + '" title="' + (x.date || '') + ': ' + (rev !== null && rev !== undefined ? Number(rev).toFixed(0) : 0) + ' €">' +
                '<div class="dashboard-chart-bar" style="height:' + pct + '%"></div>' +
                '<span class="dashboard-chart-label">' + dateShort + '</span>' +
                '<span class="dashboard-chart-day">' + dayName + '</span>' +
                '</div>'
              );
            });
            var bars = barsArr.join('');
            chartEl.innerHTML = '<p class="dashboard-chart-scale">' + scaleLabel + '</p><div class="dashboard-chart-bars">' + bars + '</div>';
          }
        }
        loading = false;
        var btnDone = document.getElementById('dashboard-cargar');
        if (btnDone) btnDone.disabled = false;
        var btnNextEl = document.getElementById('dashboard-next');
        if (btnNextEl) {
          var currentMon = getWeekStart(new Date());
          var selectedWs = (weekInput && weekInput.value) || weekStart;
          btnNextEl.disabled = (selectedWs === currentMon);
          btnNextEl.title = (selectedWs === currentMon) ? 'Semana actual' : 'Semana siguiente';
        }
      }).catch(function (err) {
        if (!isDashboardVisible()) {
          loading = false;
          var b = document.getElementById('dashboard-cargar');
          if (b) b.disabled = false;
          return;
        }
        if (weekRangeEl) weekRangeEl.textContent = formatWeekRange((weekInput && weekInput.value) || weekStart);
        weekRangeEl && weekRangeEl.classList.remove('dashboard-week-range--loading');
        if (triadEl) triadEl.innerHTML = '';
        if (kpisEl) kpisEl.innerHTML = '<p class="error-msg">' + (err.message || 'Error al cargar.') + '</p>';
        var chartEl = document.getElementById('dashboard-chart-30d');
        if (chartEl) chartEl.innerHTML = '<p class="dashboard-empty">Sin datos para el gráfico.</p>';
        loading = false;
        var btnErr = document.getElementById('dashboard-cargar');
        if (btnErr) btnErr.disabled = false;
      });
    }
    var btnCargarEl = document.getElementById('dashboard-cargar');
    if (btnCargarEl) btnCargarEl.addEventListener('click', load);
    if (weekInput) weekInput.addEventListener('change', function () {
      // Normalizar a lunes aunque el usuario elija cualquier día.
      try {
        weekInput.value = getWeekStart(weekInput.value);
      } catch (e) { }
      load();
    });
    var btnPrev = document.getElementById('dashboard-prev');
    if (btnPrev) btnPrev.addEventListener('click', function () {
      var ws = (weekInput && weekInput.value) || weekStart;
      if (weekInput) weekInput.value = addDays(ws, -7);
      if (weekRangeEl) { weekRangeEl.textContent = formatWeekRange(weekInput.value); weekRangeEl.classList.remove('dashboard-week-range--loading'); }
      load();
    });
    var btnNext = document.getElementById('dashboard-next');
    if (btnNext) {
      btnNext.addEventListener('click', function () {
        var ws = (weekInput && weekInput.value) || weekStart;
        if (weekInput) weekInput.value = addDays(ws, 7);
        if (weekRangeEl) { weekRangeEl.textContent = formatWeekRange(weekInput.value); weekRangeEl.classList.remove('dashboard-week-range--loading'); }
        load();
      });
      var currentMonInit = getWeekStart(new Date());
      var selInit = (weekInput && weekInput.value) || weekStart;
      btnNext.disabled = (selInit === currentMonInit);
      btnNext.title = (selInit === currentMonInit) ? 'Semana actual' : 'Semana siguiente';
    }
    var excelFile = document.getElementById('dashboard-excel-file');
    var excelStatus = document.getElementById('dashboard-excel-status');
    var btnImportExcel = document.getElementById('dashboard-import-excel');
    if (btnImportExcel && excelFile) btnImportExcel.addEventListener('click', function () { excelFile.click(); });
    if (excelFile) excelFile.addEventListener('change', function () {
      var files = this.files;
      if (!files || files.length === 0) return;
      var ws = (weekInput && weekInput.value) || weekStart;
      var excelBtn = document.getElementById('dashboard-import-excel');
      if (excelBtn) excelBtn.disabled = true;
      var total = files.length;
      var allErrors = [];
      var totalCreated = 0;
      var totalUpdated = 0;
      var totalShifts = 0;
      function sendNext(index) {
        if (index >= total) {
          var msg = total === 1
            ? (totalCreated + ' días creados, ' + totalUpdated + ' actualizados, ' + totalShifts + ' turnos.')
            : (total + ' archivos: ' + totalCreated + ' días creados, ' + totalUpdated + ' actualizados, ' + totalShifts + ' turnos.');
          if (allErrors.length > 0) {
            msg += ' Errores: ' + allErrors.slice(0, 5).join('; ');
            if (allErrors.length > 5) msg += ' (+' + (allErrors.length - 5) + ' más)';
            excelStatus.className = 'dashboard-import-status error';
          } else {
            excelStatus.className = 'dashboard-import-status success';
          }
          excelStatus.textContent = msg;
          if (excelBtn) excelBtn.disabled = false;
          load();
          excelFile.value = '';
          return;
        }
        excelStatus.textContent = 'Enviando ' + (index + 1) + '/' + total + '…';
        excelStatus.className = 'dashboard-import-status';
        var fd = new FormData();
        fd.append('file', files[index]);
        auth.fetchWithAuth('/api/import/excel?weekStart=' + encodeURIComponent(ws), { method: 'POST', body: fd }).then(function (r) {
          if (r.status === 401) {
            if (global.LUCAS_APP && global.LUCAS_APP.onUnauthorized) global.LUCAS_APP.onUnauthorized();
            if (excelStatus) { excelStatus.textContent = 'Sesión expirada.'; excelStatus.className = 'dashboard-import-status error'; }
            if (excelBtn) excelBtn.disabled = false;
            excelFile.value = '';
            return null;
          }
          return r.json();
        }).then(function (data) {
          if (data === null) return;
          if (data) {
            totalCreated += data.days_created || 0;
            totalUpdated += data.days_updated || 0;
            totalShifts += data.shifts_updated || 0;
            if (data.errors && data.errors.length > 0) allErrors.push((files[index].name || '') + ': ' + data.errors.join(', '));
          }
          sendNext(index + 1);
        }).catch(function () {
          allErrors.push((files[index].name || '') + ': Error al enviar');
          sendNext(index + 1);
        });
      }
      sendNext(0);
    });
    var pdfFile = document.getElementById('dashboard-pdf-file');
    var pdfStatus = document.getElementById('dashboard-pdf-status');
    var btnImportPdf = document.getElementById('dashboard-import-pdf');
    if (btnImportPdf && pdfFile) btnImportPdf.addEventListener('click', function () { pdfFile.click(); });
    if (pdfFile) pdfFile.addEventListener('change', function () {
      if (!this.files || this.files.length === 0) return;
      var ws = (weekInput && weekInput.value) || weekStart;
      var pdfBtn = document.getElementById('dashboard-import-pdf');
      if (pdfBtn) pdfBtn.disabled = true;
      pdfStatus.textContent = 'Enviando…';
      pdfStatus.className = 'dashboard-import-status';
      var fileName = this.files[0] && this.files[0].name ? this.files[0].name : '';
      var fd = new FormData();
      fd.append('file', this.files[0]);
      auth.fetchWithAuth('/api/import/cuadrante-pdf?weekStart=' + encodeURIComponent(ws), { method: 'POST', body: fd }).then(function (r) {
        if (r.status === 401) {
          if (global.LUCAS_APP && global.LUCAS_APP.onUnauthorized) global.LUCAS_APP.onUnauthorized();
          pdfStatus.textContent = 'Sesión expirada.';
          pdfStatus.className = 'dashboard-import-status error';
          return null;
        }
        if (!r.ok) {
          return r.json().catch(function () { return {}; }).then(function (d) {
            throw new Error(d.message || ('Error del servidor (' + r.status + ')'));
          });
        }
        return r.json();
      }).then(function (data) {
        if (!data) return;
        var baseMsg = (data.message && data.message.length > 0)
          ? data.message
          : ((data.days_created || 0) + ' días creados, ' + (data.days_updated || 0) + ' actualizados, ' + (data.shifts_updated || 0) + ' turnos.');
        var msg = (fileName ? ('[' + fileName + '] ') : '') + baseMsg;
        if (data.errors && data.errors.length > 0) {
          var maxErr = 3;
          var shown = data.errors.slice(0, maxErr);
          msg += ' Errores: ' + shown.join(' ');
          if (data.errors.length > maxErr) msg += ' (+' + (data.errors.length - maxErr) + ' más)';
        }
        pdfStatus.textContent = msg;
        pdfStatus.className = data.errors && data.errors.length > 0 ? 'dashboard-import-status error' : 'dashboard-import-status success';
        load();
      }).catch(function (err) {
        var detail = (err && err.message) ? err.message : '';
        pdfStatus.textContent = detail ? ('Error al enviar: ' + detail) : 'Error al enviar.';
        pdfStatus.className = 'dashboard-import-status error';
      }).finally(function () {
        if (pdfBtn) pdfBtn.disabled = false;
      });
      this.value = '';
    });
    load();
  }
  global.LUCAS_DASHBOARD_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
