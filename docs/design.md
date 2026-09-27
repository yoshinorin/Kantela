# Kantela Design

## 1. Concept

- A replacement for browser bookmarks.
- Values visiting sites directly: Kantela is **not** a feed reader.
  - It only tells whether a site has been updated.
  - Article contents are never stored.

## 2. Technology Stack

| Area | Choice |
|---|---|
| UI | WinUI 3 (Windows App SDK). No cross-platform requirement, so not .NET MAUI |
| Language / Runtime | C# / .NET 10 |
| Persistence | Entity Framework Core + SQLite |
| MVVM | CommunityToolkit.Mvvm |
| Logging | Microsoft.Extensions.Logging + ZLogger (rolling file) |
| Testing | MSTest |

### Packages

| Package | Version | Project |
|---|---|---|
| Microsoft.WindowsAppSDK | 2.5.1 | Kantela |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | Kantela |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 | Kantela.Core |
| Microsoft.EntityFrameworkCore.Tools | 10.0.12 | Kantela.Core |
| CommunityToolkit.Mvvm | 8.4.2 | Kantela.Core |
| ZLogger | 2.5.10 | Kantela |
| MSTest.Sdk (project SDK) | 4.4.1 | Kantela.Core.Tests |

Versions are the latest stable releases on NuGet as of 2026-09-27.

### Project Settings

- `TargetFramework`: `net10.0-windows10.0.19041.0` (app), `net10.0` (core, tests).
- `PublishTrimmed`: `False` for all configurations, because EF Core is not trim-safe.

## 3. Solution Structure

```
Kantela.sln
src/
  Kantela/              WinUI 3 app (Views, platform services, composition root)
  Kantela.Core/         UI-independent logic
    Models/
    Data/               DbContext, migrations
    Services/           Import/Export, backup, site operations
    ViewModels/
tests/
  Kantela.Core.Tests/
docs/
```

- The existing app project moves from `src/` to `src/Kantela/`, so that SDK globbing does not pull `Kantela.Core` sources into the app project.
- Platform-dependent operations (launching the browser, file pickers, dialogs) are abstracted behind interfaces in `Kantela.Core` and implemented in `Kantela`.

## 4. Data Model

```csharp
class Site
{
    int Id;
    string Title;
    string Url;                  // Unique
    string? FeedUrl;
    int SortOrder;
    DateTime CreatedAt;          // UTC
    DateTime? LastVisitedAt;     // UTC
}
```

- All timestamps are stored as UTC `DateTime`. `DateTimeOffset` is avoided because the EF Core SQLite provider cannot translate ordering/comparison on it.
- All sites are kept in a single **flat** list. There is no folder/category grouping.
- `SortOrder` is a single global order, renumbered on every move. The number of sites is expected to be small.
- URL comparison (uniqueness, import matching) uses ordinal comparison of the trimmed string.
- Tags are out of scope for now. They can be added later as a many-to-many relation.
- Update-check fields are added in Phase 4 via a migration (see section 9).

## 5. Features

### MUST

- Selecting a site opens it in the default browser (`Launcher.LaunchUriAsync`) and records `LastVisitedAt`.
- Arbitrary ordering by drag and drop.
- Add / edit / delete sites.
- OPML import/export.
- JSON import/export (covers fields that OPML cannot hold).
- Automatic JSON backup on exit.

### WANT

- Periodic update check (Phase 4).
- Tags (not planned yet).

### Not Planned

- Folders / categories. All sites stay in one flat list.
- Manual (on-demand) update check. It would encourage frequent checking ("zapping").

## 6. UI

| Screen | Contents |
|---|---|
| Main | A single `ListView` of all sites with reorder support. Each item shows title, URL, last visited date, and (Phase 4) an update indicator |
| Command bar | Add, Import (JSON/OPML), Export (JSON/OPML); "Open data folder" in the overflow menu |
| Site dialog | `ContentDialog` for Title, Url, FeedUrl |

Interactions:

- Click (or Enter) on a site: open it in the browser.
- Right click (context menu): Open / Edit / Delete.
- Drag and drop: reorder. The order is saved when the drag completes.

## 7. Import / Export

### JSON

```json
{
  "version": 1,
  "exportedAt": "2026-09-27T00:00:00Z",
  "sites": [
    {
      "title": "Example",
      "url": "https://example.com/",
      "feedUrl": "https://example.com/feed.xml",
      "sortOrder": 0,
      "createdAt": "2026-01-01T00:00:00Z",
      "lastVisitedAt": null
    }
  ]
}
```

- Database IDs are not exported.
- `version` identifies the schema for future migrations of the format.

### OPML (2.0)

- Export
  - Each site becomes a top-level `<outline type="rss" text title xmlUrl htmlUrl>`, in `SortOrder`.
  - Sites without `FeedUrl` are **not exported** (OPML 2.0 requires `xmlUrl` for `type="rss"`). A warning is logged for each skipped site.
- Import
  - `htmlUrl` becomes `Url` and `xmlUrl` becomes `FeedUrl`.
  - If `htmlUrl` is missing, `xmlUrl` is used as `Url` and a warning is logged.
  - Grouping outlines (folders in other readers) are ignored; all site outlines are flattened into one list in document order.

### Import Modes

The user chooses the mode on each import.

| Mode | Behavior |
|---|---|
| Merge | Appends sites whose URL does not exist yet to the end of the list. Sites with an existing URL are **skipped** |
| Replace | Deletes all sites, then inserts the imported data. A backup is taken before replacing |

Each import runs in a single transaction.

## 8. Backup

- Timing: **on exit only**.
  - Normal exit: `MainWindow.Closed`.
  - Abnormal exit: `Application.UnhandledException`, `AppDomain.CurrentDomain.UnhandledException`.
  - `TaskScheduler.UnobservedTaskException` is only logged: it does not terminate the process on .NET, so it is not an exit.
  - Not covered: forced termination (e.g. Task Manager), `StackOverflowException`, `Environment.FailFast`, power loss.
- Format: the same JSON as the export.
- Location: `<data dir>/backups/kantela-yyyyMMdd-HHmmss.json`.
- Written to a temporary file and then renamed, so a partial write never replaces a valid backup.
- Retention: the newest **5** files.
- Skipped when there are no sites, so that repeated exits with an empty list do not rotate out valid backups.

## 9. Update Check (WANT, Phase 4)

- Runs only at application startup, for sites whose last check is 24 hours or older.
- Strategy
  1. If `FeedUrl` exists: use the date of the newest entry in the feed.
  2. Otherwise: conditional GET with `ETag` / `Last-Modified`.
  - HTML diffing is not used (noisy and expensive).
- Limited concurrency (2-4), explicit `User-Agent`.
- Additional fields on `Site`: `LastCheckedAt`, `LastUpdatedAt`, `ETag`, `LastModified`.
- "Updated" is derived: `LastUpdatedAt > LastVisitedAt`.

## 10. Storage Locations

- Data directory: `%LOCALAPPDATA%\Kantela\`
  - `kantela.db`
  - `backups\`
  - `logs\`
- The app is unpackaged, so this path is used as is for both Debug and Release builds.

## 11. Deployment

- Unpackaged (`WindowsPackageType=None`), with the Windows App SDK runtime bundled (`WindowsAppSDKSelfContained=true`) and a self-contained .NET runtime.
- Distributed by copying the publish folder (xcopy deployment). No installer and no signing.
- `EnableMsixTooling=true` is still required: without it, `Kantela.pri` (which contains the compiled XAML) is missing from the publish output and the app fails at startup with `XamlParseException`.

## 12. Logging

- `Microsoft.Extensions.Logging` abstractions in `Kantela.Core`. ZLogger is configured only in the app project.
- Output: `logs/kantela-yyyyMMdd_NNN.log`, rolled daily or when a file exceeds 1 MB (`NNN` is the sequence number within the day).

## 13. Testing

- `tests/Kantela.Core.Tests` (MSTest) references `Kantela.Core` only.
- Uses Microsoft.Testing.Platform (configured in `global.json`). Run with `dotnet test --project tests/Kantela.Core.Tests/Kantela.Core.Tests.csproj`.
- EF Core tests use SQLite in-memory (`DataSource=:memory:` with an open connection).
- Targets: JSON/OPML import and export (including the skip/warning rules), import modes, backup retention, reordering logic, view models.

## 14. Tooling

- `dotnet-ef` is installed as a local tool (`dotnet-tools.json`). Restore with `dotnet tool restore`.
- Add a migration: `dotnet ef migrations add <Name> --project src/Kantela.Core --output-dir Data/Migrations`.
- Migrations are applied automatically at startup.

## 15. Implementation Phases

| Phase | Scope |
|---|---|
| 0 | Restructure solution, upgrade to .NET 10 and latest packages, add Core/Tests projects, fix README |
| 1 | Models, DbContext, initial migration, storage paths, logging setup |
| 2 | Main UI: list, open in browser, `LastVisitedAt`, reorder, site CRUD |
| 3 | JSON/OPML import/export, import modes, backup on exit |
| 4 | Update check |
