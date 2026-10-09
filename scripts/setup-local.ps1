$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
function Run-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet adimi basarisiz: $args" }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 9 SDK kurun ve terminali yeniden acin.' }
$sdks = (& dotnet --list-sdks) -join "`n"
if ($sdks -notmatch '(?m)^9\.') { throw '.NET 9 SDK bulunamadi.' }
Run-Dotnet restore
Run-Dotnet tool restore
Run-Dotnet dev-certs https --trust
Run-Dotnet user-secrets set 'Demo:Seed' 'true' --project src/TrafficFine.Web
$securePassword = Read-Host 'Yerel demo sifresi (en az 10 karakter, buyuk/kucuk harf, rakam ve sembol)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ($password.Length -lt 10 -or $password -cnotmatch '[A-Z]' -or $password -cnotmatch '[a-z]' -or $password -notmatch '[0-9]' -or $password -notmatch '[^a-zA-Z0-9]') {
        throw 'Sifre gerekli kosullari karsilamiyor.'
    }
    # Avoid echoing the secret in user-secrets output.
    & dotnet user-secrets set 'Demo:Password' $password --project src/TrafficFine.Web | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Demo sifresi kaydedilemedi.' }
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $password = $null
}
Run-Dotnet ef database update --project src/TrafficFine.Web
Write-Host 'Kurulum hazir. Baslatmak icin: dotnet run --project src/TrafficFine.Web'
