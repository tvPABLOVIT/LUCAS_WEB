/**
 * Scoring de turnos: V/R/M/D, SGT, Estado, Tipo T1–T15, Resumen Nivel 3.
 * Basado en SCORING_COMPLETO_VRMD_SGT.md (adaptado a la app web).
 */
(function (global) {
  'use strict';

  var SgtMin = 6;
  var SgtMax = 31;

  var Q1_OPTIONS = ['Pocas mesas', 'Media sala', 'Sala completa', 'Sala y terraza completas', 'Sala y terraza completas y doblamos mesas'];
  var Q2_OPTIONS = ['Muy espaciadas, sin acumulación', 'Entradas tranquilas', 'Flujo constante', 'Muchas entradas juntas', 'Entradas continuas sin margen'];
  var Q3_OPTIONS = ['Siempre adelantado', 'Generalmente con margen', 'Justo', 'Poco margen', 'Ningún margen'];
  var Q4_OPTIONS = ['Muy fácil', 'Fácil', 'Normal', 'Difícil', 'Muy difícil'];

  function optionToIndex(text, options) {
    if (!text || typeof text !== 'string') return 0;
    var t = text.trim();
    for (var i = 0; i < options.length; i++) {
      if (options[i].trim() === t) return i + 1;
    }
    return 0;
  }

  function getVolumenIndex(q1) { return optionToIndex(q1, Q1_OPTIONS); }
  function getRitmoIndex(q2) { return optionToIndex(q2, Q2_OPTIONS); }
  function getMargenIndex(q3) { return optionToIndex(q3, Q3_OPTIONS); }
  function getDificultadIndex(q4) { return optionToIndex(q4, Q4_OPTIONS); }

  /**
   * Calcula SGT = (V×2) + R + (6−M) + D. Devuelve 0 si falta algún eje (1-5).
   */
  function calcSgt(v, r, m, d) {
    if (v < 1 || v > 5 || r < 1 || r > 5 || m < 1 || m > 5 || d < 1 || d > 5) return 0;
    return (v * 2) + r + (6 - m) + d;
  }

  /**
   * Estado del turno (Nivel 1) según SGT.
   */
  function getEstado(sgt) {
    if (sgt < 6) return null;
    if (sgt <= 10) return 'Infrautilizado';
    if (sgt <= 14) return 'Tranquilo';
    if (sgt <= 18) return 'Equilibrado';
    if (sgt <= 22) return 'Productivo';
    if (sgt <= 26) return 'Exigente';
    if (sgt <= 31) return 'Crítico';
    return null;
  }

  /**
   * Tipo T1–T15 según estado + V,R,M,D.
   */
  function getTipoTurno(estado, v, r, m, d) {
    var ritmoAlto = r >= 4;
    var margenBajo = m >= 4;
    var volumenBajo = v <= 2;
    var volumenAlto = v >= 4;
    var dificultadAlta = d >= 4;
    var margenAlto = m <= 2;

    if (estado === 'Infrautilizado') return { tipo: 'T3', label: 'Infrautilizado estructural' };
    if (estado === 'Tranquilo') {
      if (volumenBajo) return { tipo: 'T1', label: 'Tranquilo por falta de volumen' };
      return { tipo: 'T2', label: 'Tranquilo por buen reparto' };
    }
    if (estado === 'Equilibrado') {
      if (margenAlto) return { tipo: 'T5', label: 'Equilibrado con holgura' };
      return { tipo: 'T4', label: 'Equilibrado estándar' };
    }
    if (estado === 'Productivo') {
      if (ritmoAlto && margenBajo) return { tipo: 'T7', label: 'Productivo con picos' };
      if (volumenAlto && dificultadAlta) return { tipo: 'T8', label: 'Productivo sobredimensionado' };
      return { tipo: 'T6', label: 'Productivo estable' };
    }
    if (estado === 'Exigente') {
      if (ritmoAlto) return { tipo: 'T9', label: 'Exigente por ritmo' };
      if (margenBajo) return { tipo: 'T10', label: 'Exigente por falta de margen' };
      return { tipo: 'T11', label: 'Tenso por desajuste' };
    }
    if (estado === 'Crítico') {
      if (volumenAlto) return { tipo: 'T12', label: 'Crítico por volumen' };
      if (ritmoAlto) return { tipo: 'T13', label: 'Crítico por distribución' };
      return { tipo: 'T14', label: 'Crítico estructural' };
    }
    if (margenAlto && dificultadAlta) return { tipo: 'T15', label: 'Incoherente por percepción' };
    return { tipo: 'T4', label: 'Equilibrado estándar' };
  }

  function fraseVolumen(v) {
    if (v <= 2) return 'la carga de trabajo fue baja';
    if (v === 3) return 'el nivel de trabajo fue el esperado';
    if (v === 4) return 'el volumen fue alto';
    if (v === 5) return 'el volumen fue muy alto';
    return '';
  }
  function fraseRitmo(r) {
    if (r <= 2) return 'las entradas se repartieron bien';
    if (r === 3) return 'el flujo fue constante';
    if (r === 4) return 'hubo varios picos de entrada';
    if (r === 5) return 'las entradas fueron continuas, sin apenas respiro';
    return '';
  }
  function fraseMargen(m) {
    if (m <= 2) return 'se trabajó con margen';
    if (m === 3) return 'el margen fue justo';
    if (m === 4) return 'hubo poco margen';
    if (m === 5) return 'no hubo margen operativo';
    return '';
  }
  function fraseDificultad(d) {
    if (d <= 2) return 'el turno fue tranquilo';
    if (d === 3) return 'el turno tuvo una dificultad normal';
    if (d === 4) return 'el turno fue difícil';
    if (d === 5) return 'el turno fue muy difícil';
    return '';
  }

  function getCierreResumenPorTipo(tipo) {
    switch (tipo) {
      case 'T15': return 'Conviene revisar los datos por posible incoherencia.';
      case 'T10': case 'T11': return 'Turno exigente; valorar refuerzos o ajustes.';
      case 'T12': case 'T13': case 'T14': return 'Turno crítico; valorar refuerzos o ajustes.';
      case 'T1': case 'T2': return 'En conjunto, turno controlado.';
      case 'T3': return 'En conjunto, turno con poco uso; sin necesidad de refuerzos.';
      case 'T4': case 'T5': case 'T6': return 'Turno alineado y con control.';
      case 'T7': case 'T8': return 'Turno productivo; valorar si el nivel de recursos fue adecuado.';
      case 'T9': return 'Turno exigente; valorar refuerzos o ajustes.';
      default: return 'Turno alineado y con control.';
    }
  }

  function capitalize(s) {
    if (!s || s.length === 0) return s;
    return s.charAt(0).toUpperCase() + s.slice(1);
  }

  /**
   * Resumen Nivel 3: párrafo con frases por eje + cierre según tipo.
   */
  function buildResumenNivel3(v, r, m, d, sgt) {
    if (v < 1 || v > 5 || r < 1 || r > 5 || m < 1 || m > 5 || d < 1 || d > 5) {
      return 'Completa Volumen, Ritmo, Margen y Dificultad para ver el resumen del turno.';
    }
    var fv = fraseVolumen(v);
    var fr = fraseRitmo(r);
    var fm = fraseMargen(m);
    var fd = fraseDificultad(d);
    var estado = getEstado(sgt);
    var tipoInfo = getTipoTurno(estado, v, r, m, d);
    var cierre = getCierreResumenPorTipo(tipoInfo.tipo);
    return capitalize(fv) + ', ' + fr + ', ' + fm + ' y ' + fd + '. ' + cierre;
  }

  /**
   * A partir de un turno (objeto con feedback_q1..q4), calcula índices, SGT, estado, tipo y resumen.
   */
  function scoreFromShift(shift) {
    var q1 = shift && (shift.feedback_q1 != null ? shift.feedback_q1 : shift.FeedbackQ1);
    var q2 = shift && (shift.feedback_q2 != null ? shift.feedback_q2 : shift.FeedbackQ2);
    var q3 = shift && (shift.feedback_q3 != null ? shift.feedback_q3 : shift.FeedbackQ3);
    var q4 = shift && (shift.feedback_q4 != null ? shift.feedback_q4 : shift.FeedbackQ4);
    var v = getVolumenIndex(q1);
    var r = getRitmoIndex(q2);
    var m = getMargenIndex(q3);
    var d = getDificultadIndex(q4);
    var sgt = calcSgt(v, r, m, d);
    var estado = getEstado(sgt);
    var tipoInfo = estado != null ? getTipoTurno(estado, v, r, m, d) : (v && r && m && d ? getTipoTurno('Equilibrado', v, r, m, d) : { tipo: null, label: '' });
    var resumen = buildResumenNivel3(v, r, m, d, sgt);
    return {
      v: v, r: r, m: m, d: d,
      sgt: sgt,
      estado: estado,
      tipo: tipoInfo.tipo,
      tipoLabel: tipoInfo.label,
      resumenNivel3: resumen,
      completo: v >= 1 && r >= 1 && m >= 1 && d >= 1
    };
  }

  global.LUCAS_SCORING = {
    SgtMin: SgtMin,
    SgtMax: SgtMax,
    getVolumenIndex: getVolumenIndex,
    getRitmoIndex: getRitmoIndex,
    getMargenIndex: getMargenIndex,
    getDificultadIndex: getDificultadIndex,
    calcSgt: calcSgt,
    getEstado: getEstado,
    getTipoTurno: getTipoTurno,
    getCierreResumenPorTipo: getCierreResumenPorTipo,
    buildResumenNivel3: buildResumenNivel3,
    scoreFromShift: scoreFromShift
  };
})(typeof window !== 'undefined' ? window : this);
