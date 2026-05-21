# 🎙️ DaVinci Resolve Silence Cutter GUI

> **Herramienta portable para eliminar silencios automáticamente de tu video y generar un Timeline listo para importar en DaVinci Resolve (versión gratuita).**

---

## ✨ ¿Qué hace?

- 🔍 Detecta silencios en tu video usando FFmpeg internamente (sin instalar nada)
- ✂️ Genera un archivo `.edl` con cortes precisos conservando solo los segmentos de voz
- 📂 El archivo EDL se importa directo en DaVinci Resolve como un timeline completo
- 🎛️ Ajuste de offset/padding para no cortar sílabas en los bordes
- 💾 Recuerda la última carpeta usada
- 🖥️ Controlable desde GUI o por línea de comandos (CLI)

---

## 🚀 Descarga el EXE Portable (Listo para usar, cero instalaciones)

👉 **[Ir a la sección de Releases](../../releases)**

Descarga el archivo `SilenceCutterGUI.exe` desde la última Release. Funciona en cualquier PC con **Windows 10/11** sin instalar nada más.

> ⚠️ El EXE lleva FFmpeg incrustado en su interior. La primera vez que lo abres, lo extraerá automáticamente a una carpeta temporal.

---

## 🛠️ Cómo usar

### Modo GUI (interfaz gráfica)
1. Descarga `SilenceCutterGUI.exe` de las Releases
2. Haz doble clic — no necesitas instalar nada
3. Selecciona tu archivo de video con "Buscar"
4. Ajusta el **Umbral de Silencio (dB)**, **Duración mínima** y **Padding**
5. Pulsa **⚡ Generar EDL**
6. Importa el `.edl` generado en **DaVinci Resolve → File → Import → Timeline**

### Modo CLI (automatización)
```bash
SilenceCutterGUI.exe --video "C:\ruta\video.mov" --offset 0.15 --threshold -35 --autorun
```

| Parámetro | Descripción | Default |
|---|---|---|
| `--video` | Ruta al archivo de video | - |
| `--offset` | Padding en segundos antes/después del corte | `0.15` |
| `--threshold` | Umbral de silencio en dB | `-35` |
| `--autorun` | Ejecutar automáticamente al abrir | - |

---

## 📁 Estructura del Repositorio

```
davinci_resolve_silencer/
├── SilenceCutterGUI/           ← Proyecto C# (.NET 8 WinForms)
│   ├── Form1.cs                ← Lógica principal + GUI + generación EDL
│   ├── Program.cs              ← Punto de entrada + soporte CLI
│   ├── SilenceCutterGUI.csproj ← Proyecto con ffmpeg/ffprobe incrustados
│   ├── ffmpeg.exe              ← Motor de audio (incrustado en el EXE final)
│   └── ffprobe.exe             ← Herramienta de análisis (incrustada)
├── auto_recortes.py            ← Script Python original (referencia)
├── auto_marcadores.py          ← Script Python de marcadores (referencia)
└── .gitignore
```

---

## 📦 Cómo compilar desde el código fuente

Requiere: [.NET 8 SDK](https://dotnet.microsoft.com/download)

```bash
cd SilenceCutterGUI
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El EXE portable quedará en:
`SilenceCutterGUI/bin/Release/net8.0-windows/win-x64/publish/SilenceCutterGUI.exe`

---

## 📋 Requisitos

| Componente | Requerido | Notas |
|---|---|---|
| Windows 10/11 x64 | ✅ Sí | Mínimo Windows 10 |
| .NET 8 Runtime | ❌ No | Ya incluido en el EXE |
| FFmpeg | ❌ No | Ya incrustado en el EXE |
| DaVinci Resolve | ✅ Sí | Para importar el EDL generado |
| GPU dedicada | ❌ No | Solo usa CPU para el análisis |

---

## 📝 Formato de salida

El archivo `.edl` generado sigue el estándar **CMX 3600** compatible con DaVinci Resolve, Premiere Pro y cualquier NLE profesional. Se guarda en la misma carpeta que el video original con el sufijo `_CORTES_FINAL.edl`.

---

## 🧑‍💻 Autor

Desarrollado para podcast de producción personal. Ingeniería inversa del formato EDL de DaVinci Resolve para la versión gratuita que no expone API.
