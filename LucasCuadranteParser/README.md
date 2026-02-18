# LucasCuadranteParser

Proyecto independiente para convertir el PDF de cuadrantes semanales de BETLEM en datos compatibles con Lucas. Pensado para probar el pipeline fuera del repositorio de Lucas antes de integrarlo.

## Requisitos

- Python 3.9+
- Dependencias: `pip install -r requirements.txt` (pdfplumber, python-dateutil)

## Uso

```bash
# Desde la carpeta LucasCuadranteParser
pip install -r requirements.txt

# Por defecto usa documentacion/horas.pdf (relativo a la raíz BETLEM)
python main.py

# Especificar PDF y carpeta de salida
python main.py "e:\BETLEM\documentacion\horas.pdf" --output-dir output

# Generar también CSV
python main.py documentacion/horas.pdf --csv
```

## Entrada

- PDF semanal de horarios BETLEM: cabecera con rango de semana (ej. del 09/02/2026 al 15/02/2026), días (Lunes 9 febrero, …), columnas 0h–23h, Total, Firma, nombres en dos líneas, totales en formato `08h00`, bloque "Puestos:", "Equipos:", detalle de turnos con rol y rango `HH:MM - HH:MM` o "Descanso semanal (8h)", "Abs", etc.

## Salida

- **JSON** (por defecto en `output/cuadrante_lucas.json`): por cada día de la semana:
  - `date`: fecha (YYYY-MM-DD)
  - `total_revenue`: 0 (no se obtiene del PDF)
  - `total_hours_worked`: suma de horas del día
  - `shifts`: array de `{ shift_name, staff_floor, staff_kitchen, hours_worked }` para **Mediodia**, **Tarde**, **Noche**

- **CSV** (opcional con `--csv`): filas por (date, shift_name, staff_floor, staff_kitchen, hours_worked).

## Código de colores del cuadrante

En el PDF, cada tipo de bloque tiene un color en la leyenda **Puestos**:

- **Colores de categoría** (trabajo en el restaurante): Manager (rosa), Camarero/a (naranja), Jefe de cocina (magenta), Segundo (verde), Cocinero/a (azul claro), Chef Oper (azul oscuro), Supervisor (amarillo). Esas horas **sí cuentan** para BETLEM.
- **Gris sólido** = **Ausencias** (descanso semanal, descanso compensatorio, recuperación festivo, Abs, ausencia injustificada). Esas horas **no cuentan**.
- **Gris con rayas** = **Otros est.** (otros establecimientos): el empleado trabaja ese día en otro restaurante (ej. "Camarero/a - CENTRIC", "Chef Operativo - MOLINA"). Esas horas **no cuentan** para BETLEM.

Regla simple: si el bloque no tiene el color de una categoría (manager, camarero, cocinero, etc.) sino gris o gris con rayas, **no está trabajando en el restaurante** y no sumamos esas horas.

El parser actual extrae **solo texto** del PDF (pdfplumber), no colores. Por eso detectamos "otros establecimientos" por el texto: cuando el rol lleva **" - NOMBRE"** (ej. " - CENTRIC", " - MOLINA"). Las ausencias se detectan por palabras clave (descanso semanal, Abs, ausencia, etc.). Una futura mejora sería usar el color del rectángulo en el PDF para clasificar automáticamente según la leyenda.

## Reglas

- **Ventanas de turno** (por defecto):
  - **Mediodía:** 10:00–16:00
  - **Tarde:** 16:01–20:00 (desde las 16:01 para no solapar con el cierre de Mediodía)
  - **Noche:** 20:00–01:00 del día siguiente (hasta la 1 de la madrugada)
  Puedes cambiarlas copiando `shift_windows.example.json` a `shift_windows.json` (formato `"HH:MM"`).
- Si una persona trabaja **al menos 1h30** en un turno, se cuenta como 1 en ese turno (sala o cocina según el puesto).
- **Personal de sala** (staff_floor): Manager, Camarero/a, Jefe de sala, Segundo de sala (y variantes).
- **Personal de cocina** (staff_kitchen): Jefe de cocina, Segundo de cocina, Chef Operativo, Cocinero/a (y variantes).
- Puestos del PDF se mapean en `config.ROLE_TO_AREA`; las claves más específicas primero (ej. "segundo de cocina" antes que "segundo").
- **No cuentan** para BETLEM: ausencias (por texto: descanso, Abs, ausencia) y otros establecimientos (por texto: rol con " - NOMBRE", ej. CENTRIC, MOLINA).

## Integración con Lucas

El JSON generado es compatible con el formato que usa la API de Lucas (ExecutionDay + ShiftFeedbacks: `date`, `total_revenue`, `total_hours_worked`, `shifts` con `shift_name`, `staff_floor`, `staff_kitchen`, `hours_worked`). Más adelante se podrá importar vía API o script que llame a este pipeline y envíe los datos a Lucas.

## Estructura

- `config.py`: ventanas de turno, umbral 2 h, mapa sala/cocina
- `main.py`: entrada = ruta PDF, salida = JSON (y opcional CSV)
- `pipeline/`: ingest, segment, entities, relations, normalize
- `output/`: carpeta de salida por defecto
- `docs/formato_salida.md`: descripción del formato de salida
