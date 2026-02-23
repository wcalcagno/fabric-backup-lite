param(
    [string]$PublishDir = "$PSScriptRoot\publish",
    [string]$OutFile    = "$PSScriptRoot\wix\Files.wxs"
)

$dir   = $PublishDir.TrimEnd('\')
$files = Get-ChildItem -Path $dir -Recurse -File
$plen  = $dir.Length + 1

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">')

foreach ($file in $files) {
    $rel    = $file.FullName.Substring($plen)
    $safeId = $rel -replace '[^a-zA-Z0-9]', '_'
    if ($safeId.Length -gt 70) { $safeId = $safeId.Substring(0, 70) }
    $relDir = Split-Path $rel -Parent

    if ([string]::IsNullOrEmpty($relDir)) {
        [void]$sb.AppendLine("      <Component Id='CMP_$safeId' Guid='*'>")
    } else {
        [void]$sb.AppendLine("      <Component Id='CMP_$safeId' Guid='*' Subdirectory='$relDir'>")
    }
    [void]$sb.AppendLine("        <File Source='$($file.FullName)' KeyPath='yes' />")
    [void]$sb.AppendLine("      </Component>")
}

[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')

[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "Done: $($files.Count) file components written to $OutFile"
