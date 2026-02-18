# Scoring completo: V/R/M/D, SGT, Estado y Tipo (T1–T15)

Documento que describe **todo** el sistema de scoring de turnos: los cuatro ejes (Volumen, Ritmo, Margen, Dificultad), la fórmula SGT, los estados (Nivel 1), los tipos T1–T15 (Nivel 2), las frases naturales y los cierres de resumen. Implementado en `TurnoScoringService` (Windows) y `TurnoResumenBuilder` (Core); las reglas son las mismas en ambos.

---

## 1. Los cuatro ejes (V, R, M, D)

Cada turno se valora con **cuatro preguntas** (Q1–Q4). Cada respuesta se traduce en un **índice del 1 al 5**:

- **1** = bajo / favorable (poco trabajo, tranquilo, con margen, fácil).
- **5** = alto / desfavorable (mucho trabajo, ritmo fuerte, sin margen, muy difícil).

Si falta alguna respuesta, el índice es **0** y no se calcula SGT ni estado/tipo hasta tener los cuatro.

### 1.1. V — Volumen (Q1: “¿Cuánto trabajo hubo en este turno?”)

| Índice | Opción (texto exacto guardado) |
|--------|-------------------------------|
| 1 | Pocas mesas |
| 2 | Media sala |
| 3 | Sala completa |
| 4 | Sala y terraza completas |
| 5 | Sala y terraza completas y doblamos mesas |

**Interpretación:** 1 = poca carga; 5 = carga máxima (sala + terraza + doblar mesas).

---

### 1.2. R — Ritmo (Q2: “¿Cómo fue el ritmo de entradas de clientes?”)

| Índice | Opción (texto exacto) |
|--------|------------------------|
| 1 | Muy espaciadas, sin acumulación |
| 2 | Entradas tranquilas |
| 3 | Flujo constante |
| 4 | Muchas entradas juntas |
| 5 | Entradas continuas sin margen |

**Interpretación:** 1 = entradas muy repartidas; 5 = entradas continuas, sin respiro.

---

### 1.3. M — Margen (Q3: “¿Cuánto margen hubo para ir adelantado con el trabajo?”)

| Índice | Opción (texto exacto) |
|--------|------------------------|
| 1 | Siempre adelantado |
| 2 | Generalmente con margen |
| 3 | Justo |
| 4 | Poco margen |
| 5 | Ningún margen |

**Interpretación:** 1 = siempre con holgura; 5 = sin margen operativo. En la fórmula SGT se usa **(6−M)** para que más margen (M bajo) suba el score.

---

### 1.4. D — Dificultad (Q4: “¿Qué tan duro fue el turno en general?”)

| Índice | Opción (texto exacto) |
|--------|------------------------|
| 1 | Muy fácil |
| 2 | Fácil |
| 3 | Normal |
| 4 | Difícil |
| 5 | Muy difícil |

**Interpretación:** 1 = muy fácil; 5 = muy difícil (vivencia global del turno).

---

## 2. Fórmula SGT (Score Global del Turno)

**Fórmula:**

```
SGT = (V × 2) + R + (6 − M) + D
```

- **V, R, M, D** deben estar todos entre 1 y 5. Si alguno es 0 (falta respuesta), **SGT = 0** y no se calcula estado ni tipo.
- **Rango posible:** mínimo 6 (V=1,R=1,M=5,D=1 → 2+1+1+1), máximo 31 (V=5,R=5,M=1,D=5 → 10+5+5+5).

**Constantes en código:** `SgtMin = 6`, `SgtMax = 31`.

**Por qué (6−M):** M = 1 (siempre adelantado) aporta 5 puntos; M = 5 (ningún margen) aporta 1. Así, más margen sube el SGT y menos margen lo baja, alineado con “más holgura = situación más favorable en el índice”.

---

## 3. Estado del turno (Nivel 1)

El **estado** depende solo de **SGT** (rangos fijos):

| SGT        | Estado          |
|-----------|------------------|
| &lt; 6     | — (sin datos)    |
| 6 – 10    | **Infrautilizado** |
| 11 – 14   | **Tranquilo**    |
| 15 – 18   | **Equilibrado**  |
| 19 – 22   | **Productivo**   |
| 23 – 26   | **Exigente**     |
| 27 – 31   | **Crítico**      |

**Uso:** Nivel 1 de lectura rápida: Infrautilizado/Tranquilo = poco uso, sin necesidad de refuerzos; Exigente/Crítico = valorar refuerzos o ajustes.

---

## 4. Tipo del turno (Nivel 2): T1–T15

El **tipo** se obtiene a partir de **estado** (SGT) y de los **índices V, R, M, D**. Se usan estos umbrales:

- **ritmoAlto** = R ≥ 4  
- **margenBajo** = M ≥ 4  
- **volumenBajo** = V ≤ 2  
- **volumenAlto** = V ≥ 4  
- **dificultadAlta** = D ≥ 4  
- **margenAlto** = M ≤ 2  

Árbol de decisión (en orden):

| Estado          | Condición adicional                    | Tipo |
|-----------------|----------------------------------------|------|
| Infrautilizado  | —                                      | **T3** — Infrautilizado estructural |
| Tranquilo      | volumenBajo                            | **T1** — Tranquilo por falta de volumen |
| Tranquilo      | no volumenBajo                         | **T2** — Tranquilo por buen reparto |
| Equilibrado    | margenAlto                             | **T5** — Equilibrado con holgura |
| Equilibrado    | no margenAlto                          | **T4** — Equilibrado estándar |
| Productivo     | ritmoAlto **y** margenBajo             | **T7** — Productivo con picos |
| Productivo     | volumenAlto **y** dificultadAlta       | **T8** — Productivo sobredimensionado |
| Productivo     | resto                                  | **T6** — Productivo estable |
| Exigente       | ritmoAlto                              | **T9** — Exigente por ritmo |
| Exigente       | margenBajo                             | **T10** — Exigente por falta de margen |
| Exigente       | resto                                  | **T11** — Tenso por desajuste |
| Crítico        | volumenAlto                            | **T12** — Crítico por volumen |
| Crítico        | ritmoAlto                              | **T13** — Crítico por distribución |
| Crítico        | resto                                  | **T14** — Crítico estructural |
| (fallback)     | margenAlto **y** dificultadAlta        | **T15** — Incoherente por percepción |
| (fallback)     | resto                                  | **T4** — Equilibrado estándar |

**Nota:** T15 se usa cuando el estado no es ninguno de los seis anteriores pero M≤2 y D≥4 (margen alto y dificultad alta a la vez → posible incoherencia en las respuestas).

---

## 5. Frases naturales por eje (para el resumen en texto)

Se usan para construir el **resumen en lenguaje natural** (Nivel 3). Cada eje tiene una frase según el **índice** (no el texto de la opción):

### 5.1. Volumen (V)

| V    | Frase |
|------|--------|
| 1–2  | la carga de trabajo fue baja |
| 3    | el nivel de trabajo fue el esperado |
| 4    | el volumen fue alto |
| 5    | el volumen fue muy alto |

### 5.2. Ritmo (R)

| R    | Frase |
|------|--------|
| 1–2  | las entradas se repartieron bien |
| 3    | el flujo fue constante |
| 4    | hubo varios picos de entrada |
| 5    | las entradas fueron continuas, sin apenas respiro |

### 5.3. Margen (M)

| M    | Frase |
|------|--------|
| 1–2  | se trabajó con margen |
| 3    | el margen fue justo |
| 4    | hubo poco margen |
| 5    | no hubo margen operativo |

### 5.4. Dificultad (D)

| D    | Frase |
|------|--------|
| 1–2  | el turno fue tranquilo |
| 3    | el turno tuvo una dificultad normal |
| 4    | el turno fue difícil |
| 5    | el turno fue muy difícil |

---

## 6. Resumen Nivel 3 (párrafo completo)

**BuildResumenNivel3(v, r, m, d, sgt):**

1. Si falta algún eje (V,R,M,D no están entre 1 y 5), se devuelve:  
   *"Completa Volumen, Ritmo, Margen y Dificultad para ver el resumen del turno."*
2. En caso contrario:
   - Se obtienen las cuatro frases (Volumen, Ritmo, Margen, Dificultad).
   - Se obtiene el **tipo** (T1–T15) con GetTipoTurno.
   - Se obtiene el **cierre** con GetCierreResumenPorTipo(tipo).
   - Se forma: **"[FraseV], [fraseR], [fraseM] y [fraseD].[cierre]"** (la primera frase con mayúscula inicial).

### 6.1. Cierre según tipo (GetCierreResumenPorTipo)

| Tipo   | Cierre |
|--------|--------|
| T15    | Conviene revisar los datos por posible incoherencia. |
| T10, T11 | Turno exigente; valorar refuerzos o ajustes. |
| T12, T13, T14 | Turno crítico; valorar refuerzos o ajustes. |
| T1, T2 | En conjunto, turno controlado. |
| T3     | En conjunto, turno con poco uso; sin necesidad de refuerzos. |
| T4, T5, T6 | Turno alineado y con control. |
| T7, T8 | Turno productivo; valorar si el nivel de recursos fue adecuado. |
| T9     | Turno exigente; valorar refuerzos o ajustes. |

---

## 7. Dónde se usa el scoring

| Lugar | Uso |
|-------|-----|
| **ShiftItemViewModel** (Windows) | VIndex, RIndex, MIndex, DIndex, Sgt, EstadoTurno, TipoTurno, ResumenNivel3 a partir de FeedbackQ1–Q4. |
| **RegistroService** (Windows) | Cálculo de SGT y estado por turno al guardar/análisis. |
| **InteligenciaService** (Windows) | GetFeedbackStrength(day): usa V,R,M,D para ponderar días en la predicción (días con más “actividad” pesan más). |
| **TurnoResumenBuilder** (Core) | BuildResumenFromFeedback(q1,q2,q3,q4) para Google Sheet / backend: mismo SGT, estado, tipo y párrafo Nivel 3. |
| **RegistroViewModel** (Windows) | Filtros o listas que usan Sgt ≥ SgtMin (6). |

Las respuestas Q1–Q4 se guardan como **texto** (FeedbackQ1–FeedbackQ4); los índices y el SGT se calculan al leer, no se persisten.

---

## 8. Tabla resumen: de la opción al tipo

Flujo en una sola línea:

**Opción (texto)** → **Índice 1–5** (GetVolumenIndex / GetRitmoIndex / GetMargenIndex / GetDificultadIndex) → **SGT** = (V×2)+R+(6−M)+D → **Estado** (por rango SGT) → **Tipo** T1–T15 (por estado + V,R,M,D) → **Frases + cierre** → **Resumen Nivel 3**.

Con este documento se tiene el scoring completo: ejes, opciones, fórmula SGT, estados, tipos T1–T15, frases y cierres, y dónde se aplica en el código.
