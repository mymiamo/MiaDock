[CmdletBinding()]
param(
    [version]$LocalVersion,
    [string]$MSBuildPath = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
[xml]$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot 'src/MiaDock.App/Package.appxmanifest') -Raw
$identity = $manifest.Package.Identity
$installed = Get-AppxPackage -Name $identity.Name | Sort-Object { [version]$_.Version } -Descending | Select-Object -First 1
$minimumVersion = [version]$identity.Version
if ($installed -and [version]$installed.Version -gt $minimumVersion) { $minimumVersion = [version]$installed.Version }
if (-not $LocalVersion) {
    if ($minimumVersion.Revision -ge 65535) { throw 'Specify a higher LocalVersion explicitly.' }
    $LocalVersion = [version]::new($minimumVersion.Major, $minimumVersion.Minor, $minimumVersion.Build, $minimumVersion.Revision + 1)
}
if ($LocalVersion -le $minimumVersion) { throw "LocalVersion must be higher than $minimumVersion." }
if (-not (Test-Path -LiteralPath $MSBuildPath)) { throw 'Visual Studio MSBuild was not found.' }

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputDirectory = Join-Path $repositoryRoot "artifacts/installers/$LocalVersion/$timestamp"
$workDirectory = Join-Path $repositoryRoot "artifacts/local-package-work/$LocalVersion/$timestamp"
New-Item -ItemType Directory -Path $outputDirectory, $workDirectory | Out-Null
$identity.SetAttribute('Version', $LocalVersion.ToString())
$localManifest = Join-Path $workDirectory 'Package.local.appxmanifest'
$manifest.Save($localManifest)

# The private key remains non-exportable in the current user's personal store.
# Creating/signing a package does not install it or change certificate trust.
$certificate = Get-ChildItem Cert:/CurrentUser/My -CodeSigningCert |
    Where-Object { $_.Subject -eq $identity.Publisher -and $_.HasPrivateKey -and
        $_.FriendlyName -eq 'MiaDock local installation signing' -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $certificate) {
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $identity.Publisher `
        -FriendlyName 'MiaDock local installation signing' -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA -KeyLength 2048 -KeyUsage DigitalSignature -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(1)
}

$buildLog = Join-Path $workDirectory 'build.log'
$arguments = @(
    (Join-Path $repositoryRoot 'src/MiaDock.App/MiaDock.App.csproj'), '/restore', '/t:Build', '/nologo', '/verbosity:minimal',
    '/p:Configuration=Release', '/p:Platform=x64', '/p:BuildMsix=true', '/p:WindowsPackageType=MSIX',
    # WindowsAppSDKSelfContained is already enabled on the executable project;
    # applying it globally would also enable it on UI/platform class libraries.
    '/p:SelfContained=true',
    '/p:UapAppxPackageBuildMode=SideloadOnly', '/p:AppxBundle=Never',
    '/p:AppxAutoIncrementPackageRevision=false', '/p:AppxSymbolPackageEnabled=false',
    '/p:AppxPackageSigningEnabled=true', "/p:PackageCertificateThumbprint=$($certificate.Thumbprint)",
    "/p:Version=$LocalVersion", "/p:AssemblyVersion=$LocalVersion", "/p:FileVersion=$LocalVersion", "/p:InformationalVersion=$LocalVersion",
    ('/p:OutputPath=' + (Join-Path $workDirectory 'bin/')),
    "/p:AppxPackageDir=$outputDirectory/", "/p:MiaDockLocalManifest=$localManifest",
    ('/p:CustomAfterMicrosoftCommonTargets=' + (Join-Path $PSScriptRoot 'MiaDock.LocalPackage.targets'))
)
Push-Location $repositoryRoot
try {
    & $MSBuildPath @arguments *> $buildLog
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $buildLog -Tail 35; throw "Packaging failed. Log: $buildLog" }
} finally { Pop-Location }

$package = Get-ChildItem -LiteralPath $outputDirectory -Recurse -Filter '*.msix' -File | Select-Object -First 1
if (-not $package) { throw "No MSIX produced. Log: $buildLog" }
$certificatePath = [IO.Path]::ChangeExtension($package.FullName, '.cer')
Export-Certificate -Cert $certificate -FilePath $certificatePath -Force | Out-Null
$signature = Get-AuthenticodeSignature -LiteralPath $package.FullName
if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'The packaged signing certificate does not match.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $reader = [IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
    try { [xml]$packagedManifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($packagedManifest.Package.Identity.Version -ne $LocalVersion.ToString()) { throw 'Incorrect packaged version.' }
    if (-not $archive.GetEntry('AppxSignature.p7x')) { throw 'Missing package signature.' }
    foreach ($entry in @('MiaDock.App.exe', 'MiaDock.UI.dll', 'coreclr.dll', 'Assets/Ionicons/LICENSE')) {
        if (-not $archive.GetEntry($entry)) { throw "Package payload is incomplete: $entry" }
    }
} finally { $archive.Dispose() }

$summary = [ordered]@{
    Version = $LocalVersion.ToString(); Package = $package.FullName; Certificate = $certificatePath
    CertificateThumbprint = $certificate.Thumbprint; SignatureTrustStatus = $signature.Status.ToString()
    SHA256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
    GitCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim(); BuildLog = $buildLog
    Installed = $false; CertificateTrustChanged = $false
}
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDirectory 'package-summary.json') -Encoding UTF8
@"
MiaDock $LocalVersion - yerel test kurulumu

Bu paket Microsoft Store yayini degildir. Imzasi yerel test sertifikasiyla atildi.
Kurulum veya sertifika guveni bu betik tarafindan degistirilmedi.

1. Calisan MiaDock surumlerini tepsi menusunden tamamen kapatin.
2. Ilk yerel kurulumda bu klasordeki .cer dosyasini acin. Sertifikayi Yukle >
   Yerel Makine > Tum sertifikalari asagidaki depolama alanina yerlestir >
   Guvenilen Kisiler yolunu secin. Sertifika bilgilerini onaylayin.
3. Bu klasordeki .msix dosyasini acip Guncellestir/Yukle secin.

Alternatif olarak SDK'nin Add-AppDevPackage.ps1 dosyasini PowerShell ile
calistirabilirsiniz; Windows gereken sertifika/kurulum onaylarini isteyecektir.
Windows'un genel guvenlik ayarlarini veya betik calistirma ilkesini degistirmeyin.

Paket mevcut MiaDock kimligini kullanir; ayarlar sifirlanmaz.
Dosyanin SHA-256 ve sertifika bilgileri ust klasordeki package-summary.json icindedir.
"@ | Set-Content -LiteralPath (Join-Path $package.DirectoryName 'KURULUM.txt') -Encoding UTF8
Write-Output "Package: $($package.FullName)"
Write-Output "Certificate: $certificatePath"
Write-Output "Summary: $(Join-Path $outputDirectory 'package-summary.json')"
