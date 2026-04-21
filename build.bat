@echo off
chcp 65001 >nul
set "MANAGED="
for %%P in ("%ProgramFiles(x86)%\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed" "D:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed" "E:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed") do if exist "%%P\Assembly-CSharp.dll" set "MANAGED=%%~P"
if not defined MANAGED (echo [!] Вставь путь к Managed: & set /p "MANAGED=Path: ")
if not exist "%MANAGED%\Assembly-CSharp.dll" (echo [X] DLL не найдена & pause & exit /b)

echo [>] Компиляция под x64 (Harmony подключена)...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /t:library /out:NewLightsOut.dll ^
  /r:"%MANAGED%\Assembly-CSharp.dll" ^
  /r:"%MANAGED%\Assembly-CSharp-firstpass.dll" ^
  /r:"%MANAGED%\UnityEngine.CoreModule.dll" ^
  /r:"%MANAGED%\netstandard.dll" ^
  /r:"%MANAGED%\0Harmony.dll" ^
  /platform:x64 /optimize+ /nologo /utf8output Mod.cs > build_log.txt 2>&1

if exist NewLightsOut.dll (echo [OK] Готово. Кидай DLL+YAML+JSON в mods\local\NewLightsOut\) else (echo [X] Ошибка. Смотри build_log.txt)
pause