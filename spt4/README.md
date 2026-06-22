# Ombarella SPT 4 Conversion

This folder is the SPT 4 / newer EFT conversion of the root Ombarella mod.

Build with the target SPT install path:

```powershell
dotnet build .\Ombarella.SPT4.csproj -c Release -p:SptInstallPath="C:\path\to\your\spt"
```

You can also set `SPT_INSTALL_PATH` instead of passing `SptInstallPath`.

The build copies `ombarella.dll` to:

```text
release\BepInEx\plugins\ombarella.dll
```

The shader bundle is kept under:

```text
release\BepInEx\plugins\Ombarella\
```

The packaged build is:

```text
release\ombarella-spt4-0.5.1.zip
```

This build accepts either `Ombarella\shader` or `Ombarella\ombhistogram` as the compute shader bundle name.
