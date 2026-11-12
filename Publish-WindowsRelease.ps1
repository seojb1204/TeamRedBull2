# 팀원 공통 Windows 게임 사전배포 도구.
# 기본 실행은 ZIP/체크섬만 생성합니다. -Publish 지정 시 GitHub 사전배포가 공개됩니다.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?$')]
    [string]$Tag,
    [switch]$Publish,
    [string]$UnityPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$repoName = 'seojb1204/TeamRedBull2'
$requiredVersion = '6000.4.0f1'

function Invoke-RepoGit {
    param([string[]]$Arguments)
    $output = & git -C $repoRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('Git 명령 실패: git ' + ($Arguments -join ' '))
    }
    return ($output | Out-String).Trim()
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git 설치가 필요합니다.'
}
$remote = Invoke-RepoGit -Arguments @('remote', 'get-url', 'origin')
if ($remote -notmatch 'github\.com[:/]seojb1204/TeamRedBull2(\.git)?$') {
    throw ('이 저장소의 origin이 TeamRedBull2가 아닙니다: ' + $remote)
}
$branch = Invoke-RepoGit -Arguments @('branch', '--show-current')
if ($branch -ne 'main') {
    throw '배포는 main에서만 가능합니다. 먼저 PR을 병합하고 최신 main을 체크아웃하세요.'
}
if (Invoke-RepoGit -Arguments @('status', '--porcelain')) {
    throw '작업 폴더에 커밋되지 않은 변경이 있습니다. 별도 보존하거나 커밋한 뒤 진행하세요.'
}
[void](Invoke-RepoGit -Arguments @('fetch', 'origin', 'main'))
$head = Invoke-RepoGit -Arguments @('rev-parse', 'HEAD')
$remoteMain = Invoke-RepoGit -Arguments @('rev-parse', 'refs/remotes/origin/main')
if ($head -ne $remoteMain) {
    throw '로컬 main이 origin/main과 일치하지 않습니다. git pull --ff-only 후 진행하세요.'
}

if (-not $UnityPath) {
    $UnityPath = Join-Path $env:ProgramFiles ('Unity\Hub\Editor\' + $requiredVersion + '\Editor\Unity.exe')
}
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw ('Unity ' + $requiredVersion + ' + Windows Build Support가 필요합니다: ' + $UnityPath)
}
# 다른 Unity 버전 자동 선택을 막습니다.
if ($UnityPath -notmatch [regex]::Escape($requiredVersion)) {
    throw ('Unity 경로에 기대 버전 ' + $requiredVersion + '이 확인되지 않습니다: ' + $UnityPath)
}

$buildStart = (Get-Date).ToUniversalTime()
& (Join-Path $repoRoot 'Build-Windows.ps1') -UnityPath $UnityPath

$windowsDir = Join-Path (Split-Path $repoRoot -Parent) 'Windows'
$exe = Join-Path $windowsDir 'ReversalTower.exe'
$dataDir = Join-Path $windowsDir 'ReversalTower_Data'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or
    -not (Test-Path -LiteralPath $dataDir -PathType Container)) {
    throw '빌드 출력에 ReversalTower.exe 또는 ReversalTower_Data가 없습니다.'
}
if ((Get-Item -LiteralPath $exe).LastWriteTimeUtc -lt $buildStart.AddSeconds(-5)) {
    throw '이번 실행에서 새로 빌드된 exe를 확인하지 못했습니다.'
}
if (Invoke-RepoGit -Arguments @('status', '--porcelain')) {
    throw 'Unity 빌드가 소스·설정 파일을 변경했습니다. 배포 전에 변경을 확인하세요.'
}

# 사전에 지정한 게임 실행 구성요소만 ZIP에 포함합니다.
$includeNames = @(
    'ReversalTower.exe', 'ReversalTower_Data',
    'UnityPlayer.dll', 'MonoBleedingEdge',
    'UnityCrashHandler64.exe', 'WinPixEventRuntime.dll', 'GameAssembly.dll'
)
$includes = @(
    foreach ($name in $includeNames) {
        $path = Join-Path $windowsDir $name
        if (Test-Path -LiteralPath $path) { $path }
    }
)
$buildsDir = Join-Path $repoRoot 'Builds'
New-Item -ItemType Directory -Path $buildsDir -Force | Out-Null
$zipName = 'ReversalTower-' + $Tag + '-Windows-x64.zip'
$zip = Join-Path $buildsDir $zipName
$checksumPath = $zip + '.sha256'
if ((Test-Path -LiteralPath $zip) -or (Test-Path -LiteralPath $checksumPath)) {
    throw '같은 버전의 빌드 파일이 이미 있습니다. 덮어쓰지 않습니다. 버전을 확인하세요.'
}
Compress-Archive -LiteralPath $includes -DestinationPath $zip -CompressionLevel Optimal
if (-not (Test-Path -LiteralPath $zip -PathType Leaf)) {
    throw 'ZIP이 생성되지 않았습니다.'
}
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
($hash + '  ' + $zipName) | Set-Content -LiteralPath $checksumPath -Encoding Ascii
Write-Host ('빌드 ZIP: ' + $zip)
Write-Host ('SHA-256: ' + $hash)
Write-Host ('기준 커밋: ' + $head)

if (-not $Publish) {
    Write-Host 'Release는 게시하지 않았습니다. 검증 후 생성된 ZIP을 GitHub Release에서 직접 게시하거나, 새 버전 태그로 -Publish를 실행하세요.'
    return
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw '게시하려면 GitHub CLI(gh)가 필요합니다. 설치 후 gh auth login을 실행하세요.'
}
& gh auth status *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub CLI에 로그인되어 있지 않습니다. gh auth login을 실행하세요.'
}
$releaseNotes = "Unity $requiredVersion / Windows x64 / Git commit $head. 시험판(Pre-release)입니다. 실제 플레이 및 DB 통신은 별도 확인하세요."
& gh release create $Tag $zip $checksumPath --repo $repoName --target $head --title ("Reversal Tower " + $Tag + " (Windows x64)") --notes $releaseNotes --prerelease --latest=false
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub Release 게시 실패. GitHub에서 부분 생성된 Release가 없는지 확인하세요.'
}
Write-Host ('GitHub 사전배포 게시 완료: https://github.com/' + $repoName + '/releases/tag/' + $Tag)
