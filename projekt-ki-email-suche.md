# Projekt: KI-gestützte E-Mail- und Dokumentensuche (Pilotprojekt)

> **Projektziel:** Aufbau einer eigenen KI-Pipeline zum Durchsuchen von E-Mails und Anhängen nach Projektreferenzen (z. B. Projektnummer `12774`) – lokal oder in Azure betrieben, ohne Abhängigkeit von Drittanbieterdiensten.
>
> **Stand:** August 2026 | **Autor:** IT-Team

---

## Inhaltsverzeichnis

1. [Projektübersicht & Architektur](#1-projektübersicht--architektur)
2. [Step 1 – HuggingFace-Modelle auf Azure deployen](#2-step-1--huggingface-modelle-auf-azure-deployen)
3. [Step 2a – Kommerzielle Lizenzierung von HuggingFace-Modellen](#3-step-2a--kommerzielle-lizenzierung-von-huggingface-modellen)
4. [Step 2b – Geeignete Modelle für Text-Analyse & Ingesting](#4-step-2b--geeignete-modelle-für-text-analyse--ingesting)
5. [Step 3 – E-Mails & Anhänge lesen (Microsoft Graph API)](#5-step-3--e-mails--anhänge-lesen-microsoft-graph-api)
6. [Vorgeschlagene Git-Repository-Struktur](#6-vorgeschlagene-git-repository-struktur)
7. [Offene Fragen & nächste Schritte](#7-offene-fragen--nächste-schritte)

---

## 1. Projektübersicht & Architektur

### Ziel (Pilot)

Alle E-Mails von Kollegen, in denen Projekt **12774** vorkommt – im Betreff, im Inhalt oder in Anhängen (PDF, DOCX, XLSX usw.) – sollen automatisch gefunden, analysiert und gesammelt werden. Das Ergebnis soll in einer Datenbank gespeichert und durchsuchbar gemacht werden.

### Grobe Pipeline

```
[Outlook / Exchange]
        │
        ▼ (Microsoft Graph API)
[E-Mail-Abruf-Dienst]
  - Betreff, Absender, Datum
  - Body (HTML/Text)
  - Anhänge (PDF, DOCX, XLSX …)
        │
        ▼ (Text-Extraktion)
[Dokument-Parser]
  - PyMuPDF / pdfplumber (PDF)
  - python-docx (DOCX)
  - openpyxl (XLSX)
        │
        ▼ (KI-Analyse)
[Embedding-Modell (HuggingFace)]
  - Text in Vektoren umwandeln
  - Suche nach "12774" + Kontext
        │
        ▼
[Datenbank]
  - Relationale DB (PostgreSQL) für Metadaten
  - Vektordatenbank (pgvector / ChromaDB) für Embeddings
        │
        ▼
[Dashboard / API]
  - Suchergebnisse anzeigen
  - Export, Filter, Projektansicht
```

### Priorisierung (laut Oliver Trebbe)

| Phase | Inhalt | Status |
|-------|--------|--------|
| **Phase 1** | E-Mails & Anhänge lesen, KI kann Inhalte verarbeiten – Proof of Concept | 🔵 Jetzt |
| **Phase 2** | Datenbank-Integration, strukturiertes Speichern | 🔜 Danach |
| **Phase 3** | Modell-Training / Fine-Tuning (Inferenz) | ⏳ Später |

---

## 2. Step 1 – HuggingFace-Modelle auf Azure deployen

Es gibt drei Hauptwege, um HuggingFace-Modelle auf Azure zum Laufen zu bringen:

---

### Weg A: Ollama auf Azure Container Apps (empfohlen für Einstieg)

**Ollama** ist ein lokaler LLM-Runner, der viele HuggingFace-Modelle unterstützt und auch als Docker-Container auf Azure Container Apps deployt werden kann.

Microsoft stellt eine offizielle Anleitung bereit:
➡️ [Deploy models with Ollama on Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/deploy-openai-gpt-oss-ollama)

**Vorteile:**
- Einfachstes Setup, Docker-basiert
- Serverlose GPUs (bezahlen nur bei Nutzung)
- Auto-Scaling auf 0 bei Inaktivität → kostensparend
- Kompatibel mit vielen Modellen aus dem Ollama-Katalog

**GPU-Verfügbarkeit in Azure (Stand 2026):**

| GPU-Typ | Verfügbare Regionen |
|---------|-------------------|
| A100 (große Modelle) | West US, West US 3, Sweden Central, Australia East |
| T4 (kleinere Modelle) | West US 3, Sweden Central, Australia East, West Europe |

**Deployment-Schritte (Kurzfassung):**

```bash
# 1. Container App erstellen (Azure Portal oder CLI)
# Image: ollama/ollama:latest
# GPU aktivieren, Port 11434

# 2. Modell laden
ollama pull llama3.2          # Beispiel: 3B Modell für Textanalyse
ollama pull nomic-embed-text  # Embedding-Modell

# 3. REST-API ansprechen
curl -X POST "https://<deine-app>.azurecontainerapps.io/api/generate" \
  -H "Content-Type: application/json" \
  -d '{"model": "llama3.2", "prompt": "Enthält dieser Text die Projektnummer 12774?", "stream": false}'
```

**Laufzeitoptionen für CPU/GPU-Beschleunigung:**

| Option | Beschreibung | Anwendungsfall |
|--------|-------------|----------------|
| **OLLAMA.cpp** (llama.cpp) | CPU-optimierter Inferenz-Runner, kein GPU nötig | Kleiner Einstieg, günstig |
| **Vulkan** | GPU-Beschleunigung für AMD/Intel-GPUs | Wenn kein CUDA verfügbar |
| **TensorRT** | NVIDIA-Optimierung für maximale Performance | Produktion mit NVIDIA-GPU |
| **CUDA (Standard)** | Standard NVIDIA GPU-Beschleunigung in Ollama | Empfohlen für Azure A100/T4 |

> **Empfehlung für Azure:** CUDA/Standard-Ollama auf T4 oder A100. TensorRT lohnt sich erst bei sehr hohem Durchsatz.

---

### Weg B: Azure Machine Learning (AML) – direkt vom HuggingFace Hub

Für produktion-ready Deployments mit Auto-Scaling und Monitoring:

➡️ [Deploy HuggingFace Models to Azure ML](https://learn.microsoft.com/en-us/azure/machine-learning/how-to-deploy-models-from-huggingface)

```python
from azure.ai.ml import MLClient
from azure.ai.ml.entities import ManagedOnlineEndpoint, ManagedOnlineDeployment

ml_client = MLClient(credential=DefaultAzureCredential(), ...)

# Endpoint erstellen
ml_client.begin_create_or_update(
    ManagedOnlineEndpoint(name="email-analysis-ep")
).wait()

# Modell deployen (direkt aus HuggingFace Hub)
ml_client.online_deployments.begin_create_or_update(
    ManagedOnlineDeployment(
        name="embedding-deploy",
        endpoint_name="email-analysis-ep",
        model="azureml://registries/HuggingFace/models/BAAI/bge-m3/labels/latest",
        instance_type="Standard_NC6s_v3",  # GPU-Instanz
        instance_count=1,
    )
).wait()
```

**Voraussetzungen für AML-Deployment:**
- Modell muss Safetensors-Format haben
- Kein `trust_remote_code` erforderlich
- Tags: `Transformers`, `Diffusers` oder `Sentence-Transformers`

**Unterstützte Tasks:** `chat-completion`, `embeddings`, `text-classification`, `image-to-text`

---

### Weg C: Eigener Docker-Container auf Azure Container Apps

Für maximale Kontrolle (eigene HuggingFace-Modelle, Custom Code):

➡️ [GitHub: huggingface-deploy-azure](https://github.com/alfredodeza/huggingface-deploy-azure)

```dockerfile
FROM python:3.11-slim
RUN pip install transformers torch sentence-transformers
COPY inference.py .
CMD ["python", "inference.py"]
```

---

### Vergleich der Deployment-Wege

| Kriterium | Ollama (Container Apps) | Azure ML (AML) | Eigener Container |
|-----------|------------------------|----------------|-------------------|
| Einrichtungsaufwand | Gering | Mittel | Hoch |
| Kosten | Pay-per-use, skaliert auf 0 | Feste VM-Kosten | Pay-per-use |
| Modellauswahl | Ollama-Bibliothek | HuggingFace Hub (gefiltert) | Beliebig |
| Monitoring | Basis | Vollständig (AML Studio) | Selbst einrichten |
| **Empfehlung** | ✅ Einstieg/PoC | ✅ Produktion | ✅ Spezialfälle |

---

## 3. Step 2a – Kommerzielle Lizenzierung von HuggingFace-Modellen

> **Wichtig:** Nicht alle Modelle auf HuggingFace dürfen kommerziell genutzt werden. Die Lizenz ist immer auf der Modellkarte (Model Card) angegeben.

### Lizenztypen im Überblick

| Lizenz | Kommerzielle Nutzung | Bedingungen | Beispiel-Modelle |
|--------|---------------------|-------------|------------------|
| **Apache 2.0** | ✅ Ja | Namensnennung, Änderungen dokumentieren | BERT, DistilBERT, viele Embedding-Modelle, Qwen |
| **MIT** | ✅ Ja | Nur Copyright-Hinweis nötig | Viele kleinere Modelle |
| **OpenRAIL-M** | ✅ Ja (mit Nutzungsbeschränkungen) | Keine Nutzung für Schaden, Überwachung etc. | BLOOM, diverse Diffusion-Modelle |
| **CC BY 4.0** | ✅ Ja | Namensnennung | Einige Datensätze und Modelle |
| **Llama 3 License** | ⚠️ Bedingt | Kostenlos bis 700 Mio. MAU; darüber Lizenzpflicht | Meta Llama 3.x |
| **Llama 2 License** | ⚠️ Eingeschränkt | Kein direktes Anbieten als Basis für Wettbewerber | Meta Llama 2 |
| **CC BY-SA / CC BY-NC** | ❌ Nein (NC) / ⚠️ (SA) | NC = kein kommerzieller Einsatz | Einzelne Forschungsmodelle |
| **GPL v3** | ⚠️ Eingeschränkt | Abgeleiteter Code muss ebenfalls open-source sein | Seltene Fälle |
| **Proprietär / Custom** | ❌ Prüfen! | Je nach Anbieter sehr unterschiedlich | Einzelne Spezialmodelle |

### Empfohlene sichere Lizenzen für kommerzielle Nutzung

Für den Unternehmenseinsatz bei der BM Baulogistik / Bolle-Gruppe empfehlen sich ausschließlich Modelle mit:

- ✅ **Apache 2.0** (sicherste Wahl)
- ✅ **MIT**
- ✅ **OpenRAIL-M** (auf Nutzungsbeschränkungen achten)

### Wie prüfe ich die Lizenz?

1. HuggingFace-Seite des Modells öffnen (z. B. `huggingface.co/BAAI/bge-m3`)
2. Oben rechts auf die **Lizenz-Badge** klicken
3. Model Card nach `License:` durchsuchen
4. Im Zweifelsfall: Rechtsabteilung oder IT-Leitung einbeziehen

> **Quellen:** [HuggingFace Licensing Guide (BlueBash)](https://www.bluebash.co/blog/understanding-hugging-face-ai-model-licensing-commercial-use/) | [LLM License Types 2025 (Local AI Zone)](https://local-ai-zone.github.io/guides/ai-model-licensing-complete-legal-guide-2025.html) | [Best Open-Source LLMs 2026 (HuggingFace Blog)](https://huggingface.co/blog/daya-shankar/open-source-llms)

---

## 4. Step 2b – Geeignete Modelle für Text-Analyse & Ingesting

Für die E-Mail-Analyse werden zwei Modelltypen benötigt:

- **Embedding-Modelle** → Text in Vektoren umwandeln, semantische Suche ermöglichen
- **LLMs (optional)** → Zusammenfassung, Extraktion, Klassifikation

### Empfohlene Embedding-Modelle (Semantische Suche & RAG)

| Modell | Größe | Lizenz | Sprachen | Kontextlänge | Empfehlung |
|--------|-------|--------|----------|-------------|------------|
| **BAAI/bge-m3** | ~570 MB | MIT | 100+ inkl. Deutsch | 8.192 Token | ⭐ Top-Wahl für mehrsprachig |
| **Qwen3-Embedding-0.6B** | ~600 MB | Apache 2.0 | 100+ | 32.768 Token | ⭐ Sehr gut, lange Texte |
| **nomic-embed-text-v1.5** | ~275 MB | Apache 2.0 | Englisch (primär) | 8.192 Token | Schnell, gut für Englisch |
| **sentence-transformers/all-mpnet-base-v2** | ~420 MB | Apache 2.0 | Englisch | 514 Token | Bewährt, Standard |
| **multilingual-e5-large** | ~560 MB | MIT | 94 Sprachen | 512 Token | Gut für Deutsch |

> **Empfehlung für dieses Projekt:** `BAAI/bge-m3` oder `Qwen3-Embedding-0.6B` – beide unterstützen Deutsch, haben Apache 2.0 / MIT-Lizenz und verarbeiten lange E-Mail-Texte.

### Empfohlene LLMs für Extraktion & Klassifikation

Für die Aufgaben „Enthält diese E-Mail Infos zu Projekt 12774?" und „Was ist der Kern der Aussage?" eignen sich leichtgewichtige LLMs:

| Modell | Größe | Lizenz | Verwendung |
|--------|-------|--------|------------|
| **Llama 3.2 3B** | ~2 GB (4-bit) | Llama 3 License | Klassifikation, Extraktion |
| **Mistral 7B Instruct** | ~4 GB (4-bit) | Apache 2.0 | Zusammenfassung, Extraktion |
| **Phi-3.5 Mini** | ~2,2 GB | MIT | Leicht, schnell, gut für Klassifikation |
| **Qwen2.5 7B Instruct** | ~4 GB (4-bit) | Apache 2.0 | Sehr gutes Deutsch-Verständnis |

> **Empfehlung für Pilot:** Mit `Mistral 7B` oder `Qwen2.5 7B` über Ollama starten – Apache 2.0-Lizenz, gutes Deutsch, läuft auf T4-GPU in Azure.

### Verarbeitungs-Stack für Anhänge

```python
# Textextraktion aus verschiedenen Anhangformaten
import fitz          # PyMuPDF → PDF
from docx import Document  # python-docx → DOCX
import openpyxl     # XLSX
import pytesseract  # OCR für gescannte PDFs

# Embedding mit HuggingFace
from sentence_transformers import SentenceTransformer
model = SentenceTransformer("BAAI/bge-m3")
embedding = model.encode("Projektnummer 12774 Baustelle Telgte ...")
```

---

## 5. Step 3 – E-Mails & Anhänge lesen (Microsoft Graph API)

Da die E-Mails in Microsoft 365 / Exchange liegen, ist die **Microsoft Graph API** der richtige Weg – ohne lokale Outlook-Abhängigkeit.

### Voraussetzungen

Bei der Graph-API-Anbindung muss zwischen zwei grundsätzlich unterschiedlichen Berechtigungsarten unterschieden werden – das beeinflusst sowohl den Auth-Flow als auch, wessen Postfächer überhaupt gelesen werden können:

| Berechtigungsart | Login nötig? | Zugriff auf | Beispiel |
|-------------------|--------------|-------------|----------|
| **Application-Permission** (z. B. `Mail.Read` als App-Permission) | Nein – Service-Account mit Client-Secret, `acquire_token_for_client` | Alle Postfächer im Tenant (mit Admin-Consent) | Neue, eigene App-Registrierung für dieses Projekt |
| **Delegated-Permission** (z. B. `Mail.Read` als Delegated) | Ja – einmaliger interaktiver Login/Consent, danach Refresh-Token | Nur das Postfach des angemeldeten Nutzers (bzw. freigegebene Postfächer, wenn Berechtigung dazu besteht) | Bestehende App |

> ⚠️ **Wichtig – bitte vor Umsetzung klären:** Die uns aktuell zur Verfügung stehende Client-ID `7632d3f5-940f-4240-b513-495e442e709e` mit den Scopes `Chat.Read.All, Group.Read.All, GroupMember.Read.All, Mail.Read, OnlineMeetingTranscript, User.Read` ist eine **Delegated-Permission**-App (erkennbar an `User.Read`, das es nur delegiert gibt). Das heißt konkret:
> - Es ist **kein** Application-Permission-Setup ohne Login – ein interaktiver Login/Consent-Flow ist mindestens einmal nötig.
> - Mit diesen Rechten lässt sich zunächst nur das **eigene** Postfach durchsuchen, nicht automatisch die Postfächer aller Kollegen.
> - Die zusätzlichen Scopes (`Chat.Read.All`, `Group.Read.All`, `OnlineMeetingTranscript`) deuten darauf hin, dass diese App für einen anderen/breiteren Zweck (Teams-Chats, Meeting-Transkripte) angelegt wurde – nicht speziell für dieses Projekt.
> - Für das eigentliche Ziel „E-Mails aller Kollegen durchsuchen" wird eine **eigene App-Registrierung mit `Mail.Read` als Application-Permission** und Admin-Consent benötigt (siehe Codebeispiel unten), **oder** ein Setup über freigegebene Postfächer/„Mail.Read.Shared" mit expliziter Freigabe durch jeden Kollegen.
> - Das sollte mit dem M365-Administrator geklärt werden, bevor Kapitel 5 als Blaupause für die Umsetzung dient.

**Voraussetzungen für den Application-Permission-Weg (empfohlen für „alle Postfächer"):**

1. Azure App-Registrierung (App-ID, Client-Secret)
2. API-Berechtigung: `Mail.Read` als **Application-Permission** (nicht Delegated)
3. Admin-Zustimmung durch M365-Administrator

### Python-Beispiel: E-Mails + Anhänge abrufen

> Das folgende Beispiel gilt für den **Application-Permission-Weg** (Service-Account, kein Login). Für einen ersten PoC mit der bestehenden, delegierten Client-ID müsste stattdessen ein interaktiver Auth-Code- oder Device-Code-Flow verwendet werden, und es könnte zunächst nur das eigene Postfach ausgelesen werden.

```python
import requests
from msal import ConfidentialClientApplication

# Authentifizierung (Service Account, kein User-Login)
app = ConfidentialClientApplication(
    client_id="<APP_ID>",
    client_credential="<APP_SECRET>",
    authority="https://login.microsoftonline.com/<TENANT_ID>"
)
token = app.acquire_token_for_client(scopes=["https://graph.microsoft.com/.default"])
headers = {"Authorization": f"Bearer {token['access_token']}"}

# E-Mails durchsuchen (Volltext-Suche nach "12774")
search_url = "https://graph.microsoft.com/v1.0/users/<USER_ID>/messages"
params = {
    "$search": '"12774"',    # Suche in Betreff + Body
    "$top": 10,
    "$select": "id,subject,from,receivedDateTime,bodyPreview,hasAttachments"
}
response = requests.get(search_url, headers=headers, params=params)
messages = response.json()["value"]

# Für jede E-Mail: Anhänge abrufen
for msg in messages:
    if msg["hasAttachments"]:
        att_url = f"https://graph.microsoft.com/v1.0/users/<USER_ID>/messages/{msg['id']}/attachments"
        attachments = requests.get(att_url, headers=headers).json()["value"]
        for att in attachments:
            # att["contentBytes"] = Base64-kodierter Dateiinhalt
            # → dekodieren, Text extrahieren, mit KI analysieren
            pass
```

### Suchumfang

| Was wird durchsucht? | Graph API? | Anmerkung |
|---------------------|-----------|-----------|
| E-Mail-Betreff | ✅ Ja | `$search` erfasst Betreff |
| E-Mail-Body | ✅ Ja | `$search` erfasst Body |
| Anhänge (Textinhalt) | ❌ Nein | Muss lokal extrahiert werden |
| Anhänge (Dateiname) | ✅ Ja | Wird von `$search` erfasst |

> **Ergebnis:** Für die Volltextsuche in **Anhang-Inhalten** (z. B. PDF, DOCX) muss der Text lokal mit PyMuPDF / python-docx extrahiert und dann mit dem KI-Modell analysiert werden. Das ist machbar und ist Teil der geplanten Pipeline.

---

## 6. Vorgeschlagene Git-Repository-Struktur

```
projekt-ki-email-suche/
│
├── README.md                        ← Dieses Dokument (Kurzversion)
├── docs/
│   ├── architektur.md               ← Detaillierte Architektur-Dokumentation
│   ├── azure-deployment.md          ← Azure-Setup Schritt für Schritt
│   └── lizenzuebersicht.md          ← Lizenzprüfung der genutzten Modelle
│
├── src/
│   ├── ingestion/
│   │   ├── graph_client.py          ← Microsoft Graph API Client
│   │   ├── email_fetcher.py         ← E-Mail-Abruf & Filterung
│   │   └── attachment_parser.py     ← PDF/DOCX/XLSX Text-Extraktion
│   │
│   ├── analysis/
│   │   ├── embeddings.py            ← Embedding-Modell Wrapper
│   │   ├── classifier.py            ← Projektnummer-Erkennung
│   │   └── summarizer.py            ← LLM-gestützte Zusammenfassung
│   │
│   ├── database/
│   │   ├── schema.sql               ← DB-Schema (PostgreSQL)
│   │   └── db_client.py             ← Datenbankzugriff
│   │
│   └── api/
│       └── main.py                  ← FastAPI REST-Endpunkt (optional)
│
├── deployment/
│   ├── Dockerfile                   ← Container für Analyse-Service
│   ├── docker-compose.yml           ← Lokales Dev-Setup
│   └── azure/
│       ├── container-app.yaml       ← Azure Container App Definition
│       └── aml-deploy.py            ← Azure ML Deployment Script
│
├── notebooks/
│   └── poc_email_search.ipynb       ← Jupyter-Notebook für ersten PoC-Test
│
├── tests/
│   ├── test_parser.py
│   └── test_embeddings.py
│
├── .env.example                     ← Umgebungsvariablen (ohne Secrets)
├── requirements.txt
└── .gitignore                       ← Secrets, __pycache__, .env
```

### .gitignore (wichtig – keine Secrets einchecken!)

```gitignore
.env
*.secret
__pycache__/
*.pyc
.venv/
node_modules/
*.pth         # Modellgewichte nicht einchecken (zu groß)
```

---

## 7. Offene Fragen & nächste Schritte

### Sofort zu klären (vor Projektstart)

- [ ] **Azure-Abonnement:** Gibt es bereits ein Azure-Abonnement? Welche Ressourcengruppe nutzen wir?
- [ ] **GPU-Kontingent:** Serverlose GPUs (T4/A100) in Azure müssen ggf. beantragt werden
- [ ] **M365 App-Registrierung:** IT-Admin muss `Mail.Read`-Permission genehmigen
- [ ] **Welche Postfächer?** Welche User/Shared Mailboxes sollen durchsucht werden?
- [ ] **Datenschutz:** Klärung mit Datenschutzbeauftragtem – E-Mails von Kollegen werden verarbeitet
- [ ] **Mitbestimmung/Betriebsrat:** Die systematische, automatisierte Auswertung von Mitarbeiter-E-Mails ist nach § 87 BetrVG in der Regel mitbestimmungspflichtig – Betriebsrat vor Umsetzung einbeziehen, nicht erst nach dem PoC
- [ ] **Berechtigungsart klären:** Application- vs. Delegated-Permission für `Mail.Read` (siehe Hinweis in Kapitel 5) – wer erteilt den Admin-Consent für Zugriff auf alle Postfächer?
- [ ] **Git-Repository:** Wo wird das Repo angelegt? Azure DevOps, GitHub oder intern?

### Nächste technische Schritte

1. **PoC-Notebook erstellen** (`notebooks/poc_email_search.ipynb`)
   - Ollama lokal starten (Docker Desktop)
   - Ein Testmodell laden (`nomic-embed-text` oder `bge-m3`)
   - 5–10 Test-E-Mails manuell einlesen und analysieren
   - Prüfen: Erkennt das Modell „12774" im Text und in Anhängen?

2. **Graph API verbinden**
   - App-Registrierung in Azure AD
   - Token-Flow testen
   - Ersten E-Mail-Abruf durchführen

3. **Deployment auf Azure Container Apps**
   - Ollama-Container aufsetzen
   - Modell wählen (Empfehlung: `BAAI/bge-m3` für Embeddings)
   - REST-API testen

4. **Datenbank-Design** (Phase 2)
   - PostgreSQL + pgvector Extension
   - Schema für E-Mail-Metadaten + Embeddings

---

## Quellen & Referenzen

- [Azure Container Apps mit Ollama (Microsoft Learn)](https://learn.microsoft.com/en-us/azure/container-apps/deploy-openai-gpt-oss-ollama)
- [HuggingFace-Modelle auf Azure ML deployen](https://learn.microsoft.com/en-us/azure/machine-learning/how-to-deploy-models-from-huggingface)
- [GitHub: huggingface-deploy-azure](https://github.com/alfredodeza/huggingface-deploy-azure)
- [HuggingFace Lizenz-Guide (BlueBash)](https://www.bluebash.co/blog/understanding-hugging-face-ai-model-licensing-commercial-use/)
- [LLM-Lizenzübersicht 2025 (Local AI Zone)](https://local-ai-zone.github.io/guides/ai-model-licensing-complete-legal-guide-2025.html)
- [Best Open-Source LLMs 2026 (HuggingFace Blog)](https://huggingface.co/blog/daya-shankar/open-source-llms)
- [Beste Embedding-Modelle 2026 (BentoML)](https://www.bentoml.com/blog/a-guide-to-open-source-embedding-models)
- [Microsoft Graph API – E-Mail-Anhänge lesen](https://learn.microsoft.com/en-us/answers/questions/2103970/how-to-read-email-attachments-using-ms-graph-api)
- [GitHub: Office 365 PDF Attachment Extract via Graph API](https://github.com/oaksakal/Office-365-Email-PDF-Attachment-Extract-Script)

---

*Dokument erstellt: August 2026 | Nächste Überprüfung: nach PoC-Phase*
