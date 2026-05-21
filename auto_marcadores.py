import subprocess
import re
import sys
import os

def check_ffmpeg():
    """Verifica si ffmpeg está instalado y accesible en el sistema."""
    try:
        subprocess.run(["ffmpeg", "-version"], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        return True
    except FileNotFoundError:
        return False

def get_silences(video_path, threshold="-35dB", duration="0.8"):
    """
    Usa ffmpeg para extraer de forma precisa los tiempos de inicio y fin de silencios en un archivo de audio o video.
    """
    command = [
        "ffmpeg", "-i", video_path, 
        "-af", f"silencedetect=noise={threshold}:d={duration}", 
        "-f", "null", "-"
    ]
    
    print(f"Analizando frecuencias y audio en '{os.path.basename(video_path)}'...")
    # Se ejecuta el comando de ffmpeg y se capturan los resultados
    result = subprocess.run(command, stderr=subprocess.PIPE, text=True, encoding='utf-8')
    
    silences = []
    # Usamos expresiones regulares para extraer los datos crudos del log de FFmpeg
    starts = re.findall(r'silence_start: ([\d\.]+)', result.stderr)
    ends = re.findall(r'silence_end: ([\d\.]+)', result.stderr)
    
    for s, e in zip(starts, ends):
        silences.append((float(s), float(e)))
        
    return silences

def frame_to_timecode(seconds, fps=30):
    """Convierte un tiempo en segundos puros a Timecode (HH:MM:SS:FF) estándar para EDL."""
    h = int(seconds // 3600)
    m = int((seconds % 3600) // 60)
    s = int(seconds % 60)
    f = int((seconds - int(seconds)) * fps)
    return f"{h:02d}:{m:02d}:{s:02d}:{f:02d}"

def generate_edl(silences, edl_path, fps=30):
    """Construye un archivo EDL (Edit Decision List) estructurado para importación directa en Resolve"""
    with open(edl_path, "w", encoding='utf-8') as f:
        f.write("TITLE: Auto-Marcadores de Silencio\n")
        f.write("FCM: NON-DROP FRAME\n\n")
        
        for i, (start, end) in enumerate(silences, 1):
            tc_start = frame_to_timecode(start, fps)
            tc_end = frame_to_timecode(end, fps)
            
            # Evento en la línea de tiempo. Usamos cortes artificiales usando los TCs
            f.write(f"{i:03d}  AX       V     C        {tc_start} {tc_start} {tc_start} {tc_start}\n")
            # Inyección explícita del marcador azul
            f.write(f"|C:ResolveColorBlue |M:Silencio Muteado |D:1\n\n")

if __name__ == "__main__":
    print("=== Detección de Silencio por Ingeniería Inversa de EDL para DaVinci ===")
    
    if not check_ffmpeg():
        print("ERROR: FFmpeg no está instalado o no se detecta en las variables de entorno.")
        print("Por favor, instala ffmpeg (https://ffmpeg.org/download.html) primero.")
        sys.exit(1)
        
    if len(sys.argv) < 2:
        print("\nUso correcto:")
        print("  python auto_marcadores.py \"ruta/a/tu/video_o_audio.mp4\"")
        print("\nSi el archivo tiene espacios en el nombre, guárdalo entre comillas.")
        sys.exit(1)
        
    video_file = sys.argv[1]
    
    if not os.path.exists(video_file):
        print(f"ERROR: No se encontró el archivo '{video_file}'")
        sys.exit(1)
        
    edl_file = video_file + "_marcadores.edl"
    
    # Parámetros por default. Puedes editarlos:
    # threshold = Nivel de volumen que se cuenta como silencio (-35 dB a -45 dB funciona bien)
    # duration = Tiempo minimo de silencio a detectar (en segundos)
    db_threshold = "-35dB"
    sec_duration = "0.8"
    
    silences = get_silences(video_file, threshold=db_threshold, duration=sec_duration)
    
    if not silences:
        print("\n[!] No se encontraron silencios lo suficientemente largos basado en los parámetros.")
    else:
        generate_edl(silences, edl_file)
        print(f"\n[+] ¡Éxito! Se inyectaron correctamente {len(silences)} marcadores detectados.")
        print(f"[+] Archivo EDL generado (Edit Decision List) -> {edl_file}")
        print("\n" + "="*50)
        print(" INSTRUCCIONES PARA APLICAR EN DAVINCI RESOLVE")
        print("="*50)
        print(" 1. Arrastra tu clip original a la línea de tiempo (Timeline).")
        print(" 2. Haz clic derecho sobre la secuencia de tu Timeline en el Media Pool.")
        print(" 3. Ve a 'Timelines' -> 'Import' -> 'Timeline Markers From EDL...'")
        print(f" 4. Selecciona el archivo:\n    {edl_file}")
        print(" 5. ¡Todos tus marcadores azules de silencio aparecerán mágicamente!")
        print("="*50)
