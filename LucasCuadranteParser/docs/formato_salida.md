# Formato de salida (Lucas)

El pipeline genera un JSON por semana con la siguiente estructura.

## Por día

Cada elemento del array es un día:

```json
{
  "date": "2026-02-09",
  "total_revenue": 0,
  "total_hours_worked": 42.5,
  "shifts": [
    {
      "shift_name": "Mediodia",
      "staff_floor": 2,
      "staff_kitchen": 1,
      "hours_worked": 12.0
    },
    {
      "shift_name": "Tarde",
      "staff_floor": 2,
      "staff_kitchen": 2,
      "hours_worked": 8.0
    },
    {
      "shift_name": "Noche",
      "staff_floor": 2,
      "staff_kitchen": 2,
      "hours_worked": 10.5
    }
  ]
}
```

## Campos

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `date` | string | Fecha en ISO (YYYY-MM-DD) |
| `total_revenue` | number | Siempre 0 (el PDF no incluye facturación) |
| `total_hours_worked` | number | Suma de horas trabajadas ese día |
| `shifts` | array | Tres objetos: Mediodia, Tarde, Noche |
| `shifts[].shift_name` | string | "Mediodia", "Tarde" o "Noche" |
| `shifts[].staff_floor` | number | Personas en sala en ese turno |
| `shifts[].staff_kitchen` | number | Personas en cocina en ese turno |
| `shifts[].hours_worked` | number | Horas trabajadas en la ventana del turno |

## Turno partido

En el PDF, una misma persona puede tener varios rangos en una línea (ej. `"12:00 - 16:00 20:00 - 00:00"`): **turno partido** (sale a las 16h, vuelve a las 20h y sale a las 00h = 2 turnos el mismo día). El pipeline genera un turno por cada rango, con el mismo rol; cada uno se cuenta en la ventana correspondiente (Mediodía, Tarde, Noche) según las horas que solapen.

## Compatibilidad con Lucas

- Coincide con el formato de `ExecutionDay` + `ShiftFeedback`: `date`, `total_revenue`, `total_hours_worked`, y por turno `shift_name`, `staff_floor`, `staff_kitchen`, `hours_worked`.
- Los nombres de turno son exactamente "Mediodia", "Tarde", "Noche" para que la API de Lucas los acepte.
