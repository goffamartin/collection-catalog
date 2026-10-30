---
name: ef-migration
description: Changing the database schema of Collection Catalog – adding an EF Core migration (even while a running instance locks bin), hand-editing migrations that convert data, verifying on a copy of the data. Use for every change of entities or their configuration.
---

# EF Core migrations

## Rules
- Schema only through migrations (startup calls `MigrateAsync` after backing up the database); never `EnsureCreated`.
  Migrations are immutable once committed – add a new one.
- Entity configuration in `Data/Configurations/*Configuration.cs` (`IEntityTypeConfiguration<T>`), not in `AppDbContext`.
- Conventions from `AppDbContext.ConfigureConventions`: enums as text (max 50), decimal(18,2), `LocalizedText` as a
  JSON column (NOT NULL, empty `{}`).
- Delete behavior: codebooks referenced by items `SetNull`, category join `Cascade`, photos/offers `Cascade`,
  inquiries `SetNull` (inquiries are kept).

## Creating a migration
A running instance locks `bin/` → let `dotnet ef` build elsewhere (MSBuild takes properties from the environment):
```bash
export UseArtifactsOutput=true ArtifactsPath="<temp>/artifacts-ef/"
dotnet restore src/CollectionCatalog.Web
dotnet ef migrations add <Name> -p src/CollectionCatalog.Web -o Data/Migrations
dotnet ef migrations has-pending-model-changes -p src/CollectionCatalog.Web   # should report "No changes"
```

## Migrations that convert data
EF generates operations naively (e.g. `DropColumn` before the data is moved, `UpdateData` of seeded rows that would
overwrite edited data) → **always read the generated `Up` and fix the order**:
1. create the new table/column, 2. convert with `migrationBuilder.Sql(...)`, 3. only then drop the old one.
- Write value mappings **into the migration** as constants – never call application code.
- SQLite table rebuilds (e.g. NOT NULL changes) cannot run in one transaction – run data conversions before them.
- Write a sensible `Down`.

## Verify on a copy of the data (required for data migrations)
```bash
T="<temp>"; rm -rf "$T/data-copy" && cp -r src/CollectionCatalog.Web/App_Data "$T/data-copy"
# state BEFORE: sqlite3 / python queries on $T/data-copy/catalog.db
# apply: any CLI command that doesn't read the console – migrations run before the arguments are evaluated
Storage__DataPath="$T/data-copy" dotnet <artifacts>/bin/CollectionCatalog.Web/debug/CollectionCatalog.Web.dll user-set-password
# state AFTER + checks: counts match, no new NULLs, PRAGMA foreign_key_check empty, last __EFMigrationsHistory row
```
Never run a new migration on `App_Data` while another (older) instance uses it.

## Tests
- `DatabaseTests.Migrations_MatchModel` guards that the model matches the migrations.
- The test context gets the Identity schema v3 through `UseApplicationServiceProvider` (`TestEnvironment`).
- After adding an entity, test FK delete behavior and seeds.
