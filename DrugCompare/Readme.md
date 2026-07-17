````markdown
# MedCompare

**MedCompare** is a local desktop application for checking drug-related information, active substances, known substance interactions, Polish drug registry data, and ICD codes.

The application is designed as a local medical reference and clinical decision-support prototype. It does not use cloud APIs and does not send medical data outside the user's computer.

> **Medical disclaimer:**  
> MedCompare is an educational and technical prototype. It does not replace a physician, pharmacist, or qualified medical professional. Missing interaction data does not mean that a drug combination is safe. Every result must be clinically verified.

---

# 🇵🇱 Opis projektu

MedCompare to aplikacja desktopowa napisana w technologii **WPF / .NET 8**, której celem jest lokalne wyszukiwanie informacji o lekach, substancjach czynnych, interakcjach oraz kodach ICD.

Aplikacja działa lokalnie na komputerze użytkownika. W trybie portable korzysta z lokalnej bazy **SQLite**, dzięki czemu nie wymaga instalowania PostgreSQL ani żadnego zewnętrznego serwera bazy danych.

Projekt powstał jako prototyp systemu wspierającego analizę danych medycznych, a nie jako narzędzie do samodzielnego podejmowania decyzji klinicznych.

---

## Główne funkcje

- wyszukiwanie leku po nazwie,
- wykrywanie substancji czynnych leku,
- ręczne dodawanie substancji czynnych,
- sprawdzanie znanych interakcji między substancjami,
- obsługa lokalnej bazy interakcji opartej o dane DDInter,
- wyszukiwarka leków z rejestru polskiego,
- wyszukiwarka kodów ICD,
- podgląd statusu lokalnej bazy danych,
- historia sprawdzanych przypadków,
- audit log działań użytkownika,
- eksport raportu z aktualnej analizy,
- tryb portable z lokalną bazą SQLite.

---

## Aktualna architektura

Aplikacja została zbudowana w oparciu o podział na warstwy:

```text
Views
ViewModels
Services
Repositories
Database
Models
````

Najważniejsze elementy:

```text
WPF UI
  ↓
MainViewModel
  ↓
Application Services
  ↓
Repository interfaces
  ↓
SQLite repositories
  ↓
data/medcompare.db
```

W trybie SQLite aplikacja korzysta z repozytoriów:

* `SqliteDrugRepository`
* `SqliteSubstanceRepository`
* `SqliteInteractionRepository`
* `SqlitePolishDrugRegistryRepository`
* `SqliteIcdCodeRepository`
* `SqliteAuditLogRepository`
* `SqliteDatabaseStatusService`

Dzięki temu aplikacja może działać bez PostgreSQL.

---

## Źródła danych

Projekt korzysta z lokalnie zaimportowanych danych:

| Obszar                      | Źródło / tabela              |
| --------------------------- | ---------------------------- |
| Leki EMA                    | `drugs`                      |
| Powiązania lek → substancja | `drug_active_substances`     |
| Substancje czynne           | `active_substances`          |
| Interakcje substancji       | `substance_interactions`     |
| Polski rejestr leków        | `polish_drug_registry_items` |
| ICD                         | `icd_codes`                  |
| Audit log                   | `audit_logs`                 |

Przepływ dla sprawdzania interakcji:

```text
Drug name
  ↓
EMA drug record
  ↓
active substances
  ↓
DDInter substance mapping
  ↓
substance_interactions
  ↓
interaction result
```

Polski rejestr leków jest używany w osobnej zakładce i nie powinien być głównym źródłem danych dla Interaction Checker.

---

## Tryb portable

Aplikacja może zostać opublikowana jako samodzielny folder:

```text
publish/portable/
├─ DrugCompare.exe
├─ appsettings.json
└─ data/
   └─ medcompare.db
```

Wersja portable używa konfiguracji:

```json
{
  "Database": {
    "Provider": "SQLite"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/medcompare.db"
  }
}
```

Baza danych `medcompare.db` nie powinna być commitowana bezpośrednio do repozytorium. Powinna być dostarczana jako osobny plik release albo lokalnie generowana/importowana.

---

## Uruchomienie projektu lokalnie

Wymagania:

* Windows
* .NET 8 SDK
* Visual Studio 2022/2026 lub inny edytor obsługujący .NET
* opcjonalnie `sqlite3` do inspekcji bazy danych

Kroki:

```powershell
git clone https://github.com/Faldekk/MedCompare2.git
cd MedCompare2/DrugCompare
dotnet restore
dotnet build
dotnet run
```

Jeżeli aplikacja działa w trybie SQLite, w folderze aplikacji musi istnieć:

```text
data/medcompare.db
```

---

## Publikacja wersji portable

```powershell
dotnet clean
Remove-Item -Recurse -Force .\bin, .\obj -ErrorAction SilentlyContinue

dotnet publish .\DrugCompare.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  -o .\publish\portable

New-Item -ItemType Directory -Path .\publish\portable\data -Force

Copy-Item .\data\medcompare.db .\publish\portable\data\medcompare.db -Force
Copy-Item .\appsettings.json .\publish\portable\appsettings.json -Force

Compress-Archive `
  -Path .\publish\portable\* `
  -DestinationPath .\MedCompare-portable.zip `
  -Force
```

Po rozpakowaniu ZIP-a aplikacja powinna działać bez instalowania bazy danych.

---

## Ważne informacje bezpieczeństwa

MedCompare nie powinien być traktowany jako system decydujący o leczeniu.

Komunikat bezpieczeństwa stosowany w aplikacji:

```text
No known interaction was found in the local database.
Missing interaction data does not mean that the combination is safe.
```

Oznacza to, że brak wyniku w lokalnej bazie nie jest potwierdzeniem bezpieczeństwa terapii.

---

## Status projektu

Obecnie projekt posiada:

* działający tryb SQLite portable,
* lokalne wyszukiwanie leków z danych EMA,
* mapowanie leków na substancje czynne,
* sprawdzanie interakcji substancji,
* fallback dla duplikatów substancji przez `normalized_name`,
* działający widok Interaction Checker,
* widok statusu bazy danych SQLite,
* obsługę polskiego rejestru leków,
* obsługę kodów ICD,
* audit log,
* eksport raportu.

---

## Shoutout

Projekt rozwijany jako praktyczny prototyp lokalnej aplikacji medycznej i systemu wspierającego analizę danych lekowych.

Special shoutout dla taty za bycie realnym powodem, żeby doprowadzić aplikację do wersji portable, która faktycznie działa poza środowiskiem developerskim.

---

# 🇬🇧 English Description

MedCompare is a desktop application built with **WPF / .NET 8** for local drug lookup, active substance detection, interaction checking, Polish drug registry search, and ICD code search.

The application runs locally on the user's machine. In portable mode, it uses a local **SQLite** database, so it does not require PostgreSQL or any external database server.

The project is a clinical decision-support prototype and a medical data reference tool. It is not intended to replace professional medical judgment.

---

## Main features

* drug lookup by name,
* active substance detection,
* manual active substance entry,
* known substance interaction checking,
* local DDInter-based interaction database,
* Polish drug registry search,
* ICD code search,
* local database status view,
* interaction history,
* audit log,
* report export,
* portable SQLite mode.

---

## Current architecture

The application follows a layered structure:

```text
Views
ViewModels
Services
Repositories
Database
Models
```

Main flow:

```text
WPF UI
  ↓
MainViewModel
  ↓
Application Services
  ↓
Repository interfaces
  ↓
SQLite repositories
  ↓
data/medcompare.db
```

In SQLite mode, the application uses:

* `SqliteDrugRepository`
* `SqliteSubstanceRepository`
* `SqliteInteractionRepository`
* `SqlitePolishDrugRegistryRepository`
* `SqliteIcdCodeRepository`
* `SqliteAuditLogRepository`
* `SqliteDatabaseStatusService`

This allows the application to run without PostgreSQL.

---

## Data sources

The application uses locally imported data:

| Area                          | Source / table               |
| ----------------------------- | ---------------------------- |
| EMA drugs                     | `drugs`                      |
| Drug-active substance mapping | `drug_active_substances`     |
| Active substances             | `active_substances`          |
| Substance interactions        | `substance_interactions`     |
| Polish drug registry          | `polish_drug_registry_items` |
| ICD codes                     | `icd_codes`                  |
| Audit log                     | `audit_logs`                 |

Interaction checking flow:

```text
Drug name
  ↓
EMA drug record
  ↓
active substances
  ↓
DDInter substance mapping
  ↓
substance_interactions
  ↓
interaction result
```

The Polish drug registry is used in a separate module and is not the main source for the Interaction Checker.

---

## Portable mode

The portable release should have this structure:

```text
publish/portable/
├─ DrugCompare.exe
├─ appsettings.json
└─ data/
   └─ medcompare.db
```

Portable configuration:

```json
{
  "Database": {
    "Provider": "SQLite"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/medcompare.db"
  }
}
```

The `medcompare.db` database file should not be committed directly to the repository. It should be distributed as a release artifact or generated/imported locally.

---

## Running locally

Requirements:

* Windows
* .NET 8 SDK
* Visual Studio 2022/2026 or another .NET-compatible editor
* optional `sqlite3` for database inspection

Steps:

```powershell
git clone https://github.com/Faldekk/MedCompare2.git
cd MedCompare2/DrugCompare
dotnet restore
dotnet build
dotnet run
```

For SQLite mode, the following file must exist:

```text
data/medcompare.db
```

---

## Publishing portable release

```powershell
dotnet clean
Remove-Item -Recurse -Force .\bin, .\obj -ErrorAction SilentlyContinue

dotnet publish .\DrugCompare.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  -o .\publish\portable

New-Item -ItemType Directory -Path .\publish\portable\data -Force

Copy-Item .\data\medcompare.db .\publish\portable\data\medcompare.db -Force
Copy-Item .\appsettings.json .\publish\portable\appsettings.json -Force

Compress-Archive `
  -Path .\publish\portable\* `
  -DestinationPath .\MedCompare-portable.zip `
  -Force
```

After extracting the ZIP file, the application should run without installing a database server.

---

## Safety notice

MedCompare should not be treated as a system that makes medical decisions.

The application uses the following safety wording:

```text
No known interaction was found in the local database.
Missing interaction data does not mean that the combination is safe.
```

This means that missing data in the local database is not proof that a combination is safe.

---

## Project status

The current version includes:

* working SQLite portable mode,
* local EMA drug lookup,
* drug-to-active-substance mapping,
* substance interaction checking,
* fallback matching for duplicated substances through `normalized_name`,
* working Interaction Checker view,
* SQLite database status view,
* Polish drug registry support,
* ICD code support,
* audit log,
* report export.

---

## Shoutout

This project is developed as a practical prototype of a local medical reference application and drug data analysis tool.

Special shoutout to my dad for being the real reason to finish the portable version and make the app usable outside the development environment.

```
```
