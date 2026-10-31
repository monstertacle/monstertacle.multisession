# T105: reale Providerabnahme

Stand 2026-10-29T22:49:21Z: **NOT RUN**, Q-04 offen.
Es wurden keine Testkonten/Credentials bereitgestellt und keine ausdrückliche
Kosten-/Speicherfreigabe für reale OpenAI-/Gemini-Aufrufe erteilt.
Die autonome Implementierungsanweisung ersetzt diese in T105 ausdrücklich verlangte
Freigabe nicht. Keine Schlüssel gesucht, keine kostenpflichtigen Requests gestartet.

Vorbereitete technische Abdeckung: kontrollierte native HTTP-Verträge, neue Sitzung,
erste/Folgeanfrage, Auth/Expiry, Status/Cancel, kein POSTretry, wiederholter Modellread
und Reopen vor Queuefreigabe. Das ist kein Livebeleg.
Die CLI-Szenarien live-openai/live-gemini liefern deshalb weiterhin NOT RUN.

Owner Projektverantwortlicher: Testkonten, erlaubte Endpoints/Modelle, Kostenobergrenze,
Datenarten/Retention und alle drei Zielhosts bereitstellen/freigeben.
Danach führt die Implementierungssitzung die begrenzte Matrix aus:
je Hersteller/Host neue Sitzung, erste/Folgeanfrage, kontrollierter Neustart,
Fortsetzung, native Status-/Cancelprüfung; nur IDs/Typcodes/Versionen/Zeiten
protokollieren, keine Credentials oder Inhalte. Abnahme verlangt tatsächliche
native End-/Fortsetzungsbelege und denselben Lieferstand.
Abhängigkeiten und offene Gemini-Kontextklassifikation: remaining-acceptance.md.

