@echo off
call "%ProgramFiles%\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
cl /O2 /EHsc /MD NvEnc.cpp /I C:\Video_Codec_SDK\Interface d3d11.lib dxgi.lib /Fe:NvEnc.exe