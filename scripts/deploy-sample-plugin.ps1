$targetDir = 'c:\LogTailPro\src\LogTailPro\bin\Debug\net9.0-windows7.0\plugins\StreamServerDataflow'
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force }
Copy-Item 'c:\LogTailPro\examples\StreamServerDataflowExtension\bin\Debug\net9.0-windows7.0\StreamServerDataflowExtension.dll' -Destination $targetDir -Force
Copy-Item 'c:\LogTailPro\examples\StreamServerDataflowExtension\plugin.json' -Destination $targetDir -Force
Get-ChildItem $targetDir | Select-Object Name, Length
