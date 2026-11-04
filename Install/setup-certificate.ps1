# Certificate step of the Setup.exe installer (GazeBar.iss). Started hidden, as administrator,
# by the installer and by the uninstaller (they are administrator already).
#
#   -Step Sign   -Folder <installation folder>   after the files are copied: signs GazeBar.exe
#                with a certificate made on this computer (see Set-GazeBarSignature in common.ps1).
#   -Step Remove -Folder <installation folder>   before the files are removed: removes the
#                certificate and the "Start with Windows" entry of this copy.
#
# Ends with exit code 0 when everything worked. Otherwise exit code 1, and the reason is written
# to -ErrorFile so that the installer can show it.
#
# Saved in UTF-8 WITH a BOM (see common.ps1).

param(
    [Parameter(Mandatory = $true)][ValidateSet('Sign', 'Remove')][string]$Step,
    [Parameter(Mandatory = $true)][string]$Folder,
    [Parameter(Mandatory = $true)][string]$ErrorFile
)

. (Join-Path $PSScriptRoot 'common.ps1')

# The folder chosen by the installer (always Program Files\GazeBar), not the one of common.ps1.
$InstallFolder = $Folder

try {
    Assert-Administrator
    if ($Step -eq 'Sign') {
        Set-GazeBarSignature (Join-Path $InstallFolder 'GazeBar.exe')
    } else {
        Remove-GazeBarCertificates
        Remove-GazeBarStartup
    }
    exit 0
}
catch {
    # UTF-8 with a BOM: the installer reads the accented letters correctly.
    $_.Exception.Message | Out-File -FilePath $ErrorFile -Encoding utf8
    exit 1
}
