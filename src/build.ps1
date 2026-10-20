$ErrorActionPreference = 'Stop'
$sourceDir = $PSScriptRoot
$outputPath = Join-Path (Split-Path $sourceDir -Parent) 'iCUE-Replica.exe'
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
$buildArguments = @('/nologo','/target:winexe','/platform:x64','/reference:System.Xml.Linq.dll', "/reference:$frameworkPath\WPF\UIAutomationClient.dll", "/reference:$frameworkPath\WPF\UIAutomationTypes.dll", "/win32icon:$sourceDir\assets\icue-app.ico","/out:$outputPath", "/reference:$frameworkPath\WPF\PresentationFramework.dll", "/reference:$frameworkPath\WPF\PresentationCore.dll", "/reference:$frameworkPath\WPF\WindowsBase.dll", '/reference:System.Xaml.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', "/resource:$sourceDir\Styles.xaml,Styles.xaml")
Get-ChildItem -LiteralPath (Join-Path $sourceDir 'assets') -File | ForEach-Object { $buildArguments += ('/resource:' + $_.FullName + ',' + $_.Name) }
$buildArguments += @("$sourceDir\App.cs", "$sourceDir\Replica.cs", "$sourceDir\Details.cs", "$sourceDir\Advanced.cs", "$sourceDir\Mica.cs", "$sourceDir\OriginalIcons.cs", "$sourceDir\DesktopFeatures.cs", "$sourceDir\Telemetry.cs", "$sourceDir\OriginalTelemetry.cs", "$sourceDir\WindowLayout.cs", "$sourceDir\CardLayout.cs", "$sourceDir\OriginalProfile.cs", "$sourceDir\CoolingControl.cs", "$sourceDir\OriginalCoolingUi.cs", "$sourceDir\ScreenControl.cs", "$sourceDir\ScreenPreview.cs", "$sourceDir\ScreenEditor.cs", "$sourceDir\LightingWizard.cs")
& $compilerPath @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $sourceDir 'Apply-IcueCooling.ps1') -Destination (Join-Path (Split-Path $outputPath -Parent) 'Apply-IcueCooling.ps1') -Force
Write-Output "Built: $outputPath"


