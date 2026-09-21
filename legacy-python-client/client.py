"""
Voice Push-to-Talk Client fuer Windows.
Taste halten (Standard: F9), sprechen, loslassen -> Text wird ins aktive Fenster eingefuegt.
Spricht mit dem Voice-Gateway auf Spark 2 (Whisper + LLM-Korrektur).
"""
import os, io, json, time, wave, threading, webbrowser, sys, datetime
import requests
_LOGF = os.path.join(os.path.dirname(os.path.abspath(__file__)), "client.log")
try:
    sys.stdout = sys.stderr = open(_LOGF, "a", buffering=1, encoding="utf-8")
except Exception:
    pass
def _log(*a):
    try:
        print(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S"), *a, flush=True)
    except Exception:
        pass

HERE = os.path.dirname(os.path.abspath(__file__))
CFG_PATH = os.path.join(HERE, "config.json")
DEFAULTS = {
    "gateway_url": "http://127.0.0.1:8055",
    "hotkey": "f9",
    "correct": True,
    "insert_mode": "paste",       # "paste" (Strg+V) oder "type" (tippen)
    "restore_clipboard": True,
    "samplerate": 16000,
    "beep": True,
    "endpoint_type": "gateway",   # "gateway" (unser Dienst + LLM-Korrektur) oder "openai" (lokaler Whisper, OpenAI-kompatibel)
    "model": "whisper-1",         # nur fuer endpoint_type=openai: Modellname des lokalen Servers
    "language": "",               # optional, z.B. "de"; leer = automatische Erkennung
}


def load_cfg():
    c = dict(DEFAULTS)
    if os.path.exists(CFG_PATH):
        try:
            c.update(json.load(open(CFG_PATH, encoding="utf-8")))
        except Exception as e:
            print("Config-Fehler:", e)
    return c


cfg = load_cfg()

import numpy as np
import sounddevice as sd
import keyboard
import pyperclip

def beep(f=880, d=120):
    # Leiser, regelbarer Ton ueber sounddevice (winsound.Beep kennt keine Lautstaerke).
    if not cfg.get("beep"):
        return
    vol = float(cfg.get("beep_volume", 0.2))
    if vol <= 0:
        return
    try:
        sr = 44100
        n = int(sr * d / 1000)
        t = np.arange(n) / sr
        w = (np.sin(2 * np.pi * f * t) * min(vol, 1.0) * 0.3).astype(np.float32)
        k = min(300, n // 2)
        if k > 0:
            env = np.ones(n, dtype=np.float32)
            env[:k] = np.linspace(0, 1, k)
            env[-k:] = np.linspace(1, 0, k)
            w *= env
        sd.play(w, sr)
    except Exception:
        pass

recording = False
frames = []
stream = None
lock = threading.Lock()


def resolve_device(val):
    # None/"" -> System-Standard; Zahl -> Geraete-Index; Text -> Namensteil (erstes Input-Geraet, das passt)
    if val is None or val == "":
        return None
    try:
        return int(val)
    except (ValueError, TypeError):
        pass
    name = str(val).lower()
    try:
        for i, d in enumerate(sd.query_devices()):
            if d.get("max_input_channels", 0) > 0 and name in d["name"].lower():
                return i
    except Exception:
        pass
    return None


def start_rec():
    global recording, frames, stream
    with lock:
        if recording:
            return
        frames = []
        dev = resolve_device(cfg.get("input_device"))
        _t0 = time.time()
        _log("start_rec dev=", dev)
        for d in ([dev, None] if dev is not None else [None]):
            try:
                stream = sd.InputStream(samplerate=cfg["samplerate"], channels=1, dtype="int16",
                                        device=d,
                                        callback=lambda indata, n, t, s: frames.append(indata.copy()))
                stream.start()
                recording = True
                beep(660, 55)
                _log("Aufnahme laeuft, open %.2fs" % (time.time() - _t0))
                if d != dev:
                    print("Hinweis: konfiguriertes Mikrofon nicht verfuegbar, nutze Standard.")
                return
            except Exception as e:
                print("Aufnahme-Fehler (Geraet", d, "):", e)
        beep(300, 200)


def stop_and_send():
    global recording, stream
    with lock:
        if not recording:
            return
        recording = False
        try:
            stream.stop()
            stream.close()
        except Exception:
            pass
    beep(520, 55)
    threading.Thread(target=process, daemon=True).start()


def transcribe_via_endpoint(buf):
    base = cfg["gateway_url"].rstrip("/")
    files = {"file": ("ptt.wav", buf, "audio/wav")}
    if cfg.get("endpoint_type", "gateway") == "openai":
        if base.endswith("/v1"):
            base = base[:-3]
        data = {"model": cfg.get("model", "whisper-1"), "response_format": "json"}
        if cfg.get("language"):
            data["language"] = cfg["language"]
        r = requests.post(base + "/v1/audio/transcriptions", files=files, data=data, timeout=120)
    else:
        r = requests.post(base + "/api/transcribe", files=files,
                          data={"correct": "true" if cfg.get("correct", True) else "false"}, timeout=120)
    r.raise_for_status()
    return (r.json().get("text") or "").strip()


def process():
    try:
        if not frames:
            _log("process: keine Audio-Frames")
            return
        audio = np.concatenate(frames, axis=0)
        _dur = len(audio) / cfg["samplerate"]
        _log("process: frames=%d dauer=%.2fs" % (len(frames), _dur))
        if _dur < 0.3:
            _log("process: zu kurz, verworfen")
            beep(300, 150)
            return
        buf = io.BytesIO()
        with wave.open(buf, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(cfg["samplerate"])
            w.writeframes(audio.tobytes())
        buf.seek(0)
        text = transcribe_via_endpoint(buf)
        if not text:
            beep(300, 150)
            return
        _log("F9 -> eingefuegt:", repr(text[:80]))
        insert_text(text)
        beep(780, 55)
    except Exception as e:
        _log("F9 Fehler:", e)
        beep(300, 250)


def insert_text(text):
    if cfg.get("insert_mode") == "type":
        keyboard.write(text, delay=0)
        return
    old = None
    try:
        old = pyperclip.paste()
    except Exception:
        pass
    pyperclip.copy(text)
    time.sleep(0.06)
    keyboard.send("ctrl+v")
    if cfg.get("restore_clipboard") and old is not None:
        def restore():
            time.sleep(0.4)
            try:
                pyperclip.copy(old)
            except Exception:
                pass
        threading.Thread(target=restore, daemon=True).start()


def on_key(e):
    _log("F9-Event:", e.event_type)
    if e.event_type == "down":
        start_rec()
    elif e.event_type == "up":
        stop_and_send()


def save_cfg():
    try:
        json.dump(cfg, open(CFG_PATH, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    except Exception as e:
        print("Config speichern fehlgeschlagen:", e)


def input_device_names():
    names = []
    try:
        for d in sd.query_devices():
            if d.get("max_input_channels", 0) > 0 and d["name"] not in names:
                names.append(d["name"])
    except Exception:
        pass
    return names


def run_tray():
    import pystray
    from PIL import Image, ImageDraw
    img = Image.new("RGB", (64, 64), (16, 22, 38))
    d = ImageDraw.Draw(img)
    d.ellipse((22, 10, 42, 34), fill=(80, 140, 255))
    d.rectangle((29, 34, 35, 48), fill=(80, 140, 255))
    d.rectangle((24, 48, 40, 52), fill=(80, 140, 255))

    def toggle(icon, item):
        cfg["correct"] = not cfg["correct"]
        save_cfg()

    def set_dev(name):
        def _(icon, item):
            cfg["input_device"] = name
            save_cfg()
        return _

    def set_vol(v):
        def _(icon, item):
            cfg["beep_volume"] = v
            save_cfg()
        return _

    def open_web(icon, item):
        try:
            webbrowser.open(cfg.get("gateway_url", ""))
        except Exception:
            pass

    def quit_(icon, item):
        icon.stop()
        os._exit(0)

    mic_items = [pystray.MenuItem("Standard (automatisch)", set_dev(None),
                                  checked=lambda item: not cfg.get("input_device"), radio=True)]
    for n in input_device_names():
        mic_items.append(pystray.MenuItem(n, set_dev(n),
                                          checked=lambda item, nm=n: cfg.get("input_device") == nm, radio=True))
    mic_menu = pystray.Menu(*mic_items)

    vol_levels = [("Aus", 0.0), ("Sehr leise", 0.03), ("Leise", 0.08), ("Mittel", 0.2), ("Lauter", 0.5), ("Voll", 1.0)]
    vol_menu = pystray.Menu(*[
        pystray.MenuItem(lbl, set_vol(v),
                         checked=lambda item, vv=v: abs(float(cfg.get("beep_volume", 0.2)) - vv) < 0.001,
                         radio=True)
        for lbl, v in vol_levels])

    menu = pystray.Menu(
        pystray.MenuItem("Weboberflaeche oeffnen", open_web, default=True),
        pystray.MenuItem("Mikrofon", mic_menu),
        pystray.MenuItem("Ton-Lautstaerke", vol_menu),
        pystray.MenuItem(lambda i: ("Korrektur: an" if cfg["correct"] else "Korrektur: aus"), toggle),
        pystray.MenuItem("Beenden", quit_),
    )
    pystray.Icon("voice-ptt", img, "Voice Push-to-Talk", menu).run()


_mutex_handle = None


def _already_running():
    # Windows: benannter Mutex -> eine zweite Instanz erkennt die erste und beendet sich.
    try:
        import ctypes
        k = ctypes.windll.kernel32
        global _mutex_handle
        _mutex_handle = k.CreateMutexW(None, False, "VoicePTT_SingleInstance_Mutex")
        return k.GetLastError() == 183  # ERROR_ALREADY_EXISTS
    except Exception:
        return False


def main():
    if _already_running():
        print("Voice Push-to-Talk laeuft bereits - zweite Instanz beendet sich.")
        return
    print("Voice Push-to-Talk aktiv.")
    print(f"Taste '{cfg['hotkey']}' halten zum Sprechen.")
    print(f"Gateway: {cfg['gateway_url']} | Korrektur: {cfg['correct']} | Einfuegen: {cfg['insert_mode']}")
    keyboard.hook_key(cfg["hotkey"], on_key)
    _log("Hook fuer Taste", cfg["hotkey"], "registriert")
    try:
        run_tray()
    except Exception as e:
        print("Kein Tray-Icon (", e, ") - laufe im Hintergrund. Strg+C zum Beenden.")
        keyboard.wait()


if __name__ == "__main__":
    try:
        _log("=== client start pid", os.getpid(), "===")
        main()
        _log("client main() beendet")
    except Exception:
        import traceback
        _log("FATAL:", traceback.format_exc())
