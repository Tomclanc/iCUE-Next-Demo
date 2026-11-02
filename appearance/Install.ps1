param([switch]$Elevated,[switch]$Restore)
$ErrorActionPreference='Stop'
$base='C:\Program Files\Corsair\Corsair iCUE5 Software'
$module=Join-Path $base 'imports\CueAppearance'
$patches=Get-Content (Join-Path $PSScriptRoot 'patches.json') -Raw | ConvertFrom-Json
$stage=Join-Path $PSScriptRoot '.prepared'
$backup=Join-Path $PSScriptRoot '.backup'
$helper=Join-Path $env:LOCALAPPDATA 'Corsair\iCUEAppearancePatch\TrayCorners.exe'
$link=Join-Path ([Environment]::GetFolderPath('Startup')) 'iCUE Tray Corners.lnk'
if(-not $Elevated){
 if(-not $Restore){
  & python (Join-Path $PSScriptRoot 'prepare.py')
  if($LASTEXITCODE -ne 0){throw 'Preparation failed; no installed files changed.'}
  $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  & $compiler /nologo /target:winexe /optimize+ /reference:System.Windows.Forms.dll ('/out:'+(Join-Path $stage 'TrayCorners.exe')) (Join-Path $PSScriptRoot 'TrayCorners.cs')
  if($LASTEXITCODE -ne 0){throw 'Helper compilation failed.'}
  $test=Start-Process (Join-Path $stage 'TrayCorners.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
  if($test.ExitCode -ne 0){throw 'Native corner test failed.'}
 }
 $arguments='-NoProfile -File "'+$PSCommandPath+'" -Elevated'
 if($Restore){$arguments+=' -Restore'}
 $p=Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList $arguments -PassThru
 $p.WaitForExit()
 Start-Process (Join-Path $base 'iCUE.exe') -WorkingDirectory $base -WindowStyle Normal
 if($p.ExitCode -ne 0){throw 'Install failed; check result.txt. iCUE has been restarted.'}
 Get-Process TrayCorners -ErrorAction SilentlyContinue | Where-Object Path -eq $helper | ForEach-Object {Stop-Process -Id $_.Id; $_.WaitForExit()}
 if($Restore){
  if(Test-Path -LiteralPath $link){Remove-Item -LiteralPath $link}
 }else{
  New-Item -ItemType Directory -Force (Split-Path $helper) | Out-Null
  Copy-Item -LiteralPath (Join-Path $stage 'TrayCorners.exe') -Destination $helper -Force
  $shell=New-Object -ComObject WScript.Shell
  $shortcut=$shell.CreateShortcut($link)
  $shortcut.TargetPath=$helper
  $shortcut.WorkingDirectory=Split-Path $helper
  $shortcut.WindowStyle=7
  $shortcut.Save()
  Start-Process $helper -WindowStyle Hidden
 }
 exit
}
$changed=$false
try{
 # Check all inputs before stopping iCUE or changing any installed DLL.
 foreach($patch in $patches){
  $target=Join-Path (Join-Path $base 'cue_qml_plugins') $patch.file
  $hash=(Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant()
  if($hash -notin @($patch.before,$patch.after)){throw 'Installed iCUE version is unsupported.'}
  if(-not $Restore -and (Get-FileHash -LiteralPath (Join-Path $stage $patch.file)).Hash.ToLowerInvariant() -ne $patch.after){throw 'Staged patch checksum mismatch.'}
  if(-not (Test-Path -LiteralPath (Join-Path $backup $patch.file))){throw 'Original backup missing.'}
  $backupHash=(Get-FileHash -LiteralPath (Join-Path $backup $patch.file)).Hash.ToLowerInvariant()
  if($backupHash -notin @($patch.before,$patch.after)){throw 'Backup checksum mismatch.'}
 }
 if(-not (Test-Path -LiteralPath (Join-Path $backup 'qmldir'))){throw 'Module backup missing.'}
 Get-Process iCUE -ErrorAction SilentlyContinue | ForEach-Object {Stop-Process -Id $_.Id; $_.WaitForExit()}
 $source=if($Restore){$backup}else{$stage}
 $changed=$true
 foreach($patch in $patches){Copy-Item -LiteralPath (Join-Path $source $patch.file) -Destination (Join-Path (Join-Path $base 'cue_qml_plugins') $patch.file) -Force}
 if(-not $Restore){Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TrayTheme.qml') -Destination (Join-Path $module 'TrayTheme.qml') -Force}
 Copy-Item -LiteralPath (Join-Path $source 'qmldir') -Destination (Join-Path $module 'qmldir') -Force
 # Move affected QML caches aside; do not erase user settings or other caches.
 $cache=Join-Path $env:LOCALAPPDATA 'Corsair\CUE5\cache\qmlcache'
 $root=[IO.Path]::GetFullPath($cache).TrimEnd('\')+'\'
 $cacheBackup=Join-Path $backup 'qmlcache'
 New-Item -ItemType Directory -Force $cacheBackup | Out-Null
 Get-ChildItem -LiteralPath $cache -File -ErrorAction SilentlyContinue | ForEach-Object {
  $full=[IO.Path]::GetFullPath($_.FullName)
  if(-not $full.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected cache path.'}
  $bytes=[IO.File]::ReadAllBytes($full)
  $text=[Text.Encoding]::Unicode.GetString($bytes)
  $odd=if($bytes.Length -gt 1){[Text.Encoding]::Unicode.GetString($bytes,1,$bytes.Length-1)}else{''}
  if(($text+$odd) -match 'DeviceCard\.qml|Tray(Menu|Profile|Battery).*\.qml'){
   Move-Item -LiteralPath $full -Destination (Join-Path $cacheBackup ($_.Name+'.'+[Guid]::NewGuid().ToString('N')))
  }
 }
 'OK' | Set-Content (Join-Path $PSScriptRoot 'result.txt')
}catch{
 if($changed){
 foreach($patch in $patches){try{Copy-Item -LiteralPath (Join-Path $backup $patch.file) -Destination (Join-Path (Join-Path $base 'cue_qml_plugins') $patch.file) -Force}catch{}}
 try{Copy-Item -LiteralPath (Join-Path $backup 'qmldir') -Destination (Join-Path $module 'qmldir') -Force}catch{}
 }
 $_ | Out-String | Set-Content (Join-Path $PSScriptRoot 'result.txt')
 exit 1
}
