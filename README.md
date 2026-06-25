# Ombarella

Ombarella now keeps the supported SPT branches in parallel top-level folders:

```text
spt-3.11/
  src/
  release/
  Ombarella.SPT311.csproj

spt-4.0/
  src/
  release/
  Ombarella.SPT4.csproj
```

The root solution includes both projects:

```powershell
dotnet build .\ombarella.sln -c Release
```

For solution builds, set the branch-specific install paths so each project resolves against the matching SPT install:

```powershell
$env:SPT_311_INSTALL_PATH = "C:\path\to\spt-3.11"
$env:SPT_4_INSTALL_PATH = "C:\path\to\spt-4.0"
dotnet build .\ombarella.sln -c Release
```

For a single branch build, you can also pass `SptInstallPath` directly to that project.
