# Ombarella SPT 4.0

This branch contains the SPT 4.0 implementation of Ombarella.

Build with the target SPT install path:

```powershell
dotnet build .\Ombarella.SPT4.csproj -c Release -p:SptInstallPath="C:\path\to\your\spt"
```

You can also set `SPT_INSTALL_PATH` instead of passing `SptInstallPath`.
For solution builds, prefer `SPT_4_INSTALL_PATH` so it can coexist with `SPT_311_INSTALL_PATH`.

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
