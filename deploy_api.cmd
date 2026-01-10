@echo off
setlocal

echo ================================================
echo   Deploy TtcTransit API to Cloud Run
echo ================================================
echo.

REM === SETTINGS ===
set PROJECT=ttc-transit-esp32
set REGION=northamerica-northeast1
set REPO=ttc-repo
set IMAGE=ttc-transit-api
set TAG=v1
set FULL_IMAGE=%REGION%-docker.pkg.dev/%PROJECT%/%REPO%/%IMAGE%:%TAG%

echo Using:
echo   Project: %PROJECT%
echo   Region : %REGION%
echo   Image  : %FULL_IMAGE%
echo.

echo Step 1: Building Docker image...
docker build -t %IMAGE% -f Dockerfile.api .
IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed with error %ERRORLEVEL%.
    goto :fail
)

echo.
echo Step 2: Tagging image...
docker tag %IMAGE% %FULL_IMAGE%

echo.
echo Step 3: Pushing image to Artifact Registry...
docker push %FULL_IMAGE%
IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo Push failed with error %ERRORLEVEL%.
    goto :fail
)

echo.
echo Step 4: Deploying to Cloud Run...
gcloud run deploy %IMAGE% ^
  --image=%FULL_IMAGE% ^
  --region=%REGION% ^
  --platform=managed ^
  --allow-unauthenticated ^
  --port=8080 ^
  --set-env-vars=GTFS_DB_PATH=/app/Data/gtfs.sqlite ^
  --project=%PROJECT%

IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo Deploy failed with error %ERRORLEVEL%.
    goto :fail
)

echo.
echo ================================================
echo   DEPLOY COMPLETE!
echo ================================================
echo.
goto :end

:fail
echo.
echo ================================================
echo   DEPLOY FAILED
echo ================================================
echo.

:end
pause
endlocal
