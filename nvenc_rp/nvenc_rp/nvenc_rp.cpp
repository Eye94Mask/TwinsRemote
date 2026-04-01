#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <iostream>
#include <vector>

#include <NvEncoder/NvEncoderD3D11.h>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
// ================= RTP =================

void write_nal(uint8_t* data, int size) {
    fwrite(data, 1, size, stdout);
    fflush(stdout);
}

uint16_t seq = 0;
uint32_t timestamp = 0;

void send_rtp_packet(SOCKET sock, sockaddr_in& addr, uint8_t* payload, int payload_size, bool marker) {
    uint8_t packet[1500];

    packet[0] = 0x80;
    packet[1] = 96 | (marker ? 0x80 : 0);
    *(uint16_t*)&packet[2] = htons(seq++);
    *(uint32_t*)&packet[4] = htonl(timestamp);
    *(uint32_t*)&packet[8] = htonl(1234);

    memcpy(packet + 12, payload, payload_size);

    sendto(sock, (char*)packet, payload_size + 12, 0,
        (sockaddr*)&addr, sizeof(addr));
}

// NAL分割
void send_nal_rtp(SOCKET sock, sockaddr_in& addr, uint8_t* data, int size) {
    int i = 0;
    while (i + 4 < size) {
        if (data[i] == 0x00 && data[i + 1] == 0x00 &&
            data[i + 2] == 0x00 && data[i + 3] == 0x01) {

            int start = i + 4;
            i = start;

            while (i + 4 < size &&
                !(data[i] == 0x00 && data[i + 1] == 0x00 &&
                    data[i + 2] == 0x00 && data[i + 3] == 0x01)) {
                i++;
            }

            int nal_size = i - start;
            uint8_t* nal = data + start;

            const int mtu = 1200;

            if (nal_size < mtu) {
                send_rtp_packet(sock, addr, nal, nal_size, true);
            }
            else {
                uint8_t nal_header = nal[0];
                uint8_t fu_indicator = (nal_header & 0xE0) | 28;

                int pos = 1;
                bool start_bit = true;

                while (pos < nal_size) {
                    int chunk = min(mtu - 2, nal_size - pos);

                    uint8_t fu_header =
                        (start_bit ? 0x80 : 0x00) |
                        ((pos + chunk >= nal_size) ? 0x40 : 0x00) |
                        (nal_header & 0x1F);

                    uint8_t buf[1500];
                    buf[0] = fu_indicator;
                    buf[1] = fu_header;

                    memcpy(buf + 2, nal + pos, chunk);

                    send_rtp_packet(sock, addr, buf, chunk + 2,
                        (pos + chunk >= nal_size));

                    pos += chunk;
                    start_bit = false;
                }
            }
        }
        else {
            i++;
        }
    }

    timestamp += 3000; // 90kHz / 30fps
}

// ================= MAIN =================

int main() {
    HMODULE cuda = LoadLibraryA("nvcuda.dll");
    if (!cuda) {
        printf("Failed to load nvcuda.dll\n");
        return -1;
    }

    // ---- Winsock ----
    //CUdevice cuDevice = 0;
    //CUcontext cuContext = nullptr;

    //cuInit(0);
    //cuDeviceGet(&cuDevice, 0);

    //CUctxCreateParams params = {};

    //cuCtxCreate(&cuContext, &params, 0, cuDevice);

    //CUresult res = cuInit(0);
    //printf("cuInit = %d\n", res);

    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);

    SOCKET sock = socket(AF_INET, SOCK_DGRAM, 0);

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_port = htons(5004);
    inet_pton(AF_INET, "127.0.0.1", &addr.sin_addr);

    // ---- D3D11 ----
    ID3D11Device* device = nullptr;
    ID3D11DeviceContext* context = nullptr;

    D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT,
        nullptr,
        0,
        D3D11_SDK_VERSION,
        &device,
        nullptr,
        &context
    );

    // ---- DXGI Duplication ----
    IDXGIDevice* dxgiDevice = nullptr;
    device->QueryInterface(__uuidof(IDXGIDevice), (void**)&dxgiDevice);

    IDXGIAdapter* adapter = nullptr;
    dxgiDevice->GetAdapter(&adapter);

    IDXGIOutput* output = nullptr;
    adapter->EnumOutputs(0, &output);

    IDXGIOutput1* output1 = nullptr;
    output->QueryInterface(__uuidof(IDXGIOutput1), (void**)&output1);

    IDXGIOutputDuplication* duplication = nullptr;
    output1->DuplicateOutput(device, &duplication);

    // ---- NVENC ----
    int maxWidth = 3840;
    int maxHeight = 2160;
    int width = 1280;
    int height = 720;

    NvEncoderD3D11 encoder(device, width, height, NV_ENC_BUFFER_FORMAT_ARGB);

    NV_ENC_INITIALIZE_PARAMS initParams = {};
    initParams.version = NV_ENC_INITIALIZE_PARAMS_VER;

    NV_ENC_CONFIG config = {};
    config.version = NV_ENC_CONFIG_VER;

    initParams.encodeConfig = &config;

    initParams.encodeGUID = NV_ENC_CODEC_H264_GUID;

    initParams.encodeWidth = width;
    initParams.encodeHeight = height;

    initParams.darWidth = width;
    initParams.darHeight = height;

    initParams.frameRateNum = 60;
    initParams.frameRateDen = 1;

    initParams.maxEncodeWidth = width;
    initParams.maxEncodeHeight = height;

    config.profileGUID = NV_ENC_H264_PROFILE_BASELINE_GUID;
    config.gopLength = 60;
    config.frameIntervalP = 1;

    config.rcParams.rateControlMode = NV_ENC_PARAMS_RC_CBR;
    config.rcParams.averageBitRate = 4000000;
    config.rcParams.maxBitRate = 4000000;

    config.encodeCodecConfig.h264Config.level = NV_ENC_LEVEL_H264_52;
    try {
        encoder.CreateEncoder(&initParams);
        printf("Encoder created\n");
    }
    catch (const std::exception& e) {
        printf("CreateEncoder failed: %s\n", e.what());
        // return -1;
    }

    // ---- Capture Loop ----
    while (true) {
        IDXGIResource* resource = nullptr;
        DXGI_OUTDUPL_FRAME_INFO frameInfo;

        if (duplication->AcquireNextFrame(16, &frameInfo, &resource) == S_OK) {
            ID3D11Texture2D* tex = nullptr;
            resource->QueryInterface(__uuidof(ID3D11Texture2D), (void**)&tex);

            const NvEncInputFrame* input = encoder.GetNextInputFrame();

            context->CopyResource(
                (ID3D11Texture2D*)input->inputPtr,
                tex
            );

            std::vector<NvEncOutputFrame> packets;
            printf("packets = %zu\n", packets.size());
            encoder.EncodeFrame(packets);

            for (auto& pkt : packets) {
                printf("frame size = %zu\n", pkt.frame.size());
                write_nal(pkt.frame.data(), pkt.frame.size());
                // send_nal_rtp(sock, addr, pkt.frame.data(), pkt.frame.size());
            }

            duplication->ReleaseFrame();

            tex->Release();
            resource->Release();
        }
    }

    return 0;
}