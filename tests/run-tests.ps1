$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$core = Join-Path $root 'src/AccountCore.cs'
if (!(Test-Path -LiteralPath $core)) { throw 'FAIL: QLTK account reader and launch planner are not implemented.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
$output = Join-Path $PSScriptRoot 'AccountTests.exe'
& $compiler /nologo /target:exe "/out:$output" /r:System.Core.dll /r:System.Xml.Linq.dll $core (Join-Path $root 'src/CharacterSnapshot.cs') (Join-Path $root 'src/AccountHistory.cs') (Join-Path $PSScriptRoot 'AccountTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Account tests failed.' }
$autoTestOutput = Join-Path $PSScriptRoot 'AutoSettingsTests.exe'
& $compiler /nologo /target:exe "/out:$autoTestOutput" /r:System.Core.dll /r:System.Xml.Linq.dll (Join-Path $root 'src/AutoSettings.cs') (Join-Path $PSScriptRoot 'AutoSettingsTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Auto model test compilation failed.' }
& $autoTestOutput
if ($LASTEXITCODE -ne 0) { throw 'Auto model tests failed.' }
$uiOutput = Join-Path $PSScriptRoot 'UiTests.exe'
& $compiler /nologo /target:exe "/out:$(Join-Path $PSScriptRoot 'FakeJava.exe')" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'FakeJava.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI process fixture compilation failed.' }
& $compiler /nologo /target:exe "/out:$uiOutput" "/r:$(Join-Path $root 'QLTK_Accounts.exe')" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'UiTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI tests compilation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'QLTK_Accounts.exe') -Destination (Join-Path $PSScriptRoot 'QLTK_Accounts.exe') -Force
& $uiOutput $root
if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
$autoUiOutput = Join-Path $PSScriptRoot 'AutoUiTests.exe'
& $compiler /nologo /target:exe "/out:$autoUiOutput" "/r:$(Join-Path $root 'QLTK_Accounts.exe')" /r:System.Core.dll /r:System.Xml.Linq.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'AutoUiTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Auto UI tests compilation failed.' }
& $autoUiOutput $root
if ($LASTEXITCODE -ne 0) { throw 'Auto UI tests failed.' }
$windowOutput = Join-Path $PSScriptRoot 'WindowTests.exe'
& $compiler /nologo /target:exe "/out:$windowOutput" "/r:$(Join-Path $root 'QLTK_Accounts.exe')" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'WindowTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Window tests compilation failed.' }
& $windowOutput $root
if ($LASTEXITCODE -ne 0) { throw 'Window arrangement tests failed.' }
$javac = 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot/bin/javac.exe'
$testClasses = Join-Path $root 'build/test-classes'
New-Item -ItemType Directory -Path $testClasses -Force | Out-Null
$compileClasspath = (Join-Path $root 'account-bridge.jar') + ';' + (Join-Path $root 'MICRO_NST.jar')
& 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot/bin/java.exe' -cp (Join-Path $root 'build/compiler-helper') CompileJava $compileClasspath $testClasses (Join-Path $PSScriptRoot 'BridgeTests.java') (Join-Path $PSScriptRoot 'HeadlessSmoke.java')
if ($LASTEXITCODE -ne 0) { throw 'Bridge tests compilation failed.' }
$java = Join-Path $root 'jre/bin/java.exe'
$gamePath = ([xml](Get-Content -LiteralPath (Join-Path $root 'settings.xml') -Raw)).Settings.GamePath
& $java -cp ($testClasses + ';' + (Join-Path $root 'account-bridge.jar') + ';' + (Join-Path $root 'MICRO_NST.jar')) BridgeTests $gamePath
if ($LASTEXITCODE -ne 0) { throw 'Bridge tests failed.' }
$fixtureClasses = Join-Path $root 'build/fixture-classes'
New-Item -ItemType Directory -Path $fixtureClasses -Force | Out-Null
$fixtureSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'fixture') -Filter '*.java' | ForEach-Object FullName)
& 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot/bin/java.exe' -cp (Join-Path $root 'build/compiler-helper') CompileJava (Join-Path $root 'MICRO_NST.jar') $fixtureClasses @fixtureSources
if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
$fixtureJar = Join-Path $root 'build/fixture.jar'
& 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot/bin/jar.exe' cfm $fixtureJar (Join-Path $PSScriptRoot 'fixture/MANIFEST.MF') -C $fixtureClasses .
if ($LASTEXITCODE -ne 0) { throw 'Fixture packaging failed.' }
$priorAutoSession = $env:QLTK_AUTO_SESSION
$smokeRoot = Join-Path $root 'build/test-smoke'
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
$smokeHome = Join-Path $smokeRoot ([guid]::NewGuid().ToString('N'))
try {
    $env:QLTK_AUTO_SESSION = 'fixture-session'
    & $java -Xms16m -Xmx128m -XX:+UseSerialGC -cp ($testClasses + ';' + $compileClasspath) HeadlessSmoke $fixtureJar $smokeHome
    $smokeExitCode = $LASTEXITCODE
} finally {
    $env:QLTK_AUTO_SESSION = $priorAutoSession
    if ([IO.Path]::GetFullPath($smokeHome).StartsWith([IO.Path]::GetFullPath($smokeRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $smokeHome -Recurse -Force -ErrorAction SilentlyContinue
    }
}
if ($smokeExitCode -ne 0) { throw 'Offline emulator integration failed.' }
