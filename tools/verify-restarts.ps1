$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$exe=Join-Path $projectRoot 'dist/CyberGolden-0.3.0-win-x64/CyberGolden.exe'
$fixture=Get-Content -Raw 'test-data/publish/state.json' | ConvertFrom-Json
$results=@()
foreach($scenario in @('sleep','hidden','paused','transient','pose','hungry','hungry-hidden')){
  $data=Join-Path $projectRoot ('test-data/restart-'+$scenario)
  $output=Join-Path $projectRoot ('verification/restart-'+$scenario)
  New-Item -ItemType Directory -Force -Path $data,$output | Out-Null
  $fixture.Settings.Sleeping=$scenario -eq 'sleep'
  $fixture.Settings.Hidden=$scenario -eq 'hidden'
  $fixture.Settings.Paused=$scenario -eq 'paused'
  $fixture.Settings.Companion=$false
  $fixture.Settings.AutoMove=$false
  $fixture.Settings.Hungry=$scenario -like 'hungry*'
  $fixture.Settings.HungerCenterAttempted=$scenario -like 'hungry*'
  if($scenario -eq 'hungry-hidden'){$fixture.Settings.Hidden=$true}
  if($scenario -like 'hungry*'){
    $fixture.PetLps=$fixture.PetLps -replace 'strengthFood#[^:]*:', 'strengthFood#10000000000:'
    $fixture.SavedUtc=[DateTimeOffset]::UtcNow.ToString('o')
  }
  $fixture.Settings.StaticCompanion=$true
  $fixture.Settings.IdlePose=if($scenario -eq 'pose'){'Lie'}else{'FrontSit'}
  $fixture.Settings.AfkSeconds=3600
  $fixture | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $data 'state.json')
  $process=Start-Process -FilePath $exe -ArgumentList @('--state-probe','--data-dir',('"'+$data+'"'),'--output',('"'+$output+'"')) -PassThru -Wait -WindowStyle Hidden
  $probe=Get-Content -Raw (Join-Path $output 'state-probe.json') | ConvertFrom-Json
  $passed=$process.ExitCode -eq 0 -and $probe.prepared
  if($scenario -eq 'sleep'){$passed=$passed -and $probe.sleeping -and $probe.behavior -eq 'Sleep' -and $probe.visible}
  if($scenario -eq 'hidden'){$passed=$passed -and $probe.hidden -and -not $probe.visible}
  if($scenario -eq 'paused'){$passed=$passed -and $probe.paused -and $probe.behavior -eq 'Idle'}
  if($scenario -eq 'transient'){$passed=$passed -and $probe.behavior -eq 'Idle' -and $probe.graph -eq 'pose-FrontSit' -and -not $probe.hidden}
  if($scenario -eq 'pose'){$passed=$passed -and $probe.behavior -eq 'Idle' -and $probe.graph -eq 'pose-Lie' -and $probe.idlePose -eq 'Lie'}
  if($scenario -eq 'hungry'){$passed=$passed -and $probe.hungry -and $probe.centerAttempted -and $probe.bubble -and $probe.behavior -eq 'Idle'}
  if($scenario -eq 'hungry-hidden'){$passed=$passed -and $probe.hungry -and $probe.centerAttempted -and -not $probe.bubble -and -not $probe.visible}
  $results+=@{scenario=$scenario;passed=$passed;probe=$probe}
  if(-not $passed){throw ('重启恢复失败：'+$scenario)}
}
$results | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 verification/restart-results.json
