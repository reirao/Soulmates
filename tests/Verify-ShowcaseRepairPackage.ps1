param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$Package,
    [string]$Output
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
if (!$Package) { $Package = Join-Path $Root 'outputs\showcase-repairs-0.22.1\Soulmates.tmod' }
if (!$Output) { $Output = Join-Path $Root 'outputs\showcase-repairs-0.22.1\package-verification.json' }
$reader = [IO.BinaryReader]::new([IO.File]::OpenRead($Package))
$sources = [Collections.Generic.List[object]]::new()
try {
    # Read the native TMOD header/table; offsets are relative to the end of that table.
    $magic = [Text.Encoding]::ASCII.GetString($reader.ReadBytes(4))
    if ($magic -ne 'TMOD') { throw 'Invalid package magic' }
    $loader = $reader.ReadString()
    $expectedHash = $reader.ReadBytes(20)
    $null = $reader.ReadBytes(256)
    $payloadLength = $reader.ReadInt32()
    $hashStart = $reader.BaseStream.Position
    if ($payloadLength -ne $reader.BaseStream.Length - $hashStart) { throw 'Invalid package length' }
    $sha = [Security.Cryptography.SHA1]::Create()
    try { $hash = $sha.ComputeHash($reader.BaseStream) } finally { $sha.Dispose() }
    if ([Convert]::ToHexString($hash) -ne [Convert]::ToHexString($expectedHash)) { throw 'Native payload hash mismatch' }
    $reader.BaseStream.Position = $hashStart
    $name = $reader.ReadString()
    $version = $reader.ReadString()
    if ($name -ne 'Soulmates' -or $version -ne '0.22.1') { throw "Unexpected package $name $version" }
    $count = $reader.ReadInt32()
    if ($count -lt 1 -or $count -gt 10000) { throw 'Invalid entry count' }
    $offset = 0L
    $entries = for ($i = 0; $i -lt $count; $i++) {
        $entryName = $reader.ReadString()
        $size = $reader.ReadInt32()
        $compressed = $reader.ReadInt32()
        if ($size -lt 0 -or $compressed -lt 0 -or $size -gt 100MB -or $compressed -gt 100MB) { throw 'Invalid entry size' }
        [pscustomobject]@{ name = $entryName; size = $size; compressed = $compressed; offset = $offset }
        $offset += $compressed
    }
    $dataStart = $reader.BaseStream.Position
    if ($dataStart + $offset -ne $reader.BaseStream.Length) { throw 'Invalid file-table offsets' }
    foreach ($entry in $entries) {
        if ($entry.name -match '^(tests|work|outputs|bin|obj|media|releases)[/\\]' -or $entry.name -match 'RegressionProbe|AuditProbe') {
            throw "Private/test content packaged: $($entry.name)"
        }
        if (!$entry.name.EndsWith('.cs', [StringComparison]::Ordinal)) { continue }
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $Root $entry.name))
        if (!$sourcePath.StartsWith($Root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Source path escaped repository'
        }
        $reader.BaseStream.Position = $dataStart + $entry.offset
        $packed = $reader.ReadBytes($entry.compressed)
        if ($entry.compressed -ne $entry.size) {
            $input = [IO.MemoryStream]::new($packed, $false)
            $decoded = [IO.MemoryStream]::new()
            $deflate = [IO.Compression.DeflateStream]::new($input, [IO.Compression.CompressionMode]::Decompress)
            try { $deflate.CopyTo($decoded); $bytes = $decoded.ToArray() }
            finally { $deflate.Dispose(); $decoded.Dispose(); $input.Dispose() }
        }
        else { $bytes = $packed }
        if ($bytes.Length -ne $entry.size) { throw "Invalid decoded size: $($entry.name)" }
        $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
        $packedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        if ($sourceHash -ne $packedHash) { throw "Source/package drift: $($entry.name)" }
        $sources.Add([pscustomobject]@{ path = $entry.name; sha256 = $packedHash; bytes = $bytes.Length })
    }
    $expectedSources = @((Get-ChildItem -LiteralPath (Join-Path $Root 'Common'), (Join-Path $Root 'Content') -Recurse -File -Filter '*.cs')) +
        @(Get-Item -LiteralPath (Join-Path $Root 'Soulmates.cs'))
    if ($sources.Count -ne $expectedSources.Count) { throw 'Missing production source entries' }
    $descriptions = foreach ($file in @('description_workshop.txt', 'description_workshop_de.txt')) {
        $bytes = [Text.Encoding]::UTF8.GetByteCount([IO.File]::ReadAllText((Join-Path $Root $file)))
        if ($bytes -gt 8000) { throw "Workshop byte limit: $file" }
        [pscustomobject]@{ path = $file; utf8Bytes = $bytes }
    }
    $result = [ordered]@{
        version = $version; internalName = $name; loader = $loader
        package = [IO.Path]::GetFullPath($Package)
        bytes = (Get-Item -LiteralPath $Package).Length
        sha256 = (Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash
        nativePayloadHashValid = $true; entryCount = $count
        excludedPrivateAndTestContent = $true; sourceCount = $sources.Count
        allPackagedSourcesMatchRepository = $true; sources = $sources
        workshopDescriptions = @($descriptions)
        scope = 'Package integrity only; client installation and public delivery are recorded separately.'
    }
    [IO.File]::WriteAllText($Output, ($result | ConvertTo-Json -Depth 5) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    "Verified $name $version, $($sources.Count) source files, $count entries, SHA256 $($result.sha256)."
}
finally { $reader.Dispose() }
