param([switch]$SkipBuild,[switch]$SkipTests)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$sdk=Join-Path $PSScriptRoot 'dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $sdk)){$sdk=(Get-Command dotnet -ErrorAction Stop).Source}
$publishDir=Join-Path $projectRoot 'dist/CyberGolden-0.3.0-win-x64'
if(-not [IO.Path]::GetFullPath($publishDir).StartsWith([IO.Path]::GetFullPath($projectRoot).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw '发布路径超出项目'}
if(-not $SkipBuild){
  & $sdk publish src/CyberGolden/CyberGolden.csproj -c Release -r win-x64 --self-contained true -o $publishDir -p:PublishSingleFile=false -p:PublishTrimmed=false -v minimal
  if($LASTEXITCODE -ne 0){throw '自包含发布失败'}
}
$deps=Get-Content -Raw (Join-Path $publishDir 'CyberGolden.deps.json') | ConvertFrom-Json
$licenses=Join-Path $publishDir 'licenses'
New-Item -ItemType Directory -Force -Path $licenses | Out-Null
foreach($entry in $deps.libraries.PSObject.Properties){
  if($entry.Value.type -ne 'package'){continue}
  $packageName,$packageVersion=$entry.Name.Split('/')
  $packageDir=Join-Path $projectRoot ('tools/nuget/'+$packageName.ToLowerInvariant()+'/'+$packageVersion)
  if(-not(Test-Path $packageDir)){continue}
  $target=Join-Path $licenses ($packageName+'-'+$packageVersion)
  New-Item -ItemType Directory -Force -Path $target | Out-Null
  Get-ChildItem -LiteralPath $packageDir -File | Where-Object { $_.Name -match '^(LICENSE|NOTICE|THIRD.PARTY)' -or $_.Extension -eq '.nuspec' } | Copy-Item -Destination $target
  if($packageName -like 'Panuon*'){Copy-Item docs/LICENSE-VPet.txt (Join-Path $target 'Apache-2.0.txt')}
}
Copy-Item docs/NOTICE.txt,docs/LICENSE-*.txt,docs/使用说明.md -Destination $publishDir -Force
# Debug symbols contain developer source paths; the offline share package excludes them.
Get-ChildItem -LiteralPath $publishDir -Filter '*.pdb' -File | ForEach-Object {Remove-Item -LiteralPath $_.FullName}
if(-not $SkipTests){
  $testRun=Join-Path $projectRoot 'verification/publish'
  $process=Start-Process -FilePath (Join-Path $publishDir 'CyberGolden.exe') -ArgumentList @('--self-test','--data-dir',('"'+(Join-Path $projectRoot 'test-data/publish')+'"'),'--output',('"'+$testRun+'"')) -PassThru -Wait -WindowStyle Hidden
  if($process.ExitCode -ne 0 -or -not(Test-Path (Join-Path $testRun 'results.json'))){throw '自包含程序验证失败'}
}
$compiler=Join-Path $PSScriptRoot 'inno/tools/ISCC.exe'
if(-not(Test-Path $compiler)){$compiler=(Get-Command iscc -ErrorAction Stop).Source}
$installerStage=Join-Path $PSScriptRoot ('installer-stage-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $installerStage | Out-Null
& $compiler /Qp ('/O'+$installerStage) (Join-Path $PSScriptRoot 'CyberGolden.iss')
if($LASTEXITCODE -ne 0){throw '安装包编译失败；临时输出保留用于诊断'}
$stageSetup=Join-Path $installerStage 'CyberGolden-Setup-0.3.0-x64.exe'
$finalSetup=Join-Path $projectRoot 'dist/CyberGolden-Setup-0.3.0-x64.exe'
$copied=$false
for($attempt=0;$attempt -lt 5 -and -not $copied;$attempt++){
  try {Copy-Item -LiteralPath $stageSetup -Destination $finalSetup -Force;$copied=$true}
  catch {if($attempt -eq 4){throw};Start-Sleep -Seconds 1}
}
$archive=Join-Path $projectRoot 'dist/CyberGolden-Portable-0.3.0-x64.zip'
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $archive -Force
Get-FileHash dist/CyberGolden-Setup-0.3.0-x64.exe,$archive -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLowerInvariant()+'  '+(Split-Path -Leaf $_.Path) } | Set-Content -Encoding utf8 dist/SHA256SUMS.txt
