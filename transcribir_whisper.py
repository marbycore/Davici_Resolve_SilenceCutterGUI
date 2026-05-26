import sys
import json
import os

try:
    from faster_whisper import WhisperModel
except ImportError:
    print("ERROR: faster-whisper no está instalado. Ejecuta: pip install faster-whisper")
    sys.exit(1)

# Asegurar que la salida sea UTF-8 para evitar problemas con caracteres especiales en C#
if sys.stdout.encoding != 'utf-8':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

def transcribe(audio_path):
    # Usamos CUDA para máxima velocidad ya que detectamos GPU
    # Model "large-v3" para máxima calidad en una 5080
    model_size = "large-v3"
    
    print(f"Cargando modelo {model_size} en GPU...")
    model = WhisperModel(model_size, device="cuda", compute_type="float16")

    print(f"Transcribiendo: {audio_path}")
    segments, info = model.transcribe(audio_path, beam_size=5)

    results = []
    for segment in segments:
        print(f"[{segment.start:.2f}s -> {segment.end:.2f}s] {segment.text}")
        results.append({
            "start": round(segment.start, 2),
            "end": round(segment.end, 2),
            "text": segment.text.strip()
        })

    return results

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Uso: python transcribir_whisper.py <ruta_del_video>")
        sys.exit(1)

    video_path = sys.argv[1]
    if not os.path.exists(video_path):
        print(f"Error: No existe {video_path}")
        sys.exit(1)

    try:
        data = transcribe(video_path)
        # Imprimimos el JSON final para que el C# lo capture
        print("---JSON_START---")
        print(json.dumps(data, indent=2, ensure_ascii=False))
        print("---JSON_END---")
    except Exception as e:
        print(f"Error durante la transcripción: {e}")
        sys.exit(1)
