param([string]$UnityPath)
$ErrorActionPreference='Stop'
if (-not $UnityPath) {
    $preferred=Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'
    if(Test-Path -LiteralPath $preferred){$UnityPath=$preferred}else{
        $editors=Get-ChildItem -Path "$env:ProgramFiles\Unity\Hub\Editor\*\Editor\Unity.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
        if(-not $editors){throw 'Unity 6 compiler not found.'};$UnityPath=$editors[0].FullName
    }
}
$runtimeRoot=Join-Path (Split-Path $UnityPath -Parent) 'Data'
$frameworkRoot=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$previewRoot=Join-Path (Split-Path $PSScriptRoot -Parent) 'WindowsPreview'
New-Item -ItemType Directory -Force -Path $previewRoot | Out-Null
$compile=@('-nologo','-noconfig','-nostdlib+','-define:DESKTOP_PREVIEW','-target:winexe','-main:PreviewHost',('-out:'+(Join-Path $previewRoot 'ReversalTowerPreview.exe')))
foreach($assembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Drawing.dll','System.Windows.Forms.dll')){$compile+='-r:'+(Join-Path $frameworkRoot $assembly)}
foreach($assembly in @('PresentationCore','WindowsBase')) {
    $reference=foreach($gac in @('GAC_64','GAC_MSIL')) {Get-ChildItem -LiteralPath (Join-Path $env:WINDIR ('Microsoft.NET\assembly\'+$gac+'\'+$assembly)) -Filter ($assembly+'.dll') -Recurse -ErrorAction SilentlyContinue}
    if(-not $reference){throw ('Missing Windows desktop media library: '+$assembly)}
    $compile+='-r:'+($reference | Select-Object -First 1).FullName
}
foreach($source in (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Assets\Scripts') -Filter '*.cs' | Where-Object Name -ne 'Soundscape.cs')){$compile+=$source.FullName}
$compile+=(Join-Path $PSScriptRoot 'Tests\UnityShim.cs')
foreach($source in (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Preview') -Filter '*.cs')){$compile+=$source.FullName}
& (Join-Path $runtimeRoot 'NetCoreRuntime\dotnet.exe') (Join-Path $runtimeRoot 'DotNetSdkRoslyn\csc.dll') @compile
if($LASTEXITCODE -ne 0){throw 'Preview compilation failed.'}
$resourceRoot=Join-Path $PSScriptRoot 'Assets\StreamingAssets'
foreach($resource in (Get-ChildItem -LiteralPath $resourceRoot -File -Recurse)) {
    $relative=$resource.FullName.Substring($resourceRoot.Length).TrimStart('\')
    $target=Join-Path (Join-Path $previewRoot 'Assets') $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $resource.FullName -Destination $target -Force
}
Write-Host "Preview ready: $previewRoot"
