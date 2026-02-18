# Datos de idioma para OCR (Tesseract)

Para que la **carga de imagen** (PNG/JPG) funcione, Tesseract necesita archivos de idioma en esta carpeta.

## Descarga

1. **Español (recomendado):** [spa.traineddata](https://github.com/tesseract-ocr/tessdata_fast/raw/main/spa.traineddata)  
   Guardar como `spa.traineddata` en esta carpeta.

2. **Inglés (alternativa):** [eng.traineddata](https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata)  
   Guardar como `eng.traineddata` en esta carpeta.

Repositorio completo: https://github.com/tesseract-ocr/tessdata_fast

## Estructura

```
tessdata/
  README.md
  spa.traineddata   ← descargar
  eng.traineddata   ← opcional
```

Al ejecutar la API, la carpeta `tessdata` debe estar junto al ejecutable (por ejemplo en `bin/Debug/net8.0/tessdata/`). Si usas Visual Studio o `dotnet run`, copia esta carpeta (con los .traineddata) a la salida del proyecto o configúrala como “Content” y “Copy if newer”.
