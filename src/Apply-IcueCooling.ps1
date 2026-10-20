param([Parameter(Mandatory=$true)][string]$RequestPath,[switch]$Apply)
$ErrorActionPreference='Stop'
$payload=Get-Content -LiteralPath $RequestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$requests=@(if($null -ne $payload.PSObject.Properties['requests']){$payload.requests}else{$payload})
if($requests.Count -lt 1 -or $requests.Count -gt 2){throw 'Expected one or two fans'}
foreach($request in $requests){
if($request.fan -notin @(5,6)){throw 'Only fan 5 and 6 are supported'}
if($request.edited -eq $false -and $request.originalId){continue}
$points=@($request.points)
if($points.Count -lt 2 -or $points.Count -gt 7){throw 'Expected 2 to 7 curve points'}
$previous=-1
foreach($point in $points){if($point.temperature -lt 20 -or $point.temperature -gt 100 -or $point.temperature -le $previous -or $point.duty -lt 20 -or $point.duty -gt 100){throw 'Invalid curve: 20-100 C, strictly increasing, 20-100 percent'};$previous=$point.temperature}
}
$base=Join-Path $env:APPDATA 'Corsair\CUE5'
$libraryPath=Join-Path $base 'cooling\configurations'
$profiles=@(Get-ChildItem -LiteralPath (Join-Path $base 'profiles') -Filter '*.cueprofiledata')
if($profiles.Count -ne 1){throw 'Active profile is ambiguous'}
$profilePath=$profiles[0].FullName
$serial='430a201134895aaab9f09f061091005f'
$icue='C:\Program Files\Corsair\Corsair iCUE5 Software\iCUE.exe'
$stage=Join-Path (Split-Path -Parent $RequestPath) ('cooling-transaction-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$stopped=$false;$written=$false
try {
 if($Apply){Get-Process iCUE -ErrorAction SilentlyContinue | Where-Object Path -eq $icue | ForEach-Object {Stop-Process -Id $_.Id; $_.WaitForExit(5000)|Out-Null};$stopped=$true}
 Copy-Item -LiteralPath $libraryPath -Destination (Join-Path $stage 'library.before')
 Copy-Item -LiteralPath $profilePath -Destination (Join-Path $stage 'profile.before')
 $library=New-Object System.Xml.XmlDocument;$library.PreserveWhitespace=$true;$library.Load($libraryPath)
 $profile=New-Object System.Xml.XmlDocument;$profile.PreserveWhitespace=$true;$profile.Load($profilePath)
 $storage=$library.SelectNodes('//storages/*') | Where-Object { $_.key -eq $serial }
 $configs=$storage.SelectSingleNode('value/software/ptr_wrapper/data/base/configurations')
 if($null -eq $configs){throw 'Missing software cooling library'}
 foreach($request in $requests){
 $points=@($request.points)
 $name=if($request.name){[string]$request.name}else{'副本 · 风扇 #'+$request.fan}
 $existing=@($configs.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and (($request.originalId -and $_.SelectSingleNode('ptr_wrapper/data/base/id').InnerText -eq $request.originalId) -or (!$request.originalId -and $_.SelectSingleNode('ptr_wrapper/data/base/name').InnerText -eq $name)) })
 if($existing.Count -gt 1){throw 'Ambiguous cooling preset'}
 if($request.edited -eq $false -and $request.originalId -and $existing.Count -ne 1){throw 'Original preset no longer exists; refresh presets'}
 $reuse=$existing.Count -eq 1 -and $request.edited -eq $false
 if($reuse){$guid=$existing[0].SelectSingleNode('ptr_wrapper/data/base/id').InnerText}else{
 if($existing.Count -eq 1 -and $existing[0].SelectSingleNode('ptr_wrapper/data/base/predefined').InnerText -eq 'true'){throw 'Create a copy before editing a factory preset'}
 $template=$configs.SelectNodes('*/ptr_wrapper/data') | Where-Object { $_.base.predefined -eq 'false' -and $_.SelectNodes('customModePoints/*').Count -ge 2 } | Select-Object -First 1
 if($null -eq $template){throw 'No verified custom curve template'}
 $entry=if($existing.Count -eq 1){$existing[0].CloneNode($true)}else{$template.ParentNode.ParentNode.CloneNode($true)}
 $data=$entry.SelectSingleNode('ptr_wrapper/data')
 $guid=if($existing.Count -eq 1){$existing[0].SelectSingleNode('ptr_wrapper/data/base/id').InnerText}else{'{'+[guid]::NewGuid().ToString()+'}'}
 $data.SelectSingleNode('base/id').InnerText=$guid
 $data.SelectSingleNode('base/name').InnerText=$name
 $data.SelectSingleNode('base/predefined').InnerText='false'
 $data.SelectSingleNode('base/coolingMode').InnerText='Normal'
 $data.SelectSingleNode('fanMode').InnerText='Custom'
 if($request.mode -eq 'rpm'){if($request.rpm -lt 500 -or $request.rpm -gt 3000){throw 'RPM must be 500-3000'};$data.SelectSingleNode('fanMode').InnerText='FixedRpm';$data.SelectSingleNode('fixedRpm').InnerText=[string][int]$request.rpm}
 $data.SelectSingleNode('sensorId').InnerText='vid<6940>pid<3100>serial<'+$serial+'>senstype<temperature>sensindex<0>'
 $list=$data.SelectSingleNode('customModePoints');$templatePoint=$list.SelectSingleNode('*').CloneNode($true)
 $list.RemoveAll();$list.SetAttribute('size','dynamic')
 $index=0
 foreach($point in $points){$p=$library.CreateElement('value'+$index++);foreach($child in $templatePoint.ChildNodes){[void]$p.AppendChild($child.CloneNode($true))};$p.SelectSingleNode('enabled').InnerText='true';$p.SelectSingleNode('sensorValue').InnerText=[string][int]$point.temperature;$p.SelectSingleNode('rpmPercents').InnerText=[string][int]$point.duty;[void]$list.AppendChild($p)}
 $ids=@($library.SelectNodes('//ptr_wrapper/id') | ForEach-Object {[uint32]$_.InnerText -band 2147483647})
 $entry.SelectSingleNode('ptr_wrapper/id').InnerText=[string]([uint64](($ids | Measure-Object -Maximum).Maximum+1)+2147483648)
 foreach($old in $existing){[void]$configs.RemoveChild($old)}
 $suffix=0;while($configs.SelectSingleNode('value'+$suffix)){$suffix++};$next=$library.CreateElement('value'+$suffix)
 foreach($child in $entry.ChildNodes){[void]$next.AppendChild($child.CloneNode($true))};[void]$configs.AppendChild($next)
 }
 $fanKey='vid<6940>pid<3100>serial<'+$serial+'>senstype<fan>sensindex<'+($request.fan-1)+'>'
 $assignment=@($profile.SelectNodes('//assignments/*') | Where-Object { $_.key -eq $fanKey })
 if($assignment.Count -ne 1){throw 'Fan assignment not found'}
 $assignment[0].SelectSingleNode('value/assignmentConfigId').InnerText=$guid
 }
 $library.Save((Join-Path $stage 'library.after'));$profile.Save((Join-Path $stage 'profile.after'))
 if($Apply){$written=$true;Copy-Item (Join-Path $stage 'library.after') $libraryPath -Force;Copy-Item (Join-Path $stage 'profile.after') $profilePath -Force;Start-Process -FilePath $icue -WindowStyle Normal;$stopped=$false}
 [pscustomobject]@{applied=[bool]$Apply;backup=$stage;fan=$request.fan;name=$name;configurationId=$guid;requiresRestart=$true} | ConvertTo-Json
} catch {
 if($written){Copy-Item (Join-Path $stage 'library.before') $libraryPath -Force;Copy-Item (Join-Path $stage 'profile.before') $profilePath -Force}
 if($stopped){Start-Process -FilePath $icue -WindowStyle Normal}
 throw
}



