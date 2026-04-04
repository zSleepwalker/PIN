@echo off
if exist ..\RIN.WebAPI\RIN.WebAPI\bin\Debug\net8.0\RIN.WebAPI.exe (
	start /D ..\RIN.WebAPI\RIN.WebAPI\bin\Debug\net8.0 RIN.WebAPI.exe
) else (
	echo RIN.WebAPI executable not found. Start RIN.WebAPI separately.
)
start /D .\ .\MatrixServer.exe
start /D .\ .\GameServer.exe