$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$testInstall=Join-Path $projectRoot 'verification/installed-app'
$fullTarget=[IO.Path]::GetFullPath($testInstall)
$fullWorkspace=[IO.Path]::GetFullPath($projectRoot).TrimEnd('\')+'\'
if(-not $fullTarget.StartsWith($fullWorkspace,[StringComparison]::OrdinalIgnoreCase)){throw '安装测试目录必须位于本项目内'}
$setup=Join-Path $projectRoot 'dist/CyberGolden-Setup-0.3.0-x64.exe'
$installLog=Join-Path $projectRoot 'verification/install.log'
$installProcess=Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$fullTarget+'"'),('/LOG="'+$installLog+'"')) -PassThru -Wait -WindowStyle Hidden
if($installProcess.ExitCode -ne 0){throw ('安装失败：'+$installProcess.ExitCode)}
$installedExe=Join-Path $fullTarget 'CyberGolden.exe'
if(-not(Test-Path -LiteralPath $installedExe)){throw '没有实际安装 EXE'}
$shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) '赛博金毛/赛博金毛.lnk'
$hasShortcut=Test-Path -LiteralPath $shortcut
$runtimeFiles=@('coreclr.dll','hostfxr.dll','libSkiaSharp.dll','assets/pet/manifest.json','assets/audio/click.wav')
$missing=$runtimeFiles | Where-Object {-not(Test-Path -LiteralPath (Join-Path $fullTarget $_))}
if($missing.Count){throw ('安装缺少实体资源：'+($missing -join ','))}
$testOutput=Join-Path $projectRoot 'verification/installed-run'
$testData=Join-Path $projectRoot 'test-data/installed'
$appProcess=Start-Process -FilePath $installedExe -ArgumentList @('--self-test','--data-dir',('"'+$testData+'"'),'--output',('"'+$testOutput+'"')) -PassThru -Wait -WindowStyle Hidden
if($appProcess.ExitCode -ne 0 -or -not(Test-Path -LiteralPath (Join-Path $testOutput 'results.json'))){throw '安装后程序未通过实际运行验证'}
# An explicit temporary marker verifies the uninstaller's default data-retention path.
# Never overwrite a real user save, and never remove the user's data directory.
$userData=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CyberGolden'
$probe=Join-Path $userData 'install-verification-retention-probe.txt'
if(Test-Path -LiteralPath $probe){throw '保留性测试标记已存在，拒绝覆盖'}
New-Item -ItemType Directory -Force -Path $userData | Out-Null
'CyberGolden uninstall retention verification' | Set-Content -LiteralPath $probe -Encoding utf8
$uninstallLog=Join-Path $projectRoot 'verification/uninstall.log'
$uninstaller=Join-Path $fullTarget 'unins000.exe'
$uninstallProcess=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+$uninstallLog+'"')) -PassThru -Wait -WindowStyle Hidden
$retained=Test-Path -LiteralPath $probe
$removed=-not(Test-Path -LiteralPath $installedExe)
$shortcutRemoved=-not(Test-Path -LiteralPath $shortcut)
# Clean only the exact marker created by this test. The directory and saves stay intact.
Remove-Item -LiteralPath $probe
[ordered]@{testedAt=(Get-Date).ToString('o');installExit=$installProcess.ExitCode;startMenuCreated=$hasShortcut;runtimeResourcesPresent=($missing.Count -eq 0);installedRunExit=$appProcess.ExitCode;uninstallExit=$uninstallProcess.ExitCode;applicationRemoved=$removed;startMenuRemoved=$shortcutRemoved;defaultUserDataRetained=$retained;cleanMachine=$false;note='Current Windows machine, isolated installation; no clean VM/no-.NET test claimed'} | ConvertTo-Json | Set-Content -Encoding utf8 verification/install-results.json
if(-not($retained -and $removed -and $hasShortcut -and $shortcutRemoved -and $uninstallProcess.ExitCode -eq 0)){throw '安装/卸载验证未全部通过'}
