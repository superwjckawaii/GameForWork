param([string]$RepositoryRoot=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
# Reference inventory is a navigation aid, never an assertion of behavioral completeness.
$root=Join-Path $RepositoryRoot 'src/Game.Core'
$catalog=Get-Content (Join-Path $root 'Equipment/Data/equipment_catalog.json') -Raw|ConvertFrom-Json
$paths=@(Get-ChildItem $root -Recurse -Filter '*.cs'|Where-Object {$_.FullName -notmatch '[\\/](obj|bin)[\\/]'})
$sources=@{}; foreach($path in $paths){$sources[[IO.Path]::GetRelativePath($RepositoryRoot,$path.FullName).Replace('\','/')]=Get-Content $path.FullName -Raw}
$references=@{}
foreach($entry in $sources.GetEnumerator()){
    if($entry.Key -match '(Catalog|Rebinder|Audit|Browser|LegendaryFactory|EquipmentRuleEngine|ItemDefinitions)\.cs$'){continue}
    foreach($match in [regex]::Matches($entry.Value,'ItemModifierKind\.(\w+)')){
        $name=$match.Groups[1].Value
        if(!$references.ContainsKey($name)){$references[$name]=[Collections.Generic.HashSet[string]]::new()}
        [void]$references[$name].Add($entry.Key)
    }
}
$json=Get-Content (Join-Path $root 'Equipment/Data/equipment_catalog.json') -Raw
$kinds=@([regex]::Matches($json,'"kind"\s*:\s*"(\w+)"')|ForEach-Object {$_.Groups[1].Value}|Sort-Object -Unique)
$enumBody=[regex]::Match((Get-Content (Join-Path $root 'Campaign/Items/ItemDefinitions.cs') -Raw),'(?s)public enum ItemModifierKind\s*\{(.*?)\}').Groups[1].Value
$kinds=@($kinds|Where-Object {$enumBody -match ('\b'+[regex]::Escape($_)+'\b')})
$output=Join-Path $RepositoryRoot 'artifacts/implementation-audit'
New-Item -ItemType Directory -Force $output|Out-Null
$kinds|ForEach-Object {
    $consumers=if($references.ContainsKey($_)){@($references[$_]|Sort-Object)}else{@()}
    [pscustomobject]@{kind=$_;directReferenceFiles=$consumers.Count;files=$consumers -join ';';review=if($consumers.Count){'Reference only; behavior requires review'}else{'No direct gameplay reference; investigate indirect use'}}
}|Export-Csv (Join-Path $output 'modifier-references.csv') -NoTypeInformation -Encoding utf8
$catalog.legendaryItems|ForEach-Object {
    $item=$_
    $consumers=@($sources.GetEnumerator()|Where-Object {$_.Key -notmatch '(Catalog|Rebinder|Audit|Browser|LegendaryFactory|EquipmentRuleEngine)\.cs$' -and
        ($_.Value.Contains($item.displayName) -or $_.Value.Contains($item.id))}|ForEach-Object {$_.Key}|Sort-Object)
    [pscustomobject]@{id=$item.id;name=$item.displayName;directReferenceFiles=$consumers.Count;files=$consumers -join ';';review='Reference inventory only; not a completed mechanic claim'}
}|Export-Csv (Join-Path $output 'legendary-references.csv') -NoTypeInformation -Encoding utf8
Write-Host "Implementation reference inventories: $output"
