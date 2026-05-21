import subprocess
import re
import sys
import os

def get_video_duration(video_path):
    """Obtiene la duracion total del video usando ffprobe."""
    cmd = [
        "ffprobe", "-v", "error", "-show_entries",
        "format=duration", "-of",
        "default=noprint_wrappers=1:nokey=1", video_path
    ]
    try:
        res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        return float(res.stdout.strip())
    except Exception as e:
        print("Error obteniendo duracion:", e)
        return 0.0

def get_silences(video_path, threshold="-35dB", duration="0.8"):
    """Usa ffmpeg para extraer de forma precisa los tiempos de inicio y fin de silencios."""
    command = [
        "ffmpeg", "-i", video_path, 
        "-af", f"silencedetect=noise={threshold}:d={duration}", 
        "-f", "null", "-"
    ]
    
    print(f"Analizando voz en '{os.path.basename(video_path)}'...")
    result = subprocess.run(command, stderr=subprocess.PIPE, text=True, encoding='utf-8')
    
    silences = []
    starts = re.findall(r'silence_start: ([\d\.]+)', result.stderr)
    ends = re.findall(r'silence_end: ([\d\.]+)', result.stderr)
    
    for s, e in zip(starts, ends):
        silences.append((float(s), float(e)))
        
    return silences

def frame_to_timecode(seconds, fps=30):
    """Convierte un tiempo en segundos a Timecode (HH:MM:SS:FF) estándar."""
    h = int(seconds // 3600)
    m = int((seconds % 3600) // 60)
    s = int(seconds % 60)
    f = int((seconds - int(seconds)) * fps)
    return f"{h:02d}:{m:02d}:{s:02d}:{f:02d}"

def generate_edl_cuts(silences, edl_path, total_duration, fps=30):
    """Genera un archivo EDL que *corta* el video, manteniendo solo las partes donde hablan."""
    # Calcular los segmentos a mantener (las voces)
    keeps = []
    current_time = 0.0
    
    for s_start, s_end in silences:
        if s_start > current_time:
            keeps.append((current_time, s_start))
        current_time = s_end
        
    if current_time < total_duration:
        keeps.append((current_time, total_duration))
        
    with open(edl_path, "w", encoding='utf-8') as f:
        f.write("TITLE: Cortes de Voz - Resolve\n")
        f.write("FCM: NON-DROP FRAME\n\n")
        
        timeline_current = 0.0
        # En DaVinci, los timelines por default empizan en 01:00:00:00 (3600 segundos)
        timeline_offset = 3600.0 
        
        for i, (k_start, k_end) in enumerate(keeps, 1):
            dur = k_end - k_start
            # Evitar microcortes absurdos
            if dur < 0.1:
                continue
                
            src_start_tc = frame_to_timecode(k_start, fps)
            src_end_tc = frame_to_timecode(k_end, fps)
            
            tl_start_tc = frame_to_timecode(timeline_offset + timeline_current, fps)
            tl_end_tc = frame_to_timecode(timeline_offset + timeline_current + dur, fps)
            
            # Evento en la línea de tiempo
            f.write(f"{i:03d}  AX       V     C        {src_start_tc} {src_end_tc} {tl_start_tc} {tl_end_tc}\n")
            
            # También exportar el audio 1
            f.write(f"{i:03d}  AX       A     C        {src_start_tc} {src_end_tc} {tl_start_tc} {tl_end_tc}\n\n")
            
            timeline_current += dur

if __name__ == "__main__":
    video_file = sys.argv[1]
    edl_file = video_file + "_CORTES_FINAL.edl"
    
    total_dur = get_video_duration(video_file)
    if total_dur == 0.0:
        print("No se pudo leer la duracion, usando estimacion de 12 horas.")
        total_dur = 43200.0
        
    silences = get_silences(video_file, threshold="-35dB", duration="0.8")
    
    if not silences:
        print("[!] No hay silencios")
    else:
        generate_edl_cuts(silences, edl_file, total_dur)
        print(f"EXITO: {edl_file} generado para cortar timeline")

