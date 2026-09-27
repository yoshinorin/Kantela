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
| AngleSharp | 1.8.2 | Kantela.Core (HTML parsing for site registration; added 2026-09-27) |
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
    string? Alias;               // Optional name shown instead of Title (added by migration AddSiteAlias)
    string Url;                  // Unique
    string? FeedUrl;
    DateTime CreatedAt;          // UTC
    DateTime? LastVisitedAt;     // UTC. Opened in the browser
    DateTime? LastPreviewedAt;   // UTC. Preview loaded successfully (added by migration AddLastPreviewedAt)
}
```

- All timestamps are stored as UTC `DateTime`. `DateTimeOffset` is avoided because the EF Core SQLite provider cannot translate ordering/comparison on it.
- All sites are kept in a single **flat** list. There is no folder/category grouping.
- There is no stored display order (the former `SortOrder` column was dropped by migration `RemoveSortOrder`). The list is sorted in memory (see "6. UI"); the number of sites is expected to be small.
- Registration order is `Id` order.
- URL comparison (uniqueness, import matching) uses `UrlNormalizer.ComparisonKey`: the scheme (http/https), host case, default port, fragment and trailing slashes are ignored; the path and query are compared as is. The key is not stored; sites are compared in memory. The stored URL is the trimmed input. The database also keeps a unique index on the raw `Url`.
- Tags are out of scope for now. They can be added later as a many-to-many relation.
- Update-check fields are added in Phase 4 via a migration (see section 9).

## 5. Features

### MUST

- Opening a site launches the default browser (`Launcher.LaunchUriAsync`) and records `LastVisitedAt`.
- Selecting a site shows a screenshot preview (see "Preview" below). A preview is not a visit.
- Sorting by registration order, name, last previewed date or last visited date.
- Add / edit / delete sites. Adding and editing check the site over the network (see "Site Registration" below).
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
| Main | Left: a single `ListView` of all sites with clickable column headers (#, Name, Previewed, Visited). Each row shows its registration number, title and URL, last previewed date, last visited date, and (Phase 4) an update indicator. Right: the preview of the selected site with an "Open in browser" button. The border between them can be dragged |
| Command bar | Add, Import (JSON/OPML), Export (JSON/OPML); "Open data folder" in the overflow menu |
| Site dialog | `ContentDialog` for Url, Title (optional), Alias (optional), FeedUrl (optional). Saving keeps the dialog open while the site is checked, and shows errors inside the dialog |

Interactions:

- Click on a site: select it and show its preview.
- Double click (or Enter) on a site: open it in the browser.
- Click on the preview: open the selected site (its registered URL) in the browser.
- Right click (context menu): Open in browser / Edit / Delete.
- Click on a column header: sort by that column. Clicking the current column reverses the direction.
  - #: by `Id` (registration order). The column shows 1, 2, 3, ... in `Id` order among the listed sites, not the `Id` itself, so there are no gaps; the numbers are reassigned when a site is deleted. `CreatedAt` is not shown.
  - Name: by title (current culture, case-insensitive). Previewed / Visited: by the date; sites without the date always come last.
  - A new column starts ascending for # and Name, and descending (newest first) for Previewed and Visited. Ties are broken by `Id`.
  - The list is not re-sorted when a date changes, or when a site is added (it is appended) or edited. The new order is applied on the next header click or start.
  - The chosen column and direction are saved to `settings.json`.

### Preview

- The preview is only for checking what a site looks like; browsing is left to the default browser.
  - The header above the preview says so, and the cursor over the page is a hand.
- `LastPreviewedAt` is recorded when the page finishes loading successfully. It does not affect `LastVisitedAt`.
- A single WebView2 shows the page, covered by a transparent layer that blocks all input.
  - Only mouse wheel input is forwarded, via the DevTools Protocol (`Input.dispatchMouseEvent`), so the page can be scrolled.
  - Only the navigation started by Kantela (and its redirects) is allowed; page-initiated navigations are cancelled.
  - Muted; context menus, DevTools, browser accelerator keys, script dialogs, downloads and new windows are disabled.
- No browsing data is kept: the WebView2 runs in InPrivate mode, and its user data folder is deleted before the first use in each run.
- Requires the WebView2 Runtime (preinstalled on Windows 11).

### Site Registration

Done by `SiteInspector` when a site is saved from the dialog (add or edit). Import does not access the network.

1. Duplicate check by normalized URL. A registered site is not fetched.
2. The site's page is fetched when the site is new, its URL changed, or the title is empty. If it cannot be fetched (network error, timeout, or a 4xx/5xx status after redirects), the site is not saved.
3. An empty title is taken from `<title>` (whitespace collapsed), or the URL when the page has none.
4. An empty feed URL is detected from `<link rel="alternate" type="application/rss+xml|application/atom+xml">`, resolved against the final page address, when the site is new or its URL changed.
   - None: saved without a feed.
   - One: used.
   - Several: not saved; the candidates are listed so that the user enters one.
5. A feed URL entered by the user (new, or changed when editing) must be reachable and its root element must be `rss`, `feed` or `RDF`; otherwise the site is not saved.

HTTP (`HttpWebClient`):

- `User-Agent: Kantela/<version> (+https://github.com/yoshinorin/Kantela)`.
- Timeout 10 seconds; redirects are followed; at most 2 MB of each response is read.
- Character encodings from `Content-Type` or `<meta>` are handled by AngleSharp, with the code pages provider registered for legacy encodings such as Shift_JIS.
- Feed XML is read with DTD processing ignored and no resolver.
- Closing the dialog while checking cancels the check.

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
      "createdAt": "2026-01-01T00:00:00Z",
      "lastVisitedAt": null,
      "lastPreviewedAt": null,
      "alias": null
    }
  ]
}
```

- Database IDs are not exported.
- `version` identifies the schema for future migrations of the format.
- `lastPreviewedAt` and `alias` are optional, so files exported before they were added can still be imported. OPML does not carry the alias.
- Sites are written in registration order and imported in file order. `sortOrder` in files exported by older versions is ignored.

### OPML (2.0)

- Export
  - Each site becomes a top-level `<outline type="rss" text title xmlUrl htmlUrl>`, in registration order.
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
  - `WebView2\` (WebView2 user data for previews; deleted on each start)
  - `settings.json` (sort column and direction; defaults are used when it is missing or invalid)
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
- Targets: JSON/OPML import and export (including the skip/warning rules), import modes, backup retention, settings, sorting, view models.

## 14. Tooling

- `dotnet-ef` is installed as a local tool (`dotnet-tools.json`). Restore with `dotnet tool restore`.
- Add a migration: `dotnet ef migrations add <Name> --project src/Kantela.Core --output-dir Data/Migrations`.
- Migrations are applied automatically at startup.

## 15. Implementation Phases

| Phase | Scope |
|---|---|
| 0 | Restructure solution, upgrade to .NET 10 and latest packages, add Core/Tests projects, fix README |
| 1 | Models, DbContext, initial migration, storage paths, logging setup |
| 2 | Main UI: list, open in browser, `LastVisitedAt`, site CRUD |
| 3 | JSON/OPML import/export, import modes, backup on exit |
| 4 | Update check |
