# Validation
Select SPECIFY_FEATURE_DIRECTORY=specs/003-native-reliability for SpecKit scripts.
Run targeted scheduler, admission and transaction regressions first, then pwsh scripts/Invoke-Quality.ps1 and dotnet tool run codemetr-csharp -- check --format json via run-capped.sh.
All native platform checks remain mandatory; no live provider calls.
