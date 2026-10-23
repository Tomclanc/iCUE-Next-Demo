param([switch]$Migration)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    $dest=Join-Path $PSScriptRoot 'dist'
    dotnet publish .\CueWidgets.csproj -c Release -o $dest
    if($LASTEXITCODE){throw 'dotnet publish failed'}
    python -m pip install -r requirements.txt
    if($LASTEXITCODE){throw 'Python dependencies failed'}
    python -m PyInstaller --noconfirm --windowed --onedir --name CueSensorBridge --distpath bridge-dist --workpath bridge-build bridge.py
    if($LASTEXITCODE){throw 'Bridge build failed'}
    New-Item -ItemType Directory -Force (Join-Path $dest 'sensor-bridge'),(Join-Path $dest 'Public') | Out-Null
    Copy-Item -Path .\bridge-dist\CueSensorBridge\* -Destination (Join-Path $dest 'sensor-bridge') -Recurse -Force
    Copy-Item -LiteralPath .\bridge.py -Destination (Join-Path $dest 'sensor-bridge')
    Copy-Item -LiteralPath .\Assets -Destination $dest -Recurse -Force
    Copy-Item -LiteralPath .\licenses -Destination $dest -Recurse -Force
    $manifest=Get-Content -Raw -Encoding UTF8 $(if($Migration){'.\AppxManifest.Migration.xml'}else{'.\AppxManifest.xml'})
    if($Migration) {
        $source=Get-Content -Raw -Encoding UTF8 .\Program.cs
        $source=$source.Replace('BC51EA37-A46D-48D5-BD08-F5DA86371234','DD214B73-78F3-46DA-A121-813768E3AE44')
        $source=$source.Replace('User!.Value);','User!.Value,"MigratedCard");')
        # Build from a temporary project folder without changing the original source.
        $temp=Join-Path $PSScriptRoot 'migration-build'
        New-Item -ItemType Directory -Force $temp | Out-Null
        [IO.File]::WriteAllText((Join-Path $temp 'Program.cs'),$source)
        $project=(Get-Content -Raw .\CueWidgets.csproj).Replace('..\src\assets','..\..\src\assets')
        [IO.File]::WriteAllText((Join-Path $temp 'CueWidgets.csproj'),$project)
        dotnet publish (Join-Path $temp 'CueWidgets.csproj') -c Release -o $dest
        if($LASTEXITCODE){throw 'Migration publish failed'}
    }
    [IO.File]::WriteAllText((Join-Path $dest 'AppxManifest.xml'),$manifest)
    Write-Host 'Build ready in widgets/dist. Pack with Windows SDK MakeAppx and sign with your own certificate. See README.md.'
} finally {Pop-Location}
