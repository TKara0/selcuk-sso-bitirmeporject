@echo off
chcp 65001 >nul
title Tek Kapi
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo Node.js bulunamadi. https://nodejs.org adresinden LTS surumunu kurup tekrar dene.
  pause
  exit /b 1
)

if not exist node_modules (
  echo Ilk calistirma: bagimliliklar kuruluyor...
  call npm install
  if errorlevel 1 ( pause & exit /b 1 )
  call npx playwright install chromium
)

echo Tek Kapi baslatiliyor. Bu pencereyi kapatirsan portal da kapanir.
node src\server.js
if errorlevel 1 pause
