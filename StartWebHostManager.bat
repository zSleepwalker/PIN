@echo off
if exist ..\RIN.WebAPI\RIN.WebAPI\bin\Debug\net8.0\RIN.WebAPI.exe (
	start /D ..\RIN.WebAPI\RIN.WebAPI\bin\Debug\net8.0 RIN.WebAPI.exe
) else (
	echo RIN.WebAPI executable not found at ..\RIN.WebAPI\RIN.WebAPI\bin\Debug\net8.0\RIN.WebAPI.exe
	echo Build and run RIN.WebAPI separately.
)