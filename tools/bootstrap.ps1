param([switch]$AssetTools)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
if(-not(Test-Path tools/dotnet/dotnet.exe)){
  Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile tools/dotnet-install.ps1
  & ./tools/dotnet-install.ps1 -Version 10.0.401 -InstallDir (Join-Path $PSScriptRoot 'dotnet') -NoPath
}
if(-not(Test-Path tools/inno/tools/ISCC.exe)){
  Invoke-WebRequest https://www.nuget.org/api/v2/package/Tools.InnoSetup/6.7.3 -OutFile tools/inno.zip
  Expand-Archive tools/inno.zip tools/inno -Force
}
if($AssetTools){
  # Only needed to regenerate deterministic frame/audio preparation; never at runtime.
  python -m pip install Pillow numpy scipy soundfile --target tools/pythonlibs
}
