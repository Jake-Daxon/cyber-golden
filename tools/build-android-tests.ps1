param([string]$JavaHome='C:\Program Files\Java\jdk-21.0.12.1')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot;$env:JAVA_HOME=$JavaHome;$env:PATH="$JavaHome/bin;"+$env:PATH
$sdk=Join-Path $PSScriptRoot 'android-sdk';$bt=Join-Path $sdk 'build-tools/36.0.0';$jar=Join-Path $sdk 'platforms/android-36/android.jar'
$build=Join-Path $project 'android/build/instrumentation';New-Item -ItemType Directory -Path "$build/classes","$build/dex" -Force | Out-Null
function Run([string]$exe,[string[]]$argv){& $exe @argv;if($LASTEXITCODE -ne 0){throw "$exe failed"}}
Run "$JavaHome/bin/javac.exe" @('-encoding','UTF-8','-source','8','-target','8','-cp',"$jar;$project/android/build/classes",'-d',"$build/classes","$project/android/tests/instrumentation/AndroidTestRunner.java")
$stage=Join-Path ([IO.Path]::GetTempPath()) ('cybergolden-tests-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item $jar,"$project/android/tests/instrumentation/AndroidManifest.xml" $stage
Push-Location $stage
try{Run "$bt/aapt2.exe" @('link','-o','base.apk','-I','android.jar','--manifest','AndroidManifest.xml')}finally{Pop-Location}
$classes=@(Get-ChildItem "$build/classes" -Recurse -Filter '*.class' | ForEach-Object FullName)
Run "$bt/d8.bat" (@('--min-api','26','--lib',$jar,'--classpath',"$project/android/build/classes",'--output',"$build/dex")+$classes)
Run "$JavaHome/bin/jar.exe" @('uf',"$stage/base.apk",'-C',"$build/dex",'classes.dex')
Push-Location $stage
try{Run "$bt/zipalign.exe" @('-f','4','base.apk','aligned.apk')}finally{Pop-Location}
$out=Join-Path $build 'tests.apk';$key=Join-Path $project '.android-private/release.jks';$pass=Join-Path $project '.android-private/signing-password.txt'
Run "$bt/apksigner.bat" @('sign','--ks',$key,'--ks-pass',"file:$pass",'--out',$out,"$stage/aligned.apk")
"Test harness APK: $out (never distribute)"
