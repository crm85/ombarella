# Ombarella SPT 3.11

This branch contains the SPT 3.11 implementation of Ombarella.

Build with the target SPT 3.11 install path:

```powershell
dotnet build .\Ombarella.SPT311.csproj -c Release -p:SptInstallPath="C:\path\to\spt-3.11"
```

You can also set `SPT_311_INSTALL_PATH`. If neither value is set, the project keeps the legacy fallback to `C:\spt_311`.

The build copies `ombarella.dll` to:

```text
release\BepInEx\plugins\ombarella.dll
```

The shader bundle is kept under:

```text
release\BepInEx\plugins\Ombarella\
```
