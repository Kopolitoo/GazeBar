# Certificate step of the installer (Prunel.iss). Started hidden, as administrator, by the
# installer and by the uninstaller (they are administrator already).
#
#   -Step Sign   -Folder <installation folder>   after the files are copied: signs Prunel.exe
#                with a certificate made on this computer (UI access needs a signature that the
#                computer trusts, see Platform/UiAccess.cs).
#   -Step Remove -Folder <installation folder>   before the files are removed: removes the
#                certificate and the "Start with Windows" entry of this copy.
#
# Ends with exit code 0 when everything worked. Otherwise exit code 1, and the reason is written
# to -ErrorFile so that the installer can show it.
#
# Runs in Windows PowerShell 5.1, the version shipped with Windows 10 and 11. Saved in UTF-8
# WITH a BOM: without it, Windows PowerShell 5.1 would read the accented letters wrongly.

param(
    [Parameter(Mandatory = $true)][ValidateSet('Sign', 'Remove')][string]$Step,
    [Parameter(Mandatory = $true)][string]$Folder,
    [Parameter(Mandatory = $true)][string]$ErrorFile
)

$ErrorActionPreference = 'Stop'

# The name of the certificate created on this computer to sign Prunel.exe. Only the
# certificates with exactly this name are ever removed.
$CertificateSubject = 'CN=Prunel local signature'
# The name used before version 0.3.0, when Prunel was called GazeBar: removed the same way.
$FormerCertificateSubject = 'CN=GazeBar local signature'

# Removes the Prunel (and former GazeBar) certificates from every store of the computer where this script puts one.
function Remove-PrunelCertificates {
    foreach ($store in 'Root', 'TrustedPublisher', 'My') {
        Get-ChildItem "Cert:\LocalMachine\$store" |
            Where-Object { $_.Subject -eq $CertificateSubject -or $_.Subject -eq $FormerCertificateSubject } |
            ForEach-Object {
                # -DeleteKey: also deletes the private key, if one is left (store "My").
                if ($_.HasPrivateKey) { Remove-Item $_.PSPath -DeleteKey } else { Remove-Item $_.PSPath }
            }
    }
}

# Signs Prunel.exe:
#  - removes the old Prunel certificates (a new one is made at each installation);
#  - creates a certificate ON THIS COMPUTER and makes the computer trust it;
#  - signs the program with it;
#  - DELETES its private key: nobody (not even a virus) can sign anything else with it later.
function Set-PrunelSignature([string]$exe) {
    Remove-PrunelCertificates
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $CertificateSubject `
        -CertStoreLocation 'Cert:\LocalMachine\My' -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(10)

    # The computer trusts this certificate: "Trusted Root" (it is its own authority) and
    # "Trusted Publishers". Only the public part goes there (a .cer file, without the key).
    $publicFile = Join-Path $env:TEMP 'Prunel-signature.cer'
    Export-Certificate -Cert $certificate -FilePath $publicFile | Out-Null
    Import-Certificate -FilePath $publicFile -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Import-Certificate -FilePath $publicFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
    Remove-Item $publicFile -Force

    $signature = Set-AuthenticodeSignature -FilePath $exe -Certificate $certificate -HashAlgorithm SHA256

    # The private key is deleted: the certificate can no longer sign anything.
    Remove-Item $certificate.PSPath -DeleteKey

    if ($signature.Status -ne 'Valid') {
        throw "Signature: $($signature.Status) $($signature.StatusMessage)"
    }
}

# "Start with Windows" (Platform/WindowsStartup.cs): removes its entry if it starts the copy of
# Prunel installed in $Folder.
function Remove-PrunelStartup {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $command = (Get-ItemProperty -Path $runKey -Name 'Prunel' -ErrorAction SilentlyContinue).Prunel
    if ($command -and $command -like "*$Folder*") {
        Remove-ItemProperty -Path $runKey -Name 'Prunel'
    }
}

try {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This script must run as administrator (it is started by the Prunel installer).'
    }
    if ($Step -eq 'Sign') {
        Set-PrunelSignature (Join-Path $Folder 'Prunel.exe')
    } else {
        Remove-PrunelCertificates
        Remove-PrunelStartup
    }
    exit 0
}
catch {
    # UTF-8 with a BOM: the installer reads the accented letters correctly.
    $_.Exception.Message | Out-File -FilePath $ErrorFile -Encoding utf8
    exit 1
}
