# Änderungsverlauf

Das Format folgt lose [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionsnummern [SemVer](https://semver.org/lang/de/).

## [2.1.1] – 2026-11-09

### Behoben

* Lieferte das Sprachmodell seine Antwort in deutschen Anführungszeichen, blieben diese
  im eingefügten Text stehen – die Prüfung griff nur bei geraden Zeichen.
* Eine frische Installation brachte eine vorbelegte LLM-Adresse mit, die auf den eigenen
  Rechner zeigte. Das Feld bleibt jetzt leer, bis eine Adresse eingetragen wird.

### Dokumentation

* Anleitung auf 2.1 datiert und um `llm_temperature`, `llm_timeout_seconds` sowie
  `send_vocabulary_to_whisper` ergänzt.
* Klargestellt, dass Begriffe nur bei `endpoint_type: openai` an Whisper gehen; am eigenen
  Gateway wirken sie über den LLM-Prompt und die Ersetzungen.
* `hotkey` und `correct` sind als Altfelder gekennzeichnet, die nur noch für eine parallel
  installierte Vorgängerfassung mitgeschrieben werden.

## [2.1.0] – 2026-11-09

### Neu

* **Profile statt einer festen Taste.** Jede Push-to-Talk-Taste hat jetzt ihren eigenen
  Verarbeitungsweg, eigenen Startton und eine eigene Anweisung an das Sprachmodell.
  Vorgabe sind vier Profile:

  | Taste | Profil | Verhalten |
  |---|---|---|
  | F9 | Diktat | Satzzeichen, Groß-/Kleinschreibung, Füllwörter raus; Wortlaut bleibt |
  | F8 | Formulieren | freie Umformulierung zu sauberer Prosa |
  | F7 | Roh | unveränderte Transkription, keine Nachbearbeitung |
  | F6 | Assistent | das Gesprochene gilt als Auftrag, eingefügt wird das Ergebnis |

* **Nachbearbeitung im Client.** VoicePTT ruft nach der Transkription selbst einen
  OpenAI-kompatiblen Chat-Endpunkt auf (`llm_url`, `llm_model`). Die Prompts liegen damit
  beim Nutzer und lassen sich im Einstellungsdialog ändern, ohne dass der Dienst angefasst
  werden muss.
* **Vokabeltabelle** für Begriffe, die Whisper regelmäßig verhört. Ein Eintrag wirkt an drei
  Stellen: als Vorgabe für Whisper, als Hinweis im LLM-Prompt und – nur bei hinterlegten
  Varianten – als harte Ersetzung im fertigen Text.
* **Einstellungsdialog mit Registerkarten** (Verbindung, Profile, Vokabular, Allgemein),
  inklusive Erreichbarkeitstest für Gateway und LLM und Abruf der Modellliste.

### Geändert

* Das Tray-Menü zeigt die belegten Tasten mit ihrem Profil. Die Einträge für feste Taste und
  LLM-Korrektur entfallen – beides steckt jetzt im Profil.
* Profile mit Korrektur `llm` schicken dem Gateway `correct=false` und holen den Rohtext.

### Beibehalten

* **Die Gateway-Schnittstelle ist unverändert.** Eine parallel betriebene Installation der
  Version 1.x oder 2.0 läuft unberührt weiter.
* Ohne eingetragene LLM-Adresse verhält sich das Programm wie bisher: Die Korrektur macht
  dann wieder das Gateway.
* Antwortet das Sprachmodell nicht oder zu langsam, wird der unveränderte Rohtext eingefügt –
  ein Diktat geht nie verloren.

### Hinweis zu Qwen3 und verwandten Modellen

Diese Modelle denken vor jeder Antwort sichtbar nach, was eine Korrektur von unter einer
Sekunde auf gut 30 Sekunden streckt. VoicePTT schaltet das deshalb pro Anfrage über
`chat_template_kwargs.enable_thinking` ab; der Schalter *Denken* im Profil stellt es
bei Bedarf wieder an. Das von Qwen dokumentierte `/no_think` im Prompt wirkt bei
llama.cpp nicht. Für abweichende Server lässt sich der Anfragekörper über `llm_extra`
ergänzen oder bereinigen.

## [2.0.0] – 2026-09-10

### Neu

* Vollständige Neufassung des Clients in C# / .NET 8 (WinForms) als Tray-Anwendung.
* Offline-Installer auf Basis von Inno Setup: Der Zielrechner braucht weder Python noch
  .NET, die Installation läuft pro Benutzer ohne Adminrechte.
* Einstellungsdialog, Mikrofon- und Tastenwahl im Tray, Autostart-Schalter im Programm.
* `suppress_hotkey`: Die Push-to-Talk-Taste erreicht das aktive Fenster standardmäßig nicht
  mehr.

### Geändert

* Konfiguration liegt in `%APPDATA%\VoicePTT\config.json`, das Protokoll in
  `%LOCALAPPDATA%\VoicePTT\client.log`.
* Der Installer übernimmt die Konfiguration der Python-Fassung und räumt deren Reste auf
  Wunsch auf.
