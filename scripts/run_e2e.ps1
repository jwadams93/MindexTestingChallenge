# build once in case user didn’t
dotnet build .\tests\E2E.Specs\E2E.Specs.csproj -v minimal -m:1 --no-restore
# then run without rebuilding/restoring to avoid file locks
dotnet test  .\tests\E2E.Specs\E2E.Specs.csproj -v normal  -m:1 --no-build --no-restore
