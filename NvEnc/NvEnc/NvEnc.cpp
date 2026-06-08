#include <windows.h>
#include <ShlObj.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <io.h>
#include <fcntl.h>

#include <iostream>
#include <vector>
#include <stdexcept>
#include <cstdint>
#include <cstdio>
#include <string>
#include <thread>
#include <atomic>
#include <array>
#include <fstream>
#include <nlohmann/json.hpp>
#include "msdirent.h"
#include "nvEncodeAPI.h"
#include <mutex>
#include <sstream>

#pragma comment(lib, "Shell32.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")

NV_ENCODE_API_FUNCTION_LIST g_nvenc = {};
std::vector<nlohmann::json> customs;

static const char* NvEncStatusToString(NVENCSTATUS st) {
    switch (st) {
    case NV_ENC_SUCCESS: return "NV_ENC_SUCCESS";
    case NV_ENC_ERR_NO_ENCODE_DEVICE: return "NV_ENC_ERR_NO_ENCODE_DEVICE";
    case NV_ENC_ERR_UNSUPPORTED_DEVICE: return "NV_ENC_ERR_UNSUPPORTED_DEVICE";
    case NV_ENC_ERR_INVALID_ENCODERDEVICE: return "NV_ENC_ERR_INVALID_ENCODERDEVICE";
    case NV_ENC_ERR_INVALID_DEVICE: return "NV_ENC_ERR_INVALID_DEVICE";
    case NV_ENC_ERR_DEVICE_NOT_EXIST: return "NV_ENC_ERR_DEVICE_NOT_EXIST";
    case NV_ENC_ERR_INVALID_PTR: return "NV_ENC_ERR_INVALID_PTR";
    case NV_ENC_ERR_INVALID_EVENT: return "NV_ENC_ERR_INVALID_EVENT";
    case NV_ENC_ERR_INVALID_PARAM: return "NV_ENC_ERR_INVALID_PARAM";
    case NV_ENC_ERR_INVALID_CALL: return "NV_ENC_ERR_INVALID_CALL";
    case NV_ENC_ERR_OUT_OF_MEMORY: return "NV_ENC_ERR_OUT_OF_MEMORY";
    case NV_ENC_ERR_ENCODER_NOT_INITIALIZED: return "NV_ENC_ERR_ENCODER_NOT_INITIALIZED";
    case NV_ENC_ERR_UNSUPPORTED_PARAM: return "NV_ENC_ERR_UNSUPPORTED_PARAM";
    case NV_ENC_ERR_LOCK_BUSY: return "NV_ENC_ERR_LOCK_BUSY";
    case NV_ENC_ERR_NOT_ENOUGH_BUFFER: return "NV_ENC_ERR_NOT_ENOUGH_BUFFER";
    case NV_ENC_ERR_INVALID_VERSION: return "NV_ENC_ERR_INVALID_VERSION";
    case NV_ENC_ERR_MAP_FAILED: return "NV_ENC_ERR_MAP_FAILED";
    case NV_ENC_ERR_NEED_MORE_INPUT: return "NV_ENC_ERR_NEED_MORE_INPUT";
    case NV_ENC_ERR_ENCODER_BUSY: return "NV_ENC_ERR_ENCODER_BUSY";
    case NV_ENC_ERR_EVENT_NOT_REGISTERD: return "NV_ENC_ERR_EVENT_NOT_REGISTERD";
    case NV_ENC_ERR_GENERIC: return "NV_ENC_ERR_GENERIC";
    case NV_ENC_ERR_INCOMPATIBLE_CLIENT_KEY: return "NV_ENC_ERR_INCOMPATIBLE_CLIENT_KEY";
    case NV_ENC_ERR_UNIMPLEMENTED: return "NV_ENC_ERR_UNIMPLEMENTED";
    case NV_ENC_ERR_RESOURCE_REGISTER_FAILED: return "NV_ENC_ERR_RESOURCE_REGISTER_FAILED";
    case NV_ENC_ERR_RESOURCE_NOT_REGISTERED: return "NV_ENC_ERR_RESOURCE_NOT_REGISTERED";
    case NV_ENC_ERR_RESOURCE_NOT_MAPPED: return "NV_ENC_ERR_RESOURCE_NOT_MAPPED";
    default: return "UNKNOWN_NVENC_STATUS";
    }
}

static void CheckNvEnc(NVENCSTATUS st, const char* where) {
    if (st != NV_ENC_SUCCESS) {
        std::cerr << where << " failed: " << NvEncStatusToString(st)
            << " (" << static_cast<int>(st) << ")\n";
        throw std::runtime_error(where);
    }
}

static void CheckHr(HRESULT hr, const char* where) {
    if (FAILED(hr)) {
        std::cerr << where << " failed: HRESULT=0x" << std::hex << hr << std::dec << "\n";
        throw std::runtime_error(where);
    }
}

template<typename T>
static void SafeRelease(T*& p) {
    if (p) {
        p->Release();
        p = nullptr;
    }
}

static uint64_t TickMs() {
    return GetTickCount64();
}

static bool WriteAllStdout(const void* data, size_t size) {
    const uint8_t* p = reinterpret_cast<const uint8_t*>(data);
    while (size > 0) {
        size_t written = fwrite(p, 1, size, stdout);
        if (written == 0) {
            return false;
        }
        p += written;
        size -= written;
    }
    return true;
}

static bool WritePacketToStdout(const uint8_t* data, uint32_t size) {
    uint8_t len[4];
    len[0] = static_cast<uint8_t>(size & 0xFF);
    len[1] = static_cast<uint8_t>((size >> 8) & 0xFF);
    len[2] = static_cast<uint8_t>((size >> 16) & 0xFF);
    len[3] = static_cast<uint8_t>((size >> 24) & 0xFF);

    if (!WriteAllStdout(len, 4)) return false;
    if (!WriteAllStdout(data, size)) return false;
    fflush(stdout);
    return true;
}

void LoadNvEnc() {
    HMODULE h = LoadLibraryA("nvEncodeAPI64.dll");
    if (!h) throw std::runtime_error("nvEncodeAPI64.dll not found");

    auto create = reinterpret_cast<NVENCSTATUS(NVENCAPI*)(NV_ENCODE_API_FUNCTION_LIST*)>(
        GetProcAddress(h, "NvEncodeAPICreateInstance")
        );

    if (!create) throw std::runtime_error("NvEncodeAPICreateInstance not found");

    g_nvenc.version = NV_ENCODE_API_FUNCTION_LIST_VER;

    if (create(&g_nvenc) != NV_ENC_SUCCESS) {
        throw std::runtime_error("NvEncodeAPICreateInstance failed");
    }
}

// ----------------------------------------------------
// Stream Presets
// ----------------------------------------------------
enum class StreamPreset {
    Stable,
    Balanced,
    Quality,
    Mobile
};

struct StreamConfig {
    uint32_t width;
    uint32_t height;
    uint32_t fps;
    uint32_t averageBitrate;
    uint32_t maxBitrate;
    uint32_t vbvBufferSize;
    uint32_t vbvInitialDelay;
    uint32_t gopLength;
    uint32_t idrPeriod;
    bool repeatSpsPps;
    bool outputAud;
    uint32_t maxRefFrames;
    GUID profileGuid;
    GUID presetGuid;
    NV_ENC_TUNING_INFO tuningInfo;
    bool enableLookahead;
    uint32_t lookaheadDepth;
    bool disableIadapt;
    bool disableBadapt;
};

static StreamPreset ParseStreamPreset(int argc, char** argv) {
    if (argc < 2) {
        return StreamPreset::Balanced;
    }

    std::string arg = argv[1];
    for (auto& c : arg) {
        c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
    }

    if (arg == "stable")   return StreamPreset::Stable;
    if (arg == "balanced") return StreamPreset::Balanced;
    if (arg == "quality")  return StreamPreset::Quality;
    if (arg == "mobile")   return StreamPreset::Mobile;

    std::cerr << "[WARN] Unknown preset: " << arg << " (fallback to balanced)\n";
    return StreamPreset::Balanced;
}

static const char* StreamPresetToString(StreamPreset preset) {
    switch (preset) {
    case StreamPreset::Stable:   return "stable";
    case StreamPreset::Balanced: return "balanced";
    case StreamPreset::Quality:  return "quality";
    case StreamPreset::Mobile:   return "mobile";
    default:                     return "unknown";
    }
}

static StreamConfig GetStreamConfig(StreamPreset preset) {
    switch (preset) {
    case StreamPreset::Stable:
        return StreamConfig{
            1280, 720,
            30,
            2 * 1000 * 1000,
            4 * 1000 * 1000,
            6000 * 1000,
            6000 * 1000,
            60,
            60,
            true,
            false,
            1,
            NV_ENC_H264_PROFILE_HIGH_GUID,
            NV_ENC_PRESET_P3_GUID,
            NV_ENC_TUNING_INFO_LOW_LATENCY,
            false,
            0,
            true,
            true
        };

    case StreamPreset::Balanced:
        return StreamConfig{
            1920, 1080,
            30,
            4 * 1000 * 1000,
            6 * 1000 * 1000,
            1 * 1000 * 1000,
            0 * 1000 * 1000,
            60,
            60,
            true,
            false,
            1,
            NV_ENC_H264_PROFILE_HIGH_GUID,
            NV_ENC_PRESET_P4_GUID,
            NV_ENC_TUNING_INFO_LOW_LATENCY,
            false,
            0,
            true,
            true
        };

    case StreamPreset::Quality:
        return StreamConfig{
            1920, 1080,
            60,
            6 * 1000 * 1000,
            8 * 1000 * 1000,
            1 * 1000 * 1000,
            1 * 1000 * 1000,
            60,
            60,
            true,
            false,
            1,
            NV_ENC_H264_PROFILE_HIGH_GUID,
            NV_ENC_PRESET_P5_GUID,
            NV_ENC_TUNING_INFO_LOW_LATENCY,
            false,
            0,
            true,
            true
        };

    case StreamPreset::Mobile:
        return StreamConfig{
            1280, 720,
            30,
            5000 * 1000,
            2 * 1000 * 1000,
            2000 * 1000,
            2000 * 1000,
            30,
            30,
            true,
            false,
            1,
            NV_ENC_H264_PROFILE_HIGH_GUID,
            NV_ENC_PRESET_P2_GUID,
            NV_ENC_TUNING_INFO_LOW_LATENCY,
            false,
            0,
            true,
            true
        };
    }



    return GetStreamConfig(StreamPreset::Balanced);
}

// ----------------------------------------------------
// Custom Stream Set
// ----------------------------------------------------
// アプリ側では選択肢を与えていないため本来は不要
static GUID GetCustomProfileGuidFromStr(std::string profileGuid) {
    if (profileGuid == "NV_ENC_H264_PROFILE_BASELINE_GUID") return NV_ENC_H264_PROFILE_BASELINE_GUID;
    if (profileGuid == "NV_ENC_H264_PROFILE_MAIN_GUID") return NV_ENC_H264_PROFILE_MAIN_GUID;
    if (profileGuid == "NV_ENC_H264_PROFILE_HIGH_GUID") return NV_ENC_H264_PROFILE_HIGH_GUID;
    if (profileGuid == "NV_ENC_H264_PROFILE_HIGH_444_GUID") return NV_ENC_H264_PROFILE_HIGH_444_GUID;

    // 高画質・高効率
    return NV_ENC_H264_PROFILE_HIGH_GUID;
}

static GUID GetCustomPresetGuidFromStr(std::string presetGuid) {
    if (presetGuid == "NV_ENC_PRESET_P1_GUID") return NV_ENC_PRESET_P1_GUID;
    if (presetGuid == "NV_ENC_PRESET_P2_GUID") return NV_ENC_PRESET_P2_GUID;
    if (presetGuid == "NV_ENC_PRESET_P3_GUID") return NV_ENC_PRESET_P3_GUID;
    if (presetGuid == "NV_ENC_PRESET_P4_GUID") return NV_ENC_PRESET_P4_GUID;
    if (presetGuid == "NV_ENC_PRESET_P5_GUID") return NV_ENC_PRESET_P5_GUID;
    if (presetGuid == "NV_ENC_PRESET_P6_GUID") return NV_ENC_PRESET_P6_GUID;
    if (presetGuid == "NV_ENC_PRESET_P7_GUID") return NV_ENC_PRESET_P7_GUID;

    // デフォルトはバランスのいいP4
    return NV_ENC_PRESET_P4_GUID;
}

static NV_ENC_TUNING_INFO GetCustomTuningInfoGuidFromStr(std::string tuningInfo) {
    if (tuningInfo == "NV_ENC_TUNING_INFO_HIGH_QUALITY") return NV_ENC_TUNING_INFO_HIGH_QUALITY;
    if (tuningInfo == "NV_ENC_TUNING_INFO_LOW_LATENCY") return NV_ENC_TUNING_INFO_LOW_LATENCY;
    if (tuningInfo == "NV_ENC_TUNING_INFO_ULTRA_LOW_LATENCY") return NV_ENC_TUNING_INFO_ULTRA_LOW_LATENCY;

    // ゲーム向け低遅延設定をデフォルトに
    return NV_ENC_TUNING_INFO_ULTRA_LOW_LATENCY;
}

// カスタムモードを取得
static StreamConfig GetCustomModeFromFile(const std::string& customDirectoryPath, const std::string& customFileName) {
    std::ifstream ifs(customDirectoryPath + customFileName);

    if (!ifs.is_open()) {
        std::cerr << "Failed to open custom file: " << customFileName << std::endl;
        return StreamConfig{};
    }

    if (!nlohmann::json::accept(ifs)) {
        std::cerr << "Invalid JSON format: " << customFileName << std::endl;
        return StreamConfig{};
    }

    std::string customJsonString(
        (std::istreambuf_iterator<char>(ifs)),
        std::istreambuf_iterator<char>()
    );

    ifs.seekg(0, std::ios::beg);

    nlohmann::json customJson = nlohmann::json::parse(ifs);

    GUID customProfileGuid = GetCustomProfileGuidFromStr(customJson["profileGuid"]);
    GUID customPresetGuid = GetCustomPresetGuidFromStr(customJson["presetGuid"]);
    NV_ENC_TUNING_INFO customTuningInfo = GetCustomTuningInfoGuidFromStr(customJson["tuningInfo"]);

    StreamConfig customMode = StreamConfig{
        customJson["width"],
        customJson["height"],
        customJson["fps"],
        customJson["averageBitrate"],
        customJson["maxBitrate"],
        customJson["vbvBufferSize"],
        customJson["vbvInitialDelay"],
        customJson["gopLength"],
        customJson["idrPeriod"],
        customJson["repeatSpsPps"],
        customJson["outputAud"],
        customJson["maxRefFrames"],
        customProfileGuid,
        customPresetGuid,
        customTuningInfo,
        customJson["enableLookahead"],
        customJson["lookaheadDepth"],
        customJson["disableIadapt"],
        customJson["disableBadapt"]
    };

    return customMode;
}

static std::vector<std::string> GetCustomFilesInFolder(const std::string& folderPath) {
    std::vector<std::string> fileNames;
    DIR* dir;
    struct dirent* entry;

    if ((dir = opendir(folderPath.c_str())) != nullptr) {
        while ((entry = readdir(dir)) != nullptr) {
            if (entry->d_type == DT_REG) {
                //std::cout << entry->d_name << std::endl;
                fileNames.push_back(entry->d_name);
            }
        }

        closedir(dir);
    }

    return fileNames;
}

static bool IsValidCustomMode(StreamConfig cfg) {
    // 解像度とFPSが初期値になっているかで判定
    if (
        cfg.width <= 0 ||
        cfg.height <= 0 ||
        cfg.fps <= 0
    ) {
        return false;
    }

    return true;
}

static int GetCustomModeIndex(
    const std::vector<std::pair<StreamConfig, std::string>>& customModes,
    const std::string& argName
) {
    for (int i = 0; i < static_cast<int>(customModes.size()); i++) {
        if (customModes[i].second == argName) {
            return i;
        }
    }

    return -1;
}

// ----------------------------------------------------
// DXGI Duplication
// ----------------------------------------------------
struct DuplicationContext {
    IDXGIOutputDuplication* duplication = nullptr;
    UINT width = 0;
    UINT height = 0;
};

static std::wstring WStringFromAdapterDesc(const DXGI_ADAPTER_DESC& desc) {
    return std::wstring(desc.Description);
}

static std::string NarrowFromWide(const std::wstring& w) {
    if (w.empty()) return {};

    int size = WideCharToMultiByte(
        CP_UTF8,
        0,
        w.c_str(),
        -1,
        nullptr,
        0,
        nullptr,
        nullptr
    );

    std::string s(size - 1, 0);

    WideCharToMultiByte(
        CP_UTF8,
        0,
        w.c_str(),
        -1,
        s.data(),
        size,
        nullptr,
        nullptr
    );

    return s;
}

static bool AdapterHasOutput(IDXGIAdapter* adapter) {
    IDXGIFactory1* factory = nullptr;
    CheckHr(CreateDXGIFactory1(__uuidof(IDXGIFactory1),
        reinterpret_cast<void**>(&factory)), "CreateDXGIFactory1");

    IDXGIAdapter1* selected = nullptr;

    for (UINT i = 0;; ++i) {
        IDXGIAdapter1* adapter = nullptr;
        HRESULT hr = factory->EnumAdapters1(i, &adapter);
        if (hr == DXGI_ERROR_NOT_FOUND) break;
        if (FAILED(hr) || !adapter) continue;

        DXGI_ADAPTER_DESC1 desc{};
        adapter->GetDesc1(&desc);

        std::string name = NarrowFromWide(desc.Description);

        std::cerr << "[GPU] adapter[" << i << "] "
            << name
            << " VendorId=0x" << std::hex << desc.VendorId << std::dec
            << " flags=0x" << std::hex << desc.Flags << std::dec
            << "\n";

        if (desc.VendorId == 0x10DE &&
            !(desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE)) {
            selected = adapter;
            std::cerr << "[GPU] Selected NVIDIA encode adapter: "
                << name << "\n";
            break;
        }

        adapter->Release();
    }

    factory->Release();
    return selected;
}

static IDXGIAdapter1* FindCaptureAdapterForAttachedOutput() {
    IDXGIFactory1* factory = nullptr;
    CheckHr(CreateDXGIFactory1(__uuidof(IDXGIFactory1),
        reinterpret_cast<void**>(&factory)), "CreateDXGIFactory1");

    IDXGIAdapter1* selected = nullptr;
    LONG bestArea = 0;

    for (UINT ai = 0;; ++ai) {
        IDXGIAdapter1* adapter = nullptr;
        HRESULT hr = factory->EnumAdapters1(ai, &adapter);
        if (hr == DXGI_ERROR_NOT_FOUND) break;
        if (FAILED(hr) || !adapter) continue;

        DXGI_ADAPTER_DESC1 ad{};
        adapter->GetDesc1(&ad);

        std::string adapterName = NarrowFromWide(ad.Description);

        for (UINT oi = 0;; ++oi) {
            IDXGIOutput* output = nullptr;
            hr = adapter->EnumOutputs(oi, &output);
            if (hr == DXGI_ERROR_NOT_FOUND) break;
            if (FAILED(hr) || !output) continue;

            DXGI_OUTPUT_DESC od{};
            output->GetDesc(&od);

            LONG w = od.DesktopCoordinates.right - od.DesktopCoordinates.left;
            LONG h = od.DesktopCoordinates.bottom - od.DesktopCoordinates.top;
            LONG area = w * h;

            std::cerr << "[CAPTURE] adapter[" << ai << "] "
                << adapterName
                << " output[" << oi << "] attached="
                << od.AttachedToDesktop
                << " area=" << area
                << "\n";

            if (od.AttachedToDesktop && area > bestArea) {
                if (selected) selected->Release();
                selected = adapter;
                selected->AddRef();
                bestArea = area;

                std::cerr << "[CAPTURE] selected candidate: "
                    << adapterName << "\n";
            }

            output->Release();
        }

        adapter->Release();
    }

    factory->Release();
    return selected;
}

static IDXGIAdapter1* FindNvencAdapter() {
    IDXGIFactory1* factory = nullptr;
    CheckHr(CreateDXGIFactory1(__uuidof(IDXGIFactory1),
        reinterpret_cast<void**>(&factory)), "CreateDXGIFactory1");

    IDXGIAdapter1* selected = nullptr;

    for (UINT i = 0;; ++i) {
        IDXGIAdapter1* adapter = nullptr;
        HRESULT hr = factory->EnumAdapters1(i, &adapter);
        if (hr == DXGI_ERROR_NOT_FOUND) break;
        if (FAILED(hr) || !adapter) continue;

        DXGI_ADAPTER_DESC1 desc{};
        adapter->GetDesc1(&desc);

        std::string name = NarrowFromWide(desc.Description);

        std::cerr << "[GPU] adapter[" << i << "] "
            << name
            << " VendorId=0x" << std::hex << desc.VendorId << std::dec
            << " flags=0x" << std::hex << desc.Flags << std::dec
            << "\n";

        if (desc.VendorId == 0x10DE &&
            !(desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE)) {
            selected = adapter;
            std::cerr << "[GPU] Selected NVIDIA encode adapter: "
                << name << "\n";
            break;
        }

        adapter->Release();
    }

    factory->Release();
    return selected;
}

static IDXGIAdapter1* FindBestNvencAdapter() {
    IDXGIFactory1* factory = nullptr;

    HRESULT hr = CreateDXGIFactory1(
        __uuidof(IDXGIFactory1),
        reinterpret_cast<void**>(&factory)
    );

    if (FAILED(hr) || !factory) {
        std::cerr << "[GPU] CreateDXGIFactory1 failed: 0x"
            << std::hex << hr << std::dec << "\n";
        return nullptr;
    }

    IDXGIAdapter1* selected = nullptr;

    for (UINT i = 0;; ++i) {
        IDXGIAdapter1* adapter = nullptr;
        hr = factory->EnumAdapters1(i, &adapter);

        if (hr == DXGI_ERROR_NOT_FOUND) break;

        if (FAILED(hr) || !adapter) continue;

        DXGI_ADAPTER_DESC1 desc{};
        adapter->GetDesc1(&desc);

        std::string name = NarrowFromWide(desc.Description);

        std::cerr << "[INFO] Found adapter[" << i << "]: "
            << name
            << " VendorId=0x" << std::hex << desc.VendorId << std::dec
            << " DedicatedVideoMemory="
            << static_cast<unsigned long long>(desc.DedicatedVideoMemory / 1024 / 1024)
            << "MB\n";

        if (desc.VendorId == 0x10DE && AdapterHasOutput(adapter)) {
            selected = adapter;
            std::cerr << "[INFO] Selected NVIDIA adapter with output: "
                << name << "\n";
            break;
        }

        adapter->Release();
    }

    factory->Release();

    return selected;
}

static DuplicationContext CreateDuplication(ID3D11Device* device) {
    DuplicationContext out{};

    IDXGIDevice* dxgiDevice = nullptr;
    IDXGIAdapter* adapter = nullptr;

    try {
        CheckHr(
            device->QueryInterface(__uuidof(IDXGIDevice),
                reinterpret_cast<void**>(&dxgiDevice)),
            "ID3D11Device->QueryInterface(IDXGIDevice)"
        );

        CheckHr(dxgiDevice->GetAdapter(&adapter), "IDXGIDevice::GetAdapter");

        for (UINT i = 0;; ++i) {
            IDXGIOutput* output = nullptr;
            HRESULT hr = adapter->EnumOutputs(i, &output);

            if (hr == DXGI_ERROR_NOT_FOUND) {
                break;
            }

            if (FAILED(hr) || !output) {
                continue;
            }

            DXGI_OUTPUT_DESC outputDesc{};
            output->GetDesc(&outputDesc);

            std::string outputName = NarrowFromWide(outputDesc.DeviceName);

            std::cerr << "[INFO] Output[" << i << "]: "
                << outputName
                << " attached=" << outputDesc.AttachedToDesktop
                << " desktop=("
                << outputDesc.DesktopCoordinates.left << ","
                << outputDesc.DesktopCoordinates.top << ")-("
                << outputDesc.DesktopCoordinates.right << ","
                << outputDesc.DesktopCoordinates.bottom << ")\n";

            if (!outputDesc.AttachedToDesktop) {
                output->Release();
                continue;
            }

            IDXGIOutput1* output1 = nullptr;
            hr = output->QueryInterface(
                __uuidof(IDXGIOutput1),
                reinterpret_cast<void**>(&output1)
            );

            if (FAILED(hr) || !output1) {
                std::cerr << "[WARN] Output[" << i << "] QueryInterface IDXGIOutput1 failed: 0x"
                    << std::hex << hr << std::dec << "\n";
                output->Release();
                continue;
            }

            hr = output1->DuplicateOutput(device, &out.duplication);

            if (SUCCEEDED(hr)) {
                DXGI_OUTDUPL_DESC duplDesc{};
                out.duplication->GetDesc(&duplDesc);

                out.width = duplDesc.ModeDesc.Width;
                out.height = duplDesc.ModeDesc.Height;

                std::cerr << "[INFO] DuplicateOutput succeeded on Output[" << i << "]: "
                    << out.width << "x" << out.height << "\n";

                output1->Release();
                output->Release();
                SafeRelease(adapter);
                SafeRelease(dxgiDevice);
                return out;
            }

            std::cerr << "[WARN] DuplicateOutput failed on Output[" << i << "]: HRESULT=0x"
                << std::hex << hr << std::dec << "\n";

            output1->Release();
            output->Release();
        }

        throw std::runtime_error("No output could be duplicated");
    }
    catch (...) {
        SafeRelease(adapter);
        SafeRelease(dxgiDevice);

        if (out.duplication) {
            out.duplication->Release();
            out.duplication = nullptr;
        }

        throw;
    }
}

// ----------------------------------------------------
// NVENC
// ----------------------------------------------------
struct EncoderContext {
    void* encoder = nullptr;
    NV_ENC_OUTPUT_PTR bitstreamBuffer = nullptr;
    uint32_t width = 0;
    uint32_t height = 0;
};

static EncoderContext CreateEncoder(ID3D11Device* device, const StreamConfig& cfg) {
    EncoderContext ctx{};
    ctx.width = cfg.width;
    ctx.height = cfg.height;

    NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS openParams{};
    openParams.version = NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS_VER;
    openParams.device = device;
    openParams.deviceType = NV_ENC_DEVICE_TYPE_DIRECTX;
    openParams.apiVersion = NVENCAPI_VERSION;

    CheckNvEnc(
        g_nvenc.nvEncOpenEncodeSessionEx(&openParams, &ctx.encoder),
        "nvEncOpenEncodeSessionEx"
    );

    GUID encodeGUID = NV_ENC_CODEC_H264_GUID;

    NV_ENC_PRESET_CONFIG presetConfig{};
    presetConfig.version = NV_ENC_PRESET_CONFIG_VER;
    presetConfig.presetCfg.version = NV_ENC_CONFIG_VER;

    CheckNvEnc(
        g_nvenc.nvEncGetEncodePresetConfigEx(
            ctx.encoder,
            encodeGUID,
            cfg.presetGuid,
            cfg.tuningInfo,
            &presetConfig
        ),
        "nvEncGetEncodePresetConfigEx"
    );

    NV_ENC_CONFIG encodeConfig = presetConfig.presetCfg;
    encodeConfig.version = NV_ENC_CONFIG_VER;

    encodeConfig.profileGUID = cfg.profileGuid;
    encodeConfig.gopLength = cfg.gopLength;
    encodeConfig.frameIntervalP = 1;

    encodeConfig.rcParams.rateControlMode = NV_ENC_PARAMS_RC_CBR;
    encodeConfig.rcParams.averageBitRate = cfg.averageBitrate;
    encodeConfig.rcParams.maxBitRate = cfg.maxBitrate;
    encodeConfig.rcParams.vbvBufferSize = cfg.vbvBufferSize;
    encodeConfig.rcParams.vbvInitialDelay = cfg.vbvInitialDelay;
    encodeConfig.rcParams.enableLookahead = cfg.enableLookahead ? 1 : 0;
    encodeConfig.rcParams.lookaheadDepth = cfg.lookaheadDepth;
    encodeConfig.rcParams.disableIadapt = cfg.disableIadapt ? 1 : 0;
    encodeConfig.rcParams.disableBadapt = cfg.disableBadapt ? 1 : 0;

    encodeConfig.encodeCodecConfig.h264Config.repeatSPSPPS = cfg.repeatSpsPps ? 1 : 0;
    encodeConfig.encodeCodecConfig.h264Config.outputAUD = cfg.outputAud ? 1 : 0;
    encodeConfig.encodeCodecConfig.h264Config.idrPeriod = cfg.idrPeriod;
    encodeConfig.encodeCodecConfig.h264Config.maxNumRefFrames = cfg.maxRefFrames;
    encodeConfig.encodeCodecConfig.h264Config.sliceMode = 0;
    encodeConfig.encodeCodecConfig.h264Config.sliceModeData = 0;

    NV_ENC_INITIALIZE_PARAMS initParams{};
    initParams.version = NV_ENC_INITIALIZE_PARAMS_VER;
    initParams.encodeGUID = encodeGUID;
    initParams.presetGUID = cfg.presetGuid;
    initParams.tuningInfo = cfg.tuningInfo;
    initParams.encodeConfig = &encodeConfig;

    initParams.encodeWidth = cfg.width;
    initParams.encodeHeight = cfg.height;
    initParams.darWidth = cfg.width;
    initParams.darHeight = cfg.height;
    initParams.maxEncodeWidth = cfg.width;
    initParams.maxEncodeHeight = cfg.height;

    initParams.frameRateNum = cfg.fps;
    initParams.frameRateDen = 1;

    initParams.enablePTD = 1;
    initParams.enableEncodeAsync = 0;
    initParams.enableOutputInVidmem = 0;
    initParams.bufferFormat = NV_ENC_BUFFER_FORMAT_ARGB;

    CheckNvEnc(
        g_nvenc.nvEncInitializeEncoder(ctx.encoder, &initParams),
        "nvEncInitializeEncoder"
    );

    NV_ENC_CREATE_BITSTREAM_BUFFER createBs{};
    createBs.version = NV_ENC_CREATE_BITSTREAM_BUFFER_VER;

    CheckNvEnc(
        g_nvenc.nvEncCreateBitstreamBuffer(ctx.encoder, &createBs),
        "nvEncCreateBitstreamBuffer"
    );

    ctx.bitstreamBuffer = createBs.bitstreamBuffer;
    return ctx;
}

static void DestroyEncoder(EncoderContext& ctx) {
    if (ctx.encoder) {
        if (ctx.bitstreamBuffer) {
            g_nvenc.nvEncDestroyBitstreamBuffer(ctx.encoder, ctx.bitstreamBuffer);
            ctx.bitstreamBuffer = nullptr;
        }
        g_nvenc.nvEncDestroyEncoder(ctx.encoder);
        ctx.encoder = nullptr;
    }
}

// ----------------------------------------------------
// CPU fallback
// ----------------------------------------------------
struct GpuBridgeSlot {
    ID3D11Texture2D* encodeInputTex = nullptr;
};

struct GpuBridgeContext {
    static constexpr UINT SLOT_COUNT = 3;
    GpuBridgeSlot slots[SLOT_COUNT];
    UINT slotIndex = 0;

    UINT width = 0;
    UINT height = 0;
};

static GpuBridgeContext CreateGpuBridge(
    ID3D11Device* encodeDevice,
    UINT width,
    UINT height
) {
    GpuBridgeContext bridge{};
    bridge.width = width;
    bridge.height = height;

    for (UINT i = 0; i < GpuBridgeContext::SLOT_COUNT; ++i) {
        D3D11_TEXTURE2D_DESC desc{};

        desc.Width = width;
        desc.Height = height;
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags =
            D3D11_BIND_SHADER_RESOURCE |
            D3D11_BIND_RENDER_TARGET;

        CheckHr(
            encodeDevice->CreateTexture2D(&desc, nullptr, &bridge.slots[i].encodeInputTex),
            "CreateTexture2D(bridge encode input)"
        );
    }

    return bridge;
}

static void DestroyGpuBridge(GpuBridgeContext& bridge) {
    for (UINT i = 0; i < GpuBridgeContext::SLOT_COUNT; ++i) {
        SafeRelease(bridge.slots[i].encodeInputTex);
    }

    bridge.slotIndex = 0;
    bridge.width = 0;
    bridge.height = 0;
}

static ID3D11Texture2D* CopyCaptureTextureToEncodeGpuCpuFallback(
    GpuBridgeContext& bridge,
    ID3D11Device* captureDevice,
    ID3D11DeviceContext* captureContext,
    ID3D11DeviceContext* encodeContext,
    ID3D11Texture2D* captureTex
) {
    GpuBridgeSlot& slot = bridge.slots[bridge.slotIndex];

    D3D11_TEXTURE2D_DESC srcDesc{};
    captureTex->GetDesc(&srcDesc);

    D3D11_TEXTURE2D_DESC stagingDesc{};
    stagingDesc.Width = bridge.width;
    stagingDesc.Height = bridge.height;
    stagingDesc.MipLevels = 1;
    stagingDesc.ArraySize = 1;
    stagingDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    stagingDesc.SampleDesc.Count = 1;
    stagingDesc.Usage = D3D11_USAGE_STAGING;
    stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;

    ID3D11Texture2D* staging = nullptr;

    CheckHr(
        captureDevice->CreateTexture2D(&stagingDesc, nullptr, &staging),
        "CreateTexture2D(cpu staging)"
    );

    captureContext->CopyResource(staging, captureTex);

    D3D11_MAPPED_SUBRESOURCE mapped{};
    CheckHr(
        captureContext->Map(staging, 0, D3D11_MAP_READ, 0, &mapped),
        "Map(cpu staging)"
    );

    encodeContext->UpdateSubresource(
        slot.encodeInputTex,
        0,
        nullptr,
        mapped.pData,
        mapped.RowPitch,
        0
    );

    encodeContext->Flush();

    captureContext->Unmap(staging, 0);
    staging->Release();

    bridge.slotIndex = (bridge.slotIndex + 1) % GpuBridgeContext::SLOT_COUNT;
    return slot.encodeInputTex;
}

// ----------------------------------------------------
// Scaler + Registered Ring Buffers
// ----------------------------------------------------
struct EncodeSlot {
    ID3D11Texture2D* tex = nullptr;
    ID3D11VideoProcessorOutputView* outputView = nullptr;
    NV_ENC_REGISTERED_PTR registered = nullptr;
};

struct ScaleContext {
    static constexpr UINT SLOT_COUNT = 3;

    EncodeSlot slots[SLOT_COUNT];
    UINT slotIndex = 0;

    ID3D11VideoDevice* videoDevice = nullptr;
    ID3D11VideoContext* videoContext = nullptr;
    ID3D11VideoProcessorEnumerator* enumerator = nullptr;
    ID3D11VideoProcessor* processor = nullptr;

    UINT inWidth = 0;
    UINT inHeight = 0;
    UINT outWidth = 0;
    UINT outHeight = 0;
};

static ScaleContext CreateScaler(
    ID3D11Device* device,
    ID3D11DeviceContext* context,
    void* encoder,
    UINT inWidth,
    UINT inHeight,
    UINT outWidth,
    UINT outHeight
) {
    ScaleContext sc{};
    sc.inWidth = inWidth;
    sc.inHeight = inHeight;
    sc.outWidth = outWidth;
    sc.outHeight = outHeight;

    CheckHr(
        device->QueryInterface(__uuidof(ID3D11VideoDevice),
            reinterpret_cast<void**>(&sc.videoDevice)),
        "QueryInterface(ID3D11VideoDevice)"
    );

    CheckHr(
        context->QueryInterface(__uuidof(ID3D11VideoContext),
            reinterpret_cast<void**>(&sc.videoContext)),
        "QueryInterface(ID3D11VideoContext)"
    );

    D3D11_VIDEO_PROCESSOR_CONTENT_DESC contentDesc{};
    contentDesc.InputFrameFormat = D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE;
    contentDesc.InputWidth = inWidth;
    contentDesc.InputHeight = inHeight;
    contentDesc.OutputWidth = outWidth;
    contentDesc.OutputHeight = outHeight;
    contentDesc.Usage = D3D11_VIDEO_USAGE_PLAYBACK_NORMAL;

    CheckHr(
        sc.videoDevice->CreateVideoProcessorEnumerator(&contentDesc, &sc.enumerator),
        "CreateVideoProcessorEnumerator"
    );

    CheckHr(
        sc.videoDevice->CreateVideoProcessor(sc.enumerator, 0, &sc.processor),
        "CreateVideoProcessor"
    );

    for (UINT i = 0; i < ScaleContext::SLOT_COUNT; ++i) {
        D3D11_TEXTURE2D_DESC texDesc{};
        texDesc.Width = outWidth;
        texDesc.Height = outHeight;
        texDesc.MipLevels = 1;
        texDesc.ArraySize = 1;
        texDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        texDesc.SampleDesc.Count = 1;
        texDesc.Usage = D3D11_USAGE_DEFAULT;
        texDesc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;

        CheckHr(
            device->CreateTexture2D(&texDesc, nullptr, &sc.slots[i].tex),
            "CreateTexture2D(scale slot)"
        );

        D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC ovDesc{};
        ovDesc.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;
        ovDesc.Texture2D.MipSlice = 0;

        CheckHr(
            sc.videoDevice->CreateVideoProcessorOutputView(
                sc.slots[i].tex,
                sc.enumerator,
                &ovDesc,
                &sc.slots[i].outputView
            ),
            "CreateVideoProcessorOutputView"
        );

        NV_ENC_REGISTER_RESOURCE reg{};
        reg.version = NV_ENC_REGISTER_RESOURCE_VER;
        reg.resourceType = NV_ENC_INPUT_RESOURCE_TYPE_DIRECTX;
        reg.resourceToRegister = sc.slots[i].tex;
        reg.width = outWidth;
        reg.height = outHeight;
        reg.pitch = 0;
        reg.bufferFormat = NV_ENC_BUFFER_FORMAT_ARGB;
        reg.bufferUsage = NV_ENC_INPUT_IMAGE;

        CheckNvEnc(
            g_nvenc.nvEncRegisterResource(encoder, &reg),
            "nvEncRegisterResource(slot init)"
        );

        sc.slots[i].registered = reg.registeredResource;
    }

    return sc;
}

static EncodeSlot& ScaleTexture(ScaleContext& sc, ID3D11Texture2D* inputTex) {
    EncodeSlot& slot = sc.slots[sc.slotIndex];

    ID3D11VideoProcessorInputView* inputView = nullptr;

    D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC ivDesc{};
    ivDesc.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
    ivDesc.Texture2D.ArraySlice = 0;
    ivDesc.Texture2D.MipSlice = 0;

    CheckHr(
        sc.videoDevice->CreateVideoProcessorInputView(
            inputTex,
            sc.enumerator,
            &ivDesc,
            &inputView
        ),
        "CreateVideoProcessorInputView"
    );

    RECT srcRect{ 0, 0, static_cast<LONG>(sc.inWidth), static_cast<LONG>(sc.inHeight) };
    RECT dstRect{ 0, 0, static_cast<LONG>(sc.outWidth), static_cast<LONG>(sc.outHeight) };

    sc.videoContext->VideoProcessorSetStreamSourceRect(sc.processor, 0, TRUE, &srcRect);
    sc.videoContext->VideoProcessorSetStreamDestRect(sc.processor, 0, TRUE, &dstRect);
    sc.videoContext->VideoProcessorSetOutputTargetRect(sc.processor, TRUE, &dstRect);

    D3D11_VIDEO_PROCESSOR_STREAM stream{};
    stream.Enable = TRUE;
    stream.pInputSurface = inputView;

    CheckHr(
        sc.videoContext->VideoProcessorBlt(
            sc.processor,
            slot.outputView,
            0,
            1,
            &stream
        ),
        "VideoProcessorBlt"
    );

    inputView->Release();

    sc.slotIndex = (sc.slotIndex + 1) % ScaleContext::SLOT_COUNT;
    return slot;
}

static void DestroyScaler(ScaleContext& sc, void* encoder) {
    for (UINT i = 0; i < ScaleContext::SLOT_COUNT; ++i) {
        if (sc.slots[i].registered) {
            g_nvenc.nvEncUnregisterResource(encoder, sc.slots[i].registered);
            sc.slots[i].registered = nullptr;
        }
        SafeRelease(sc.slots[i].outputView);
        SafeRelease(sc.slots[i].tex);
    }

    SafeRelease(sc.processor);
    SafeRelease(sc.enumerator);
    SafeRelease(sc.videoContext);
    SafeRelease(sc.videoDevice);
}

static bool EncodeRegisteredTexture(
    EncoderContext& enc,
    NV_ENC_REGISTERED_PTR registered,
    uint64_t frameIndex,
    bool forceIDR,
    bool outputSpsPps
) {
    NV_ENC_MAP_INPUT_RESOURCE map{};
    map.version = NV_ENC_MAP_INPUT_RESOURCE_VER;
    map.registeredResource = registered;

    try {
        CheckNvEnc(
            g_nvenc.nvEncMapInputResource(enc.encoder, &map),
            "nvEncMapInputResource"
        );

        NV_ENC_PIC_PARAMS pic{};
        pic.version = NV_ENC_PIC_PARAMS_VER;
        pic.inputBuffer = map.mappedResource;
        pic.bufferFmt = NV_ENC_BUFFER_FORMAT_ARGB;
        pic.inputWidth = enc.width;
        pic.inputHeight = enc.height;
        pic.outputBitstream = enc.bitstreamBuffer;
        pic.pictureStruct = NV_ENC_PIC_STRUCT_FRAME;
        pic.inputTimeStamp = frameIndex;

        if (forceIDR) {
            pic.encodePicFlags |= NV_ENC_PIC_FLAG_FORCEIDR;
        }
        if (outputSpsPps) {
            pic.encodePicFlags |= NV_ENC_PIC_FLAG_OUTPUT_SPSPPS;
        }

        NVENCSTATUS st = g_nvenc.nvEncEncodePicture(enc.encoder, &pic);
        if (st == NV_ENC_ERR_ENCODER_BUSY || st == NV_ENC_ERR_NEED_MORE_INPUT) {
            g_nvenc.nvEncUnmapInputResource(enc.encoder, map.mappedResource);
            return false;
        }
        CheckNvEnc(st, "nvEncEncodePicture");

        NV_ENC_LOCK_BITSTREAM lock{};
        lock.version = NV_ENC_LOCK_BITSTREAM_VER;
        lock.outputBitstream = enc.bitstreamBuffer;
        lock.doNotWait = 0;

        CheckNvEnc(
            g_nvenc.nvEncLockBitstream(enc.encoder, &lock),
            "nvEncLockBitstream"
        );

        try {
            if (!WritePacketToStdout(
                reinterpret_cast<const uint8_t*>(lock.bitstreamBufferPtr),
                static_cast<uint32_t>(lock.bitstreamSizeInBytes))) {
                g_nvenc.nvEncUnlockBitstream(enc.encoder, enc.bitstreamBuffer);
                g_nvenc.nvEncUnmapInputResource(enc.encoder, map.mappedResource);
                return false;
            }
        }
        catch (...) {
            g_nvenc.nvEncUnlockBitstream(enc.encoder, enc.bitstreamBuffer);
            throw;
        }

        CheckNvEnc(
            g_nvenc.nvEncUnlockBitstream(enc.encoder, enc.bitstreamBuffer),
            "nvEncUnlockBitstream"
        );

        CheckNvEnc(
            g_nvenc.nvEncUnmapInputResource(enc.encoder, map.mappedResource),
            "nvEncUnmapInputResource"
        );

        return true;
    }
    catch (...) {
        if (map.mappedResource) {
            g_nvenc.nvEncUnmapInputResource(enc.encoder, map.mappedResource);
        }
        throw;
    }
}

// ----------------------------------------------------
// Recreate helpers
// ----------------------------------------------------
struct RecreateSessionException : public std::runtime_error {
    explicit RecreateSessionException(const char* msg) : std::runtime_error(msg) {}
};

static void DestroyDuplication(DuplicationContext& dup) {
    if (dup.duplication) {
        dup.duplication->Release();
        dup.duplication = nullptr;
    }
    dup.width = 0;
    dup.height = 0;
}

static void DestroySession(
    DuplicationContext& dup,
    GpuBridgeContext& bridge,
    ScaleContext& scaler,
    EncoderContext& enc
) {
    DestroyScaler(scaler, enc.encoder);
    DestroyEncoder(enc);
    DestroyGpuBridge(bridge);
    DestroyDuplication(dup);
}

static void CreateSession(
    ID3D11Device* captureDevice,
    ID3D11Device* encodeDevice,
    ID3D11DeviceContext* encodeContext,
    DuplicationContext& dup,
    GpuBridgeContext& bridge,
    ScaleContext& scaler,
    EncoderContext& enc,
    const StreamConfig& cfg
) {
    dup = CreateDuplication(captureDevice);

    enc = CreateEncoder(encodeDevice, cfg);

    bridge = CreateGpuBridge(
        encodeDevice,
        dup.width,
        dup.height
    );

    scaler = CreateScaler(
        encodeDevice,
        encodeContext,
        enc.encoder,
        dup.width,
        dup.height,
        cfg.width,
        cfg.height
    );
}

std::string ReplaceOtherStr(std::string& replacedStr, std::string from, std::string to) {
    const unsigned int pos = replacedStr.find(from);
    const int len = from.length();

    if (pos == std::string::npos || from.empty()) {
        return replacedStr;
    }

    return replacedStr.replace(pos, len, to);
}

bool ShouldApplyRelayCap(StreamConfig current, StreamConfig cap){
    return current.width > cap.width
        || current.height > cap.height
        || current.fps > cap.fps
        || current.averageBitrate > cap.averageBitrate;
}

static StreamConfig GetStreamConfigByName(
    const std::string& name,
    const std::vector<std::pair<StreamConfig, std::string>>& customModes
) {
    const int customIndex = GetCustomModeIndex(customModes, name);

    if (customIndex >= 0) {
        return customModes[customIndex].first;
    }

    std::string arg = name;
    for (auto& c : arg) {
        c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
    }

    if (arg == "stable")   { return GetStreamConfig(StreamPreset::Stable); }
    if (arg == "balanced") { return GetStreamConfig(StreamPreset::Balanced); }
    if (arg == "quality")  { return GetStreamConfig(StreamPreset::Quality); }
    if (arg == "mobile")   { return GetStreamConfig(StreamPreset::Mobile); }

    std::cerr << "[WARN] Unknown preset in set_preset: "
        << name
        << " fallback to balanced\n";

    return GetStreamConfig(StreamPreset::Balanced);
}

static std::string WideToSjis(const std::wstring& wstr) {
    if (wstr.empty()) return {};

    int sizeNeeded = WideCharToMultiByte(
        CP_ACP,
        0,
        wstr.c_str(),
        (int)wstr.size(),
        nullptr,
        0,
        nullptr,
        nullptr
    );

    std::string result(sizeNeeded, 0);

    WideCharToMultiByte(
        CP_ACP,
        0,
        wstr.c_str(),
        (int)wstr.size(),
        result.data(),
        sizeNeeded,
        nullptr,
        nullptr
    );

    return result;
}

static std::string GetMyDocumentPath() {
    PWSTR path = nullptr;

    HRESULT hr = SHGetKnownFolderPath(FOLDERID_Documents, 0, nullptr, &path);

    if (SUCCEEDED(hr)) {
        std::wstring wpath(path);
        std::string utf8Path = WideToSjis(wpath);

        CoTaskMemFree(path);
        return utf8Path;
    }

    return "";
}

// ----------------------------------------------------
// Main
// ----------------------------------------------------
int main(int argc, char** argv) {
    _setmode(_fileno(stdout), _O_BINARY);
    _setmode(_fileno(stdin), _O_BINARY);

    std::string customDirectoryPath = GetMyDocumentPath() + "/Twins Remote/customs";
    std::vector<std::string> customFileNames = GetCustomFilesInFolder(customDirectoryPath);
    std::vector<std::pair<StreamConfig, std::string>> customModes;

    for (std::string customFileName : customFileNames) {
        StreamConfig config =
            GetCustomModeFromFile(customDirectoryPath + "/", customFileName);

        customModes.push_back(
            std::pair(config, ReplaceOtherStr(customFileName, ".json", ""))
        );
    }

    ID3D11Device* captureDevice = nullptr;
    ID3D11DeviceContext* captureContext = nullptr;

    ID3D11Device* encodeDevice = nullptr;
    ID3D11DeviceContext* encodeContext = nullptr;

    DuplicationContext dup{};
    EncoderContext enc{};
    ScaleContext scaler{};
    GpuBridgeContext bridge{};

    StreamConfig cfg{};

    std::atomic<bool> forceIdrRequested{ false };
    std::atomic<bool> stopCommandThread{ false };
    std::atomic<bool> reconfigureRequested{ false };
    std::atomic<bool> relayCapActuallyApplied{ false };

    std::mutex pendingConfigMutex;
    StreamConfig pendingConfig{};

    std::thread commandThread([&]() {
        std::string line;

        while (!stopCommandThread.load()) {
            if (!std::getline(std::cin, line)) {
                Sleep(10);
                continue;
            }

            if (line == "force_idr") {
                forceIdrRequested.store(true);
                std::cerr << "[NVENC] command received: force_idr\n";
                continue;
            }

            if (line.rfind("set_preset ", 0) == 0) {
                if (!relayCapActuallyApplied.load()) {
                    std::cerr << "[NVENC] set_preset skipped: relay cap was not applied\n";
                    continue;
                }

                std::istringstream iss(line);

                std::string cmd;
                std::string presetName;

                if (iss >> cmd >> presetName) {
                    StreamConfig next = GetStreamConfigByName(presetName, customModes);

                    {
                        std::lock_guard<std::mutex> lock(pendingConfigMutex);
                        pendingConfig = next;
                    }

                    reconfigureRequested.store(true);
                    forceIdrRequested.store(true);
                    relayCapActuallyApplied.store(false);

                    std::cerr << "[NVENC] command received: set_preset "
                        << presetName << "\n";
                }

                continue;
            }

            if (line.rfind("set_config ", 0) == 0) {
                std::istringstream iss(line);

                std::string cmd;
                uint32_t width = 0;
                uint32_t height = 0;
                uint32_t fps = 0;
                uint32_t averageBitrate = 0;
                uint32_t maxBitrate = 0;
                uint32_t vbvBufferSize = 0;
                uint32_t vbvInitialDelay = 0;
                uint32_t gopLength = 0;
                uint32_t idrPeriod = 0;
                uint32_t repeatSpsPps = 0;
                uint32_t outputAud = 0;
                uint32_t maxRefFrames = 0;
                std::string profileGuid;
                std::string presetGuid;
                std::string tuningInfo;
                uint32_t enableLookahead = 0;
                uint32_t lookaheadDepth = 0;
                uint32_t disableIadapt = 0;
                uint32_t disableBadapt = 0;

                if (
                    iss >> cmd
                    >> width
                    >> height
                    >> fps
                    >> averageBitrate
                    >> maxBitrate
                    >> vbvBufferSize
                    >> vbvInitialDelay
                    >> gopLength
                    >> idrPeriod
                    >> repeatSpsPps
                    >> outputAud
                    >> maxRefFrames
                    >> profileGuid
                    >> presetGuid
                    >> tuningInfo
                    >> enableLookahead
                    >> lookaheadDepth
                    >> disableIadapt
                    >> disableBadapt
                    ) {
                    if (width == 0 || height == 0 || fps == 0 || averageBitrate == 0) {
                        std::cerr << "[NVENC] invalid set_config values\n";
                        continue;
                    }

                    StreamConfig next{};
                    next.width = width;
                    next.height = height;
                    next.fps = fps;
                    next.averageBitrate = averageBitrate;
                    next.maxBitrate = maxBitrate;
                    next.vbvBufferSize = vbvBufferSize;
                    next.vbvInitialDelay = vbvInitialDelay;
                    next.gopLength = gopLength;
                    next.idrPeriod = idrPeriod;
                    next.repeatSpsPps = repeatSpsPps != 0;
                    next.outputAud = outputAud != 0;
                    next.maxRefFrames = maxRefFrames;
                    next.profileGuid = GetCustomProfileGuidFromStr(profileGuid);
                    next.presetGuid = GetCustomPresetGuidFromStr(presetGuid);
                    next.tuningInfo = GetCustomTuningInfoGuidFromStr(tuningInfo);
                    next.enableLookahead = enableLookahead != 0;
                    next.lookaheadDepth = lookaheadDepth;
                    next.disableIadapt = disableIadapt != 0;
                    next.disableBadapt = disableBadapt != 0;

                    if (!ShouldApplyRelayCap(cfg, next)) {
                        relayCapActuallyApplied.store(false);
                        std::cerr << "[NVENC] relay cap skipped: current config is already <= cap\n";
                        continue;
                    }

                    relayCapActuallyApplied.store(true);

                    {
                        std::lock_guard<std::mutex> lock(pendingConfigMutex);
                        pendingConfig = next;
                    }

                    reconfigureRequested.store(true);
                    forceIdrRequested.store(true);

                    std::cerr << "[NVENC] command received: set_config "
                        << width << "x" << height
                        << " @" << fps
                        << " bitrate=" << averageBitrate / 1000
                        << "kbps\n";
                }
                else {
                    std::cerr << "[NVENC] failed to parse set_config: "
                        << line << "\n";
                }

                continue;
            }

            std::cerr << "[NVENC] unknown command: " << line << "\n";
        }
        });

    try {
        D3D_FEATURE_LEVEL flOut = D3D_FEATURE_LEVEL_11_0;
        D3D_FEATURE_LEVEL fls[] = {
            D3D_FEATURE_LEVEL_11_1,
            D3D_FEATURE_LEVEL_11_0
        };

        UINT flags = 0;

        IDXGIAdapter1* captureAdapter = FindCaptureAdapterForAttachedOutput();
        if (!captureAdapter) {
            throw std::runtime_error("No capture adapter with attached output found");
        }

        IDXGIAdapter1* nvAdapter = FindNvencAdapter();
        if (!nvAdapter) {
            throw std::runtime_error("No NVIDIA NVENC adapter found");
        }

        CheckHr(
            D3D11CreateDevice(
                captureAdapter,
                D3D_DRIVER_TYPE_UNKNOWN,
                nullptr,
                flags,
                fls,
                static_cast<UINT>(std::size(fls)),
                D3D11_SDK_VERSION,
                &captureDevice,
                &flOut,
                &captureContext
            ),
            "D3D11CreateDevice(capture adapter)"
        );

        CheckHr(
            D3D11CreateDevice(
                nvAdapter,
                D3D_DRIVER_TYPE_UNKNOWN,
                nullptr,
                flags,
                fls,
                static_cast<UINT>(std::size(fls)),
                D3D11_SDK_VERSION,
                &encodeDevice,
                &flOut,
                &encodeContext
            ),
            "D3D11CreateDevice(encode adapter)"
        );

        captureAdapter->Release();
        nvAdapter->Release();

        std::cerr << "[INFO] Capture D3D11 Device Created\n";
        std::cerr << "[INFO] Encode D3D11 Device Created\n";

        if (!nvAdapter) {
            throw std::runtime_error("No NVIDIA NVENC adapter found");
        }

        CheckHr(
            D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_HARDWARE,
                nullptr,
                flags,
                fls,
                static_cast<UINT>(std::size(fls)),
                D3D11_SDK_VERSION,
                &captureDevice,
                &flOut,
                &captureContext
            ),
            "D3D11CreateDevice(capture)"
        );

        CheckHr(
            D3D11CreateDevice(
                nvAdapter,
                D3D_DRIVER_TYPE_UNKNOWN,
                nullptr,
                flags,
                fls,
                static_cast<UINT>(std::size(fls)),
                D3D11_SDK_VERSION,
                &encodeDevice,
                &flOut,
                &encodeContext
            ),
            "D3D11CreateDevice(encode)"
        );

        nvAdapter->Release();
        nvAdapter = nullptr;

        std::cerr << "[INFO] Capture D3D11 Device Created\n";
        std::cerr << "[INFO] Encode D3D11 Device Created\n";

        LoadNvEnc();
        std::cerr << "[INFO] NVENC API Loaded\n";

        std::string modeName;

        int customIndex = -1;
        if (argc >= 2) {
            customIndex = GetCustomModeIndex(customModes, argv[1]);
        }

        if (customIndex >= 0) {
            auto& [mode, name] = customModes[customIndex];
            modeName = name;
            cfg = mode;
        }
        else {
            StreamPreset preset = ParseStreamPreset(argc, argv);
            modeName = StreamPresetToString(preset);
            cfg = GetStreamConfig(preset);
        }

		std::cerr << "[INFO] Selected Mode Config: " << modeName
            << " (" << cfg.width << "x" << cfg.height
            << " @" << cfg.fps << "fps, "
            << cfg.averageBitrate / 1000000.0 << "Mbps)\n";

        uint64_t frameIndex = 0;
        bool firstFrame = true;

        EncodeSlot* lastEncodedSlot = nullptr;

        while (true) {
            try {
                CreateSession(
                    captureDevice,
                    encodeDevice,
                    encodeContext,
                    dup,
                    bridge,
                    scaler,
                    enc,
                    cfg
                );

                while (true) {
                    IDXGIResource* desktopResource = nullptr;
                    ID3D11Texture2D* desktopTex = nullptr;
                    bool acquiredFrame = false;

                    if (reconfigureRequested.exchange(false)) {
                        {
                            std::lock_guard<std::mutex> lock(pendingConfigMutex);
                            cfg = pendingConfig;
                        }

                        firstFrame = true;
                        forceIdrRequested.store(true);

                        std::cerr << "[NVENC] reconfigure requested: "
                            << cfg.width << "x" << cfg.height
                            << " @" << cfg.fps
                            << " bitrate=" << cfg.averageBitrate / 1000
                            << "kbps\n";

                        throw RecreateSessionException("stream config changed");
                    }

                    try {
                        DXGI_OUTDUPL_FRAME_INFO frameInfo{};
                        HRESULT hr = dup.duplication->AcquireNextFrame(
                            0,
                            &frameInfo,
                            &desktopResource
                        );

                        if (hr == DXGI_ERROR_WAIT_TIMEOUT) {
                            if (lastEncodedSlot) {
                                EncodeRegisteredTexture(
                                    enc,
                                    lastEncodedSlot->registered,
                                    frameIndex++,
                                    false,
                                    true
                                );
                            }

                            continue;
                        }

                        if (hr == DXGI_ERROR_ACCESS_LOST) {
                            throw RecreateSessionException("AcquireNextFrame: ACCESS_LOST");
                        }

                        CheckHr(hr, "AcquireNextFrame");
                        acquiredFrame = true;

                        CheckHr(
                            desktopResource->QueryInterface(
                                __uuidof(ID3D11Texture2D),
                                reinterpret_cast<void**>(&desktopTex)
                            ),
                            "IDXGIResource->QueryInterface(ID3D11Texture2D)"
                        );

                        D3D11_TEXTURE2D_DESC desc{};
                        desktopTex->GetDesc(&desc);

                        if (desc.Width != dup.width || desc.Height != dup.height) {
                            throw RecreateSessionException("Captured frame size changed");
                        }

                        ID3D11Texture2D* encodeInputTex =
                            CopyCaptureTextureToEncodeGpuCpuFallback(
                                bridge,
                                captureDevice,
                                captureContext,
                                encodeContext,
                                desktopTex
                            );

                        EncodeSlot& slot = ScaleTexture(scaler, encodeInputTex);

                        bool periodicIdr =
                            firstFrame ||
                            (cfg.idrPeriod > 0 && (frameIndex % cfg.idrPeriod) == 0);

                        bool requestedIdr = forceIdrRequested.exchange(false);

                        bool forceIDR = periodicIdr || requestedIdr;
                        bool outputSpsPps = forceIDR;

                        if (!EncodeRegisteredTexture(
                            enc,
                            slot.registered,
                            frameIndex,
                            forceIDR,
                            outputSpsPps
                        )) {
                            SafeRelease(desktopTex);
                            SafeRelease(desktopResource);

                            hr = dup.duplication->ReleaseFrame();
                            if (hr == DXGI_ERROR_ACCESS_LOST) {
                                throw RecreateSessionException("ReleaseFrame: ACCESS_LOST");
                            }

                            acquiredFrame = false;
                            ++frameIndex;
                            continue;
                        }

                        lastEncodedSlot = &slot;

                        firstFrame = false;
                        ++frameIndex;

                        SafeRelease(desktopTex);
                        SafeRelease(desktopResource);

                        hr = dup.duplication->ReleaseFrame();
                        if (hr == DXGI_ERROR_ACCESS_LOST) {
                            throw RecreateSessionException("ReleaseFrame: ACCESS_LOST");
                        }

                        CheckHr(hr, "ReleaseFrame");
                        acquiredFrame = false;

                        Sleep(static_cast<DWORD>(1000.0 / cfg.fps));
                    }
                    catch (...) {
                        SafeRelease(desktopTex);
                        SafeRelease(desktopResource);

                        if (acquiredFrame && dup.duplication) {
                            HRESULT r = dup.duplication->ReleaseFrame();

                            if (FAILED(r) && r != DXGI_ERROR_ACCESS_LOST) {
                                std::cerr << "[WARN] ReleaseFrame during exception failed: 0x"
                                    << std::hex << r << std::dec << "\n";
                            }
                        }

                        throw;
                    }
                }
            }
            catch (const RecreateSessionException& e) {
                std::cerr << "[WARN] Recreating session: " << e.what() << "\n";

                DestroySession(dup, bridge, scaler, enc);

                firstFrame = true;
                lastEncodedSlot = nullptr;

                Sleep(300);
                continue;
            }
        }
    }
    catch (const std::exception& e) {
        std::cerr << "[ERROR] " << e.what() << "\n";
    }

    stopCommandThread.store(true);

    if (commandThread.joinable()) {
        commandThread.detach();
    }

    DestroySession(dup, bridge, scaler, enc);

    SafeRelease(captureContext);
    SafeRelease(captureDevice);

    SafeRelease(encodeContext);
    SafeRelease(encodeDevice);

    return 0;
}