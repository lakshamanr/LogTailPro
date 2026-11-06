# Zips the LogTailPro source, excluding build output and VCS/IDE folders.
# Output: ..\LogTailPro-source.zip (next to the project folder, not inside it).
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root    = $PSScriptRoot
$zipPath = Join-Path (Split-Path $root -Parent) 'LogTailPro-source.zip'
$excluded = '\\(bin|obj|\.git|\.vs|publish)(\\|$)|\\installer\\out(\\|$)'

if (Test-Path $zipPath) { Remove-Item $zipPath }

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
try {
    Get-ChildItem $root -Recurse -File -Force |
        Where-Object { $_.FullName.Substring($root.Length) -notmatch $excluded } |
        ForEach-Object {
            $rel = 'LogTailPro' + $_.FullName.Substring($root.Length)
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $rel.Replace('\', '/'))
        }
}
finally { $zip.Dispose() }

'Created {0} ({1:N2} MB)' -f $zipPath, ((Get-Item $zipPath).Length / 1MB)
