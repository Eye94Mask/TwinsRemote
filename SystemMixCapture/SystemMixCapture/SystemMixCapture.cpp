#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <functiondiscoverykeys_devpkey.h>
#include <iostream>
#include <vector>
#include <thread>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "avrt.lib")

static bool g_running = true;

BOOL WINAPI ConsoleCtrlHandler(DWORD ctrlType)
{
    switch (ctrlType)
    {
    case CTRL_C_EVENT:
    case CTRL_BREAK_EVENT:
    case CTRL_CLOSE_EVENT:
    case CTRL_LOGOFF_EVENT:
    case CTRL_SHUTDOWN_EVENT:
        g_running = false;
        return TRUE;
    default:
        return FALSE;
    }
}

int wmain()
{
    SetConsoleCtrlHandler(ConsoleCtrlHandler, TRUE);

    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr))
    {
        std::wcerr << L"CoInitializeEx failed: 0x" << std::hex << hr << L"\n";
        return 1;
    }

    IMMDeviceEnumerator* pEnumerator = nullptr;
    IMMDevice* pDevice = nullptr;
    IAudioClient* pAudioClient = nullptr;
    IAudioCaptureClient* pCaptureClient = nullptr;
    WAVEFORMATEX* pMixFormat = nullptr;
    HANDLE hEvent = nullptr;

    do
    {
        hr = CoCreateInstance(
            __uuidof(MMDeviceEnumerator),
            nullptr,
            CLSCTX_ALL,
            __uuidof(IMMDeviceEnumerator),
            (void**)&pEnumerator);
        if (FAILED(hr))
        {
            std::wcerr << L"CoCreateInstance(MMDeviceEnumerator) failed: 0x" << std::hex << hr << L"\n";
            break;
        }

        hr = pEnumerator->GetDefaultAudioEndpoint(eRender, eConsole, &pDevice);
        if (FAILED(hr))
        {
            std::wcerr << L"GetDefaultAudioEndpoint failed: 0x" << std::hex << hr << L"\n";
            break;
        }

        hr = pDevice->Activate(
            __uuidof(IAudioClient),
            CLSCTX_ALL,
            nullptr,
            (void**)&pAudioClient);
        if (FAILED(hr))
        {
            std::wcerr << L"IMMDevice::Activate(IAudioClient) failed: 0x" << std::hex << hr << L"\n";
            break;
        }

        hr = pAudioClient->GetMixFormat(&pMixFormat);
        if (FAILED(hr))
        {
            std::wcerr << L"GetMixFormat failed: 0x" << std::hex << hr << L"\n";
            break;
        }

        // Rust 側 AudioEncoder に合わせて 48kHz / stereo / 16bit PCM を要求
        WAVEFORMATEX fmt = {};
        fmt.wFormatTag = WAVE_FORMAT_PCM;
        fmt.nChannels = 2;
        fmt.nSamplesPerSec = 48000;
        fmt.wBitsPerSample = 16;
        fmt.nBlockAlign = fmt.nChannels * fmt.wBitsPerSample / 8;
        fmt.nAvgBytesPerSec = fmt.nSamplesPerSec * fmt.nBlockAlign;
        fmt.cbSize = 0;

        hEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
        if (!hEvent)
        {
            std::wcerr << L"CreateEvent failed: " << GetLastError() << L"\n";
            break;
        }

        hr = pAudioClient->Initialize(
            AUDCLNT_SHAREMODE_SHARED,
            AUDCLNT_STREAMFLAGS_LOOPBACK |
            AUDCLNT_STREAMFLAGS_EVENTCALLBACK |
            AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM,
            0,
            0,
            &fmt,
            nullptr);
        if (FAILED(hr))
        {
            std::wcerr << L"IAudioClient::Initialize failed: 0x" << std::hex << hr << L"\n";
            break;
        }

        hr = pAudioClient->SetEventHandle(hEvent);
        if (FAILED(hr))
        {
            std::wcerr << L"SetEventHandle failed: 0x" << std::hex << hr << L"\n";
            break;
        }

        hr = pAudioClient->GetService(__uuidof(IAudioCaptureClient), (void**)&pCaptureClient);
        if (FAILED(hr))
        {
            std::wcerr << L"GetService(IAudioCaptureClient) failed: 0x" << std::hex << hr << L"\n";
            break;
        }

        hr = pAudioClient->Start();
        if (FAILED(hr))
        {
            std::wcerr << L"IAudioClient::Start failed: 0x" << std::hex << hr << L"\n";
            break;
        }

        HANDLE hStdout = GetStdHandle(STD_OUTPUT_HANDLE);
        if (hStdout == INVALID_HANDLE_VALUE || hStdout == nullptr)
        {
            std::wcerr << L"GetStdHandle(STD_OUTPUT_HANDLE) failed\n";
            break;
        }

        std::wcerr << L"[SystemMixCapture] started: 48kHz stereo 16bit PCM -> stdout\n";

        while (g_running)
        {
            DWORD waitResult = WaitForSingleObject(hEvent, 200);
            if (waitResult != WAIT_OBJECT_0)
            {
                continue;
            }

            while (true)
            {
                UINT32 packetFrames = 0;
                hr = pCaptureClient->GetNextPacketSize(&packetFrames);
                if (FAILED(hr))
                {
                    std::wcerr << L"GetNextPacketSize failed: 0x" << std::hex << hr << L"\n";
                    g_running = false;
                    break;
                }

                if (packetFrames == 0)
                {
                    break;
                }

                BYTE* pData = nullptr;
                UINT32 numFrames = 0;
                DWORD flags = 0;
                UINT64 devPos = 0;
                UINT64 qpcPos = 0;

                hr = pCaptureClient->GetBuffer(&pData, &numFrames, &flags, &devPos, &qpcPos);
                if (FAILED(hr))
                {
                    std::wcerr << L"GetBuffer failed: 0x" << std::hex << hr << L"\n";
                    g_running = false;
                    break;
                }

                const DWORD bytesToWrite = numFrames * fmt.nBlockAlign;
                DWORD bytesWritten = 0;

                if (flags & AUDCLNT_BUFFERFLAGS_SILENT)
                {
                    std::vector<BYTE> silence(bytesToWrite, 0);
                    if (!WriteFile(hStdout, silence.data(), bytesToWrite, &bytesWritten, nullptr))
                    {
                        std::wcerr << L"WriteFile(stdout silence) failed: " << GetLastError() << L"\n";
                        g_running = false;
                    }
                }
                else
                {
                    if (!WriteFile(hStdout, pData, bytesToWrite, &bytesWritten, nullptr))
                    {
                        std::wcerr << L"WriteFile(stdout) failed: " << GetLastError() << L"\n";
                        g_running = false;
                    }
                }

                hr = pCaptureClient->ReleaseBuffer(numFrames);
                if (FAILED(hr))
                {
                    std::wcerr << L"ReleaseBuffer failed: 0x" << std::hex << hr << L"\n";
                    g_running = false;
                    break;
                }

                if (!g_running)
                {
                    break;
                }
            }
        }

        pAudioClient->Stop();
    } while (false);

    if (hEvent) CloseHandle(hEvent);
    if (pMixFormat) CoTaskMemFree(pMixFormat);
    if (pCaptureClient) pCaptureClient->Release();
    if (pAudioClient) pAudioClient->Release();
    if (pDevice) pDevice->Release();
    if (pEnumerator) pEnumerator->Release();

    CoUninitialize();
    return 0;
}