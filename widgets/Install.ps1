param([ValidateSet('Auto','Official','Migration')][string]$Edition='Auto')
$ErrorActionPreference='Stop'
if($Edition -eq 'Auto') {
    $Edition=if(Get-AppxPackage -Name CueReplica.WindowsWidgets){'Migration'}else{'Official'}
}
$name=if($Edition -eq 'Migration'){'CueReplica.WindowsWidgets'}else{'CueOfficial.WindowsWidgets'}
$file=Join-Path $PSScriptRoot ($name+'.msix')
$sig=Get-AuthenticodeSignature -LiteralPath $file
if($sig.SignerCertificate.Thumbprint -ne '5FC9E3DB5309FA67685840837E02DC65CC4821D1') {
    throw 'Unexpected package signing certificate. Do not install this file.'
}
if($sig.Status -ne 'Valid') {
    throw 'The package certificate is not trusted or its signature is invalid. Read README.md and verify the public certificate before manually trusting it. This script does not install certificates.'
}
Add-AppxPackage -Path $file
Write-Host 'Installed. Press Win+W, add the iCUE sensor widget. Keep official iCUE running.'
