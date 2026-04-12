#include <Windows.h>
#include <iostream>
#include <stdio.h>
#include <tchar.h>
#include "LoopbackCapture.h"

int wmain(int argc, wchar_t* argv[])
{
	if (argc < 2)
	{
		std::wcerr << L"Usage: ProcessAudioCapture <pid>\n";
		return 1;
	}

	DWORD pid = wcstoul(argv[1], nullptr, 10);
	if (pid == 0)
	{
		std::wcerr << L"Invalid pid\n";
		return 1;
	}

	// include target process tree
	CLoopbackCapture capture;
	HRESULT hr = capture.StartCaptureToStdoutAsync(pid, true);
	if (FAILED(hr))
	{
		std::wcerr << L"StartCaptureToStdoutAsync failed: 0x" << std::hex << hr << L"\n";
		return 1;
	}

	// Rustがkillする前提
	Sleep(INFINITE);

	return 0;
}