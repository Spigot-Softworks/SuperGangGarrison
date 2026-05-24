$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$sln = "f:\OpenGarrisonCode\OpenGarrison-Fork\OpenGarrison.sln"
$exe = "f:\OpenGarrisonCode\OpenGarrison-Fork\Client\bin\Debug\net10.0\OG2.exe"

& $dotnet build $sln -c Debug
if ($LASTEXITCODE -eq 0) {
    Start-Process $exe
}
