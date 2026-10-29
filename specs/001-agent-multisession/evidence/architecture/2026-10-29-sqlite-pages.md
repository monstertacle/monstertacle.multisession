# Native SQLitekeysets für Sitzungen und angenommene Anfragen

Gemessen2026-10-29T11:04:11Z, Ausgang4113798. T050/T063, bestehender T115-Vertrag.
QueryTransactions.cs SHA256eb22a0e06d30c0074b6e296d0961394902106ec9bed3f83121a6666f3045bc43;
SqliteQueryStore.cs dff6c7038342bac437ee6c836cf2b62c02b92d8b618aca6b86052f9879373e3e;
PageCursor.cs b3451708969f3cbd822edd3a2574633b164cce2142bd79a8b9d9b62d34c0c8f1.

## Aktueller Bestand und Verträge

Session-/Requestlookup und Acceptance laufen in genau einem DBworker. Native paginierte
Ports sind noch NotImplemented, Common Sessions-/RequestQueries validieren Seitengröße
und delegieren an den autoritativen Store. SqliteStoreState liest persistente StoreId;
Schema SessionOrder(CreatedUtc,SessionId) und Unique(SessionId,Sequence) stehen bereit.
Snapshots werden typisiert als JSON gelesen, scalarZeitpunkte invariantUTC gespeichert.
PageCursor übernimmt bereits strenge Größe1..100, Payloadversion, Store-/Listen-/Session-
Scope, vollständigen Lastkey und Guidordinalordnung; nicht nachbauen. ControlledPageProjection
ist ausschließlich kleines Double, kein Ersatz durch LINQ-Vollmaterialisierung in SQLite.

## Entscheidung vor Implementierung

Nur die beiden jetzt echten nichtleeren Listen umsetzen; History/Recovery folgen ihren
Schreib-/Redvertikalen und bleiben sichtbar offen. Eigene Queryverantwortung SqlitePageQueries
plus kleiner Reader für maximal pageSize+1. Sitzungen per tuplekey(CreatedUtc,SessionId),
Requests per SessionId/Sequence/RequestId, jeweils parametrisiertes WHERE/ORDER BY/LIMIT.
Erste Seite und Fortsetzung nutzen expliziten SQLkeyset, kein OFFSET. Rawscalarwerte liefern
Cursor, JSON liefert DTOwerte. Keine aktuelle Profil-/Providerkonfiguration als Datenquelle.
NextCursor nur bei tatsächlicher Extrazeile; nach Reopen derselbe Metadatenscope.
Neue Einfügung vor Lastkey wird nicht rückwirkend geliefert, keine Snapshotisolation
für gesamte mehrseitige Liste behaupten. Anfragecursor anderer Sitzung/Liste/Store wird
vor Verwendung als SQLkey abgewiesen. Nicht vorhandene nonempty SessionId liefert wie
bestehender kontrollierter Vertrag leere Requestliste, kein fremder Datensatz.

## Testplan vor Green

Reale öffentliche Factory/Store/Commonfacaden: feste CreatedUtc-Gleichstände und ordinal
UUIDs, kleine und maximale Seitengrößen, Einfügen vor/nach Lastkey, Reopen mit demselben
Cursor; Requests in gespeicherter Sequence samt ursprünglicher Kandidaten-ID auch bei
verworfenem Receipt; Cursor fremder Sitzung/Liste/Store, ungültige Größe/Encoding sowie
fremde nonempty SessionId. Seed nur über echte Session-/Binding-/Acceptanceports, keine
produktive DB oder Provider-HTTPoperation. Originale Anfangsfehler müssen Stubverhalten
fachlich zeigen, danach minimale SQLqueries, volle Regression/Guard/Qualität. T063 bleibt
für History-/Recoverylisten und Kultur-/Crashgesamtumfang offen. Owner ausführende Sitzung.
