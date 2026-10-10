param(
  [switch]$SkipTests,
  [string]$SdkRoot = (Join-Path $PSScriptRoot 'android-sdk'),
  [string]$JavaHome = $env:JAVA_HOME,
  [string]$Python = 'C:\Users\Daxon\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
if(-not $JavaHome){$JavaHome=(Get-ChildItem 'C:\Program Files\Java\jdk-*' -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName}
if(-not (Test-Path "$JavaHome\bin\javac.exe")){throw '需要 JDK 17 或更高版本；请传入 -JavaHome'}
$env:JAVA_HOME=$JavaHome
$env:PATH="$JavaHome\bin;"+$env:PATH
$build=Join-Path $project 'android/build'
$dist=Join-Path $project 'dist'
$bt=Join-Path $SdkRoot 'build-tools/36.0.0'
$platform=Join-Path $SdkRoot 'platforms/android-36/android.jar'
if(-not (Test-Path $platform)){throw '请先运行 tools/bootstrap-android.ps1，或传入已安装 Android 36 / Build Tools 36.0.0 的 -SdkRoot'}
New-Item -ItemType Directory -Path "$build/classes","$build/dex","$build/tests",$dist -Force | Out-Null
& $Python (Join-Path $PSScriptRoot 'prepare-android.py')
if($LASTEXITCODE -ne 0){throw 'Assets preparation failed'}
function Run-Checked([string]$exe,[string[]]$arguments){& $exe @arguments;if($LASTEXITCODE -ne 0){throw "$exe failed with exit $LASTEXITCODE"}}
$sources=@(Get-ChildItem "$project/android/src" -Recurse -Filter '*.java' | ForEach-Object FullName)
Run-Checked "$JavaHome/bin/javac.exe" (@('-encoding','UTF-8','-source','8','-target','8','-classpath',$platform,'-d',"$build/classes")+$sources)
if(-not $SkipTests){
Run-Checked "$JavaHome/bin/javac.exe" @('-encoding','UTF-8','-d',"$build/tests","$project/android/src/com/jakedaxon/cybergolden/PetState.java","$project/android/tests/PetStateTest.java")
Run-Checked "$JavaHome/bin/java.exe" @('-cp',"$build/tests",'com.jakedaxon.cybergolden.PetStateTest')
}
$nativeStage=Join-Path ([IO.Path]::GetTempPath()) ('cybergolden-android-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $nativeStage -Force | Out-Null
Copy-Item "$project/android/res","$project/android/assets" -Destination $nativeStage -Recurse -Force
Copy-Item "$project/android/AndroidManifest.xml",$platform -Destination $nativeStage -Force
Push-Location $nativeStage
try {
  # aapt2 on Windows cannot reliably open non-ASCII absolute paths.
  Run-Checked "$bt/aapt2.exe" @('compile','--dir','res','-o','resources.zip')
  Run-Checked "$bt/aapt2.exe" @('link','-o','base.apk','-I','android.jar','--manifest','AndroidManifest.xml','-A','assets','-0','wav','resources.zip')
  Copy-Item 'base.apk','resources.zip' -Destination $build -Force
} finally { Pop-Location }
& $Python (Join-Path $PSScriptRoot 'finalize-android-zip.py') "$build/base.apk"
if($LASTEXITCODE -ne 0){throw 'APK entry normalization failed'}
$classes=@(Get-ChildItem "$build/classes" -Recurse -Filter '*.class' | ForEach-Object FullName)
Run-Checked "$bt/d8.bat" (@('--release','--min-api','26','--lib',$platform,'--output',"$build/dex")+$classes)
Copy-Item -LiteralPath "$build/base.apk" -Destination "$build/unsigned.apk" -Force
& $Python (Join-Path $PSScriptRoot 'finalize-android-zip.py') "$build/unsigned.apk" "$build/dex/classes.dex"
if($LASTEXITCODE -ne 0){throw 'DEX packaging failed'}
Copy-Item "$build/unsigned.apk" -Destination "$nativeStage/unsigned.apk" -Force
Push-Location $nativeStage
try { Run-Checked "$bt/zipalign.exe" @('-f','-p','4','unsigned.apk','aligned.apk');Copy-Item 'aligned.apk' -Destination "$build/aligned.apk" -Force } finally { Pop-Location }
$private=Join-Path $project '.android-private'
New-Item -ItemType Directory -Path $private -Force | Out-Null
$key=Join-Path $private 'release.jks';$pass=Join-Path $private 'signing-password.txt'
if(-not (Test-Path $key)){
  if(-not (Test-Path $pass)){
    $secret=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [IO.File]::WriteAllText($pass,$secret,[Text.UTF8Encoding]::new($false));$secret=$null
  }
  Run-Checked "$JavaHome/bin/keytool.exe" @('-genkeypair','-keystore',$key,'-storepass:file',$pass,'-keypass:file',$pass,'-alias','cybergolden','-keyalg','RSA','-keysize','3072','-validity','10950','-dname','CN=CyberGolden Android, OU=Independent Project')
}
$apk=Join-Path $dist 'CyberGolden-Android-0.2.1-preview.apk'
Run-Checked "$bt/apksigner.bat" @('sign','--ks',$key,'--ks-key-alias','cybergolden','--ks-pass',"file:$pass",'--out',$apk,"$build/aligned.apk")
Run-Checked "$bt/apksigner.bat" @('verify','--verbose','--print-certs',$apk)
Copy-Item $apk -Destination "$nativeStage/CyberGolden-Android-0.2.1-preview.apk" -Force
Push-Location $nativeStage
try {
  Run-Checked "$bt/zipalign.exe" @('-c','-p','4','CyberGolden-Android-0.2.1-preview.apk')
  Run-Checked "$bt/aapt2.exe" @('dump','badging','CyberGolden-Android-0.2.1-preview.apk')
} finally { Pop-Location }
$hash=(Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $dist 'Android-SHA256SUMS.txt'),"$hash  CyberGolden-Android-0.2.1-preview.apk`n",[Text.UTF8Encoding]::new($false))
Write-Output "Android APK ready: $apk"
