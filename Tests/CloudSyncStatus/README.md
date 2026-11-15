# CloudSync status regression
Run `dotnet run --project Tests/CloudSyncStatus/Test.csproj`. Compiles actual CloudSync.cs against deterministic Unity/HTTP stubs. No network, credentials, production database, personal saves, or Unity engine.
12 checks: empty/nonempty success, GET failure, cache/last-success preservation, full offline, malformed/null JSON, recovery, sending reset, automation network guard. Real Unity/network integration remains separate.
