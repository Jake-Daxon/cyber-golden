param([string]$JavaHome=$env:JAVA_HOME)
$ErrorActionPreference='Stop'
if(-not $JavaHome){$JavaHome=(Get-ChildItem 'C:\Program Files\Java\jdk-*' -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName}
if(-not (Test-Path "$JavaHome/bin/java.exe")){throw '先安装 JDK 17 或更高版本，或传入 -JavaHome'}
$env:JAVA_HOME=$JavaHome;$env:PATH="$JavaHome/bin;"+$env:PATH
$sdk=Join-Path $PSScriptRoot 'android-sdk';$cache=Join-Path $PSScriptRoot 'android-downloads'
New-Item -ItemType Directory -Path $sdk,$cache -Force | Out-Null
if(-not (Test-Path "$sdk/cmdline-tools/latest/bin/sdkmanager.bat")){
  $zip=Join-Path $cache 'sdktools-verified.zip'
  & curl.exe -L --fail --connect-timeout 15 --max-time 900 --silent --show-error 'https://dl.google.com/android/repository/commandlinetools-win-15859902_latest.zip' -o $zip
  if($LASTEXITCODE -ne 0){throw 'SDK download failed'}
  if((Get-FileHash $zip).Hash.ToLowerInvariant() -ne '90ae805d20434428bffcb699c290860f19bb5f66a67e6b330067e3de801fb04a'){throw 'SDK SHA256 mismatch'}
  Expand-Archive -LiteralPath $zip -DestinationPath "$cache/sdktools" -Force
  New-Item -ItemType Directory -Path "$sdk/cmdline-tools/latest" -Force | Out-Null
  Copy-Item -Path "$cache/sdktools/cmdline-tools/*" -Destination "$sdk/cmdline-tools/latest" -Recurse -Force
}
# Standard SDK licences for the requested Android development, not a device setting.
@('y')*100 | & "$sdk/cmdline-tools/latest/bin/sdkmanager.bat" "--sdk_root=$sdk" --licenses | Out-File (Join-Path $cache 'licenses.log')
if($LASTEXITCODE -ne 0){throw 'SDK licences failed'}
& "$sdk/cmdline-tools/latest/bin/sdkmanager.bat" "--sdk_root=$sdk" 'platforms;android-36' 'build-tools;36.0.0' 'platform-tools'
if($LASTEXITCODE -ne 0){throw 'SDK install failed'}
function Install-Direct([string]$url,[string]$sha1,[string]$archiveFolder,[string]$destination){
  $zip=Join-Path $cache $url
  & curl.exe -L --fail --connect-timeout 15 --max-time 900 --silent --show-error ("https://dl.google.com/android/repository/"+$url) -o $zip
  if($LASTEXITCODE -ne 0){throw "Download failed: $url"}
  if((Get-FileHash $zip -Algorithm SHA1).Hash.ToLowerInvariant() -ne $sha1){throw "Checksum mismatch: $url"}
  $extract=Join-Path $cache ([IO.Path]::GetFileNameWithoutExtension($url)+'-extract')
  Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
  New-Item -ItemType Directory -Path $destination -Force | Out-Null
  Copy-Item -Path (Join-Path $extract "$archiveFolder/*") -Destination $destination -Recurse -Force
}
# Some Windows machines briefly lock freshly extracted binaries. A checked direct
# extraction uses the same official packages when sdkmanager leaves partial output.
if(-not (Test-Path "$sdk/platforms/android-36/android.jar")){Install-Direct 'platform-36_r02.zip' '2c1a80dd4d9f7d0e6dd336ec603d9b5c55a6f576' 'android-36' "$sdk/platforms/android-36"}
if(-not (Test-Path "$sdk/build-tools/36.0.0/aapt2.exe")){Install-Direct 'build-tools_r36_windows.zip' 'f16ccffd34de8790dede813a6c7d8e2c11a27b50' 'android-16' "$sdk/build-tools/36.0.0"}
if(-not (Test-Path "$sdk/platform-tools/adb.exe")){Install-Direct 'platform-tools_r37.0.1-win.zip' 'e03e78b1d80b396f1c3358e31251cb31740e1110' 'platform-tools' "$sdk/platform-tools"}
'Android SDK 36 and Build Tools 36.0.0 ready'
