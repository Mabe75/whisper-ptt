# whisper-ptt — Voice Push-to-Talk für Windows

Taste halten, sprechen, loslassen — der erkannte Text landet im aktiven Fenster.
Eine kleine Tray-Anwendung für Windows, die die Aufnahme an einen Whisper-Dienst
schickt und das Ergebnis dort einfügt, wo gerade der Cursor steht.

> **Das hier ist nur der Client.** Die Spracherkennung läuft auf einem
> Whisper-Dienst, den du selbst betreibst — lokal auf demselben Rechner oder
> irgendwo im Netz. Ohne diesen Dienst nimmt das Programm zwar auf, bekommt aber
> keinen Text zurück.

## Was du brauchst

* Windows 10 oder 11 (64 Bit) und ein Mikrofon
* einen erreichbaren **Whisper-Server**, wahlweise
  * ein eigenes **Gateway** (`endpoint_type: gateway`), das die Transkription um
    eine **LLM-Nachkorrektur** ergänzt — Groß-/Kleinschreibung, Satzzeichen,
    Füllwörter. Das ist die angenehmste Variante, erfordert aber zusätzlich zum
    Whisper-Modell ein laufendes Sprachmodell und einen kleinen Dienst, der
    `POST /api/transcribe` anbietet und `{"text": "..."}` zurückgibt.
  * oder jeden **OpenAI-kompatiblen** Endpunkt (`endpoint_type: openai`,
    `POST /v1/audio/transcriptions`) — etwa faster-whisper-server / Speaches,
    whisper.cpp im Server-Modus oder LocalAI. Reine Transkription, keine
    Korrektur, dafür in ein paar Minuten aufgesetzt.

Beides darf auf demselben Rechner laufen wie der Client.

## Eigenschaften

* **Keine Abhängigkeiten auf dem Client-Rechner** — weder Python noch .NET. Die
  Laufzeit steckt im Installer, bei der Installation wird nichts nachgeladen.
* **Installation ohne Adminrechte**, pro Benutzer, mit sauberer Deinstallation.
* **Einfügen wahlweise** per Strg+V oder zeichenweisem Tippen, Zwischenablage
  wird danach wiederhergestellt.

## Installation

Die aktuelle `VoicePTT-Setup.exe` unter [Releases](../../releases) herunterladen
und starten. Der Assistent fragt nach Autostart und Desktop-Verknüpfung.

Danach im Tray-Menü unter *Einstellungen…* die Adresse des Whisper-Dienstes
eintragen und mit *Verbindung testen* prüfen — voreingestellt ist
`http://127.0.0.1:8055`, also ein Dienst auf demselben Rechner.

> Windows SmartScreen meldet sich beim ersten Start, weil der Installer nicht
> signiert ist: *Weitere Informationen* → *Trotzdem ausführen*.

## Bedienung

**Taste halten, sprechen, loslassen.** Welche Taste, entscheidet, was mit dem Diktat
geschieht — jedes Profil hat seinen eigenen Startton, sodass man den Modus hört:

| Taste | Profil | Verhalten |
|---|---|---|
| F9 | Diktat | Satzzeichen, Groß-/Kleinschreibung, Füllwörter raus; Wortlaut bleibt |
| F8 | Formulieren | freie Umformulierung zu sauberer Prosa, Briefton |
| F7 | Roh | unveränderte Transkription — für Passwörter, Code, exakte Zitate |
| F6 | Assistent | das Gesprochene gilt als Auftrag, eingefügt wird das Ergebnis |

Tasten, Namen, Anweisungen und Töne sind frei konfigurierbar; weitere Profile lassen
sich anlegen. Über das Tray-Symbol: Mikrofon, Ton-Lautstärke, Autostart,
Einstellungsdialog, Protokoll.

## Spezialwörter

Whisper hört „Claude" gern als „Cloud" und „Qwen" als „kwen". Dagegen gibt es eine
Vokabeltabelle, deren Einträge an drei Stellen wirken:

| Spalte | Wirkung |
|---|---|
| **Begriff** | geht als Hinweis an das LLM; zusätzlich als `prompt` an Whisper, aber nur bei `endpoint_type: openai` |
| **Varianten** | werden im fertigen Text hart ersetzt, ohne Rücksicht auf den Zusammenhang |
| **Verwechslungen** | nennen dem LLM ein Wort als wahrscheinliche Fehlerkennung — entschieden wird am Satz |

Die dritte Spalte löst den Fall, den eine Ersetzungsliste nicht lösen kann: „ich frag mal
cloud" wird zu Claude, „die Daten in die cloud geschoben" bleibt die Cloud. Genannt werden
dem Modell nur Verwechslungen, die im aktuellen Text wirklich vorkommen — jedes überflüssige
Paar kostet messbar Trefferquote.

Die ausführliche Anleitung liegt in [`installer/ANLEITUNG.txt`](installer/ANLEITUNG.txt)
und wird mitinstalliert.

## Konfiguration

Alles lässt sich im Einstellungsdialog ändern; die Datei liegt unter
`%APPDATA%\VoicePTT\config.json`, das Protokoll unter
`%LOCALAPPDATA%\VoicePTT\client.log`.

| Feld | Bedeutung |
|---|---|
| `gateway_url` | Adresse des Whisper-Dienstes |
| `endpoint_type` | `gateway` (eigener Dienst inkl. LLM-Korrektur) oder `openai` |
| `model` | nur bei `openai`: erwarteter Modellname |
| `language` | z. B. `de`; leer = automatische Erkennung |
| `hotkey` | Taste des alten Clients; ab 2.1 nur noch Spiegel des ersten Profils, damit eine parallel installierte Altfassung dieselbe Taste benutzt |
| `input_device` | `null` = Systemstandard, sonst Teil des Gerätenamens |
| `correct` | Korrektur im Gateway; ab 2.1 entscheidet das Profil über `correction`, das Feld bleibt für eine parallele Altfassung erhalten |
| `insert_mode` | `paste` (Strg+V) oder `type` (zeichenweise tippen) |
| `restore_clipboard` | Zwischenablage nach dem Einfügen wiederherstellen |
| `samplerate` | Aufnahmefrequenz, Standard `16000` |
| `beep`, `beep_volume` | Signaltöne, Lautstärke `0.0`–`1.0` |
| `suppress_hotkey` | `true` = die Taste erreicht das aktive Fenster nicht mehr |
| `min_seconds` | kürzere Aufnahmen werden verworfen |
| `llm_url`, `llm_model` | OpenAI-kompatibler Chat-Endpunkt für die Nachbearbeitung |
| `llm_temperature`, `llm_timeout_seconds` | Vorgabe `0` und `120` |
| `llm_extra` | wird unverändert in den Request gemischt; ein Feld mit `null` entfernt einen vom Client gesetzten Schlüssel |
| `profiles` | Taste, Name, `correction` (`llm`/`gateway`/`none`), Anweisung, `thinking`, `replacements`, `beep_hz` |
| `vocabulary` | `term`, `variants`, `confusions`, `active` |
| `send_vocabulary_to_whisper` | Begriffe als `prompt` an Whisper (nur `endpoint_type: openai`) |
| `log_prompts` | schreibt den fertigen Systemprompt ins Protokoll |

### Nachdenkende Modelle

Qwen3 und Verwandte denken vor jeder Antwort sichtbar nach — eine Korrektur dauert damit
statt 0,7 Sekunden rund 30. VoicePTT schaltet das pro Anfrage über
`chat_template_kwargs.enable_thinking` ab; der Schalter *Denken* im Profil stellt es wieder
an. Das dokumentierte `/no_think` im Prompt wirkt bei llama.cpp **nicht**. Für abweichende
Server lässt sich der Anfragekörper über `llm_extra` ergänzen oder bereinigen.

Beispiel für ein lokales Whisper auf demselben Rechner:

```json
{
  "gateway_url": "http://127.0.0.1:8000",
  "endpoint_type": "openai",
  "model": "Systran/faster-whisper-small",
  "language": "de"
}
```

## Selbst bauen

Auf dem Entwicklungsrechner nötig:

* [.NET SDK 8](https://dotnet.microsoft.com/download) oder neuer
* [Inno Setup 6](https://jrsoftware.org/isinfo.php) — `winget install -e --id JRSoftware.InnoSetup`

```powershell
.\build.ps1
```

Das Ergebnis liegt danach in `dist\VoicePTT-Setup.exe`. Nur neu packen, ohne zu
kompilieren: `.\build.ps1 -SkipPublish`.

## Aufbau

```
src/VoicePTT/            C#-Quellcode
installer/VoicePTT.iss   Inno-Setup-Skript
installer/ANLEITUNG.txt  Benutzerdoku, wird mitinstalliert
build.ps1                Build: dotnet publish + Inno Setup
tools/make_icon.py       erzeugt das Programmsymbol (einmalig, Build-Zeit)
legacy-python-client/    die frühere Python-Fassung, nur als Referenz
VoicePTT-Setup.bat       der frühere BAT-Installer, nur als Referenz
```

| Datei | Aufgabe |
|---|---|
| `Program.cs` | Einstieg, Einzelinstanz-Mutex, `--quit` für Setup/Deinstallation |
| `TrayApp.cs` | Tray-Symbol, Menü, Ablauf Aufnahme → Transkription → Einfügen |
| `HotkeyHook.cs` | globaler Low-Level-Tastatur-Hook, optional abfangend |
| `Recorder.cs` | Mikrofonaufnahme (NAudio/WaveIn), WAV-Erzeugung |
| `Transcriber.cs` | Upload an Gateway bzw. OpenAI-kompatiblen Server |
| `Profile.cs` | Profile und Vokabeleinträge samt Vorgaben einer frischen Installation |
| `LlmClient.cs` | Nachbearbeitung über einen OpenAI-kompatiblen Chat-Endpunkt |
| `Vocabulary.cs` | Whisper-Vorgabe, Hinweisblock für das LLM, harte Ersetzungen |
| `TextInserter.cs` | Einfügen per Strg+V oder zeichenweisem Tippen |
| `SettingsForm.cs` | Einstellungsdialog |
| `AppConfig.cs` | `config.json`, inkl. Übernahme alter Konfigurationen |
| `Native.cs` | P/Invoke: SendInput, Hook-API |

## Umstieg von der Python-Fassung

Der Installer findet die alte `config.json` (über den Pfad im früheren
Autostart-Skript oder an den üblichen Ablageorten) und übernimmt sie. Auf Wunsch
entfernt er Autostart-Eintrag und Registrierung der alten Version; ein eigener
Programmordner bleibt unangetastet.

Beide Fassungen benutzen denselben Einzelinstanz-Mutex und laufen daher nie
gleichzeitig. Läuft die alte Version noch, weist die neue mit einem
Hinweisfenster darauf hin.

## English summary

Hold a key, speak, release — the transcribed text is inserted into the active
window. This is the **client only**: it needs a self-hosted Whisper service,
either a custom gateway that adds LLM post-correction (`POST /api/transcribe`,
returning `{"text": "..."}`), or any OpenAI-compatible
`POST /v1/audio/transcriptions` endpoint such as faster-whisper-server /
Speaches, whisper.cpp in server mode or LocalAI. Either may run on the same
machine. The client ships as a fully offline installer: no Python, no .NET
runtime, no admin rights required. UI and documentation are in German.

## Lizenz

[MIT](LICENSE)
