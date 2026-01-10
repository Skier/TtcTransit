@echo off
setlocal

echo ================================================
echo   Deploy TtcTransit API to Cloud Run
echo ================================================
echo.

REM === SETTINGS ===
set PROJECT=ttc-transit-esp32

REM Где лежат образы (старый регион, где уже есть ttc-repo)
set IMAGE_REGION=northamerica-northeast1

REM Где будет работать Cloud Run сервис (новый регион, с domain mapping)
set RUN_REGION=us-central1

set REPO=ttc-repo
set SERVICE_NAME=ttc-transit-api
set TAG=v1

set FULL_IMAGE=%IMAGE_REGION%-docker.pkg.dev/%PROJECT%/%REPO%/%SERVICE_NAME%:%TAG%

echo Using:
echo   Project      : %PROJECT%
echo   Image region : %IMAGE_REGION%
echo   Run region   : %RUN_REGION%
echo   Image        : %FULL_IMAGE%
echo.

echo Step 1: Building Docker image...
docker build -t %SERVICE_NAME% -f Dockerfile.api .
IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed with error %ERRORLEVEL%.
    goto :fail
)

echo.
echo Step 2: Tagging image...
docker tag %SERVICE_NAME% %FULL_IMAGE%
IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo Tag failed with error %ERRORLEVEL%.
    goto :fail
)

echo.
echo Step 3: Pushing image to Artifact Registry...
docker push %FULL_IMAGE%
IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo Push failed with error %ERRORLEVEL%.
    goto :fail
)

echo.
echo Step 4: Deploying to Cloud Run (region: %RUN_REGION%)...
gcloud run deploy %SERVICE_NAME% ^
  --image=%FULL_IMAGE% ^
  --region=%RUN_REGION% ^
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
