// ApplicationLoopback.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <windows.h>
#include <iostream>
#include <stdio.h>
#include <tchar.h>
#include <psapi.h>
#include <vector>
//#include "LoopbackCapture.h"

using namespace std;

void usage()
{
    std::wcout <<
        L"Usage: ExcludeAppsAudio <AppName> <includetree|excludetree> <outputfilename>\n"
        L"\n";
}

bool isTheSameWord(TCHAR* procName, TCHAR* targetName) {
    int sizeOfProcName =  sizeof(procName);
    int sizeOfTargetName = sizeof(targetName);

    if (sizeOfProcName != sizeOfTargetName) return false;

    for (int i = 0; i < sizeOfProcName; ++i) {
        if (procName[i] != targetName[i]) return false;
    }

    return true;
}

void GetPidFromAppName(TCHAR* targetName, vector<DWORD>& ret) {
    
    DWORD allProc[1024];
    DWORD cbNeeded;
    int nProc;
    int i;

    // PID一覧を取得
    if (!EnumProcesses(allProc, sizeof(allProc), &cbNeeded)) return;

    nProc = cbNeeded / sizeof(DWORD);

    for (i = 0; i < nProc; i++) {
        TCHAR procName[MAX_PATH] = TEXT("<unknown>");

        HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION |
            PROCESS_VM_READ,
            FALSE, allProc[i]);

        // プロセス名を取得
        if (NULL != hProcess) {
            HMODULE hMod;
            DWORD cbNeeded;

            if (EnumProcessModules(hProcess, &hMod, sizeof(hMod),
                &cbNeeded)) {
                GetModuleBaseName(hProcess, hMod, procName,
                    sizeof(procName) / sizeof(TCHAR));
            }
        }

        if (isTheSameWord(procName, targetName)) {
            ret.push_back(allProc[i]);
            // プロセス名とPIDを表示
            _tprintf(TEXT("%s  (PID: %u)\n"), procName, allProc[i]);
        }

        CloseHandle(hProcess);
    }
}

int _tmain(int argc, TCHAR* argv[])
{
    // if (argc < 4)
    // {
    //     usage();
    //     return 0;
    // }

    // DWORD appName = wcstoul(argv[1], nullptr, 0);
    // if (processId == 0)
    // {
    //     usage();
    //     return 0;
    // }

    // bool includeProcessTree = false;

    // PCWSTR outputFile = argv[2];

    // CLoopbackCapture loopbackCapture;
    // HRESULT hr = loopbackCapture.StartCaptureAsync(processId, includeProcessTree, outputFile);
    // if (FAILED(hr))
    // {
    //     wil::unique_hlocal_string message;
    //     FormatMessageW(FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS | FORMAT_MESSAGE_ALLOCATE_BUFFER, nullptr, hr,
    //         MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (PWSTR)&message, 0, nullptr);
    //     std::wcout << L"Failed to start capture\n0x" << std::hex << hr << L": " << message.get() << L"\n";
    // }
    // else
    // {
    //     std::wcout << L"Capturing 10 seconds of audio." << std::endl;
    //     Sleep(10000);

    //     loopbackCapture.StopCaptureAsync();

    //     std::wcout << L"Finished.\n";
    // }

    // 結果を入れるベクタ
    vector<DWORD> pids;
    
    TCHAR appName[MAX_PATH];

    _tcsncpy_s(appName, argv[1], _TRUNCATE);
    GetPidFromAppName(appName, pids);

    for (const DWORD pid : pids) {
        cout << pid << endl;
    }

    return 0;
}