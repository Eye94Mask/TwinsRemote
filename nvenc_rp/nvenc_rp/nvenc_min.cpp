#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <io.h>
#include <fcntl.h>

#include <iostream>
#include <vector>
#include <stdexcept>
#include <cstdint>
#include <cstdio>
#include "nvEncodeAPI.h"

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")

NV_ENCODE_API_FUNCTION_LIST g_nvenc = {};

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
	// 4byte little-endian length + AU (For Rust)
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

// -----------------------------
// Load NVENC
// -----------------------------

void LoadNvEnc() {
    HMODULE h = LoadLibraryA("nvEncodeAPI64.dll");
    if (!h) throw std::runtime_error("nvEncodeAPI64.dll not found");

	auto create = (NVENCSTATUS(NVENCAPI*)(NV_ENCODE_API_FUNCTION_LIST*))
		GetProcAddress(h, "NvEncodeAPICreateInstance");

	if (!create) throw std::runtime_error("NvEncodeAPICreateInstance not found");

    g_nvenc.version = NV_ENCODE_API_FUNCTION_LIST_VER;

	if (create(&g_nvenc) != NV_ENC_SUCCESS)
		throw std::runtime_error("NvEncodeAPICreateInstance failed");
}

// -----------------------------
// DXGI Duplication Helper
// -----------------------------
struct DuplicationContext {
	IDXGIOutputDuplication* duplication = nullptr;
	UINT width = 0;
	UINT height = 0;
};

static DuplicationContext CreateDuplication(ID3D11Device* device) {
	DuplicationContext out{};

	IDXGIDevice* dxgiDevice = nullptr;
	IDXGIAdapter* adapter = nullptr;
	IDXGIOutput* output = nullptr;
	IDXGIOutput1* output1 = nullptr;

	try {
		CheckHr(device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice)),
			"ID3D11Device->QueryInterface(IDXGIDevice)");

		CheckHr(dxgiDevice->GetAdapter(&adapter), "IDXGIDevice::GetAdapter");

		// Use adapter 0 output
		CheckHr(adapter->EnumOutputs(0, &output), "IDXGIAdapter::EnumOutputs(0)");
		CheckHr(output->QueryInterface(__uuidof(IDXGIOutput1), reinterpret_cast<void**>(&output1)),
			"IDXGIOutput->QueryInterface(IDXGIOutput1)");

		DXGI_OUTPUT_DESC desc{};
		output->GetDesc(&desc);

		CheckHr(output1->DuplicateOutput(device, &out.duplication), "IDXGIOutput1::DuplicateOutput");

		DXGI_OUTDUPL_DESC duplDesc{};
		out.duplication->GetDesc(&duplDesc);
		out.width = duplDesc.ModeDesc.Width;
		out.height = duplDesc.ModeDesc.Height;

		SafeRelease(output1);
		SafeRelease(output);
		SafeRelease(adapter);
		SafeRelease(dxgiDevice);
		return out;
	}
	catch (...) {
		SafeRelease(output1);
		SafeRelease(output);
		SafeRelease(adapter);
		SafeRelease(dxgiDevice);
		if (out.duplication) {
			out.duplication->Release();
			out.duplication = nullptr;
		}

		throw;
	}
}

// -----------------------------
// NVENC encoder helper
// -----------------------------
struct EncoderContext {
	void* encoder = nullptr;
	NV_ENC_OUTPUT_PTR bitstreamBuffer = nullptr;
	uint32_t width = 0;
	uint32_t height = 0;
};

static EncoderContext CreateEncoder(ID3D11Device* device, uint32_t width, uint32_t height) {
	EncoderContext ctx{};
	ctx.width = width;
	ctx.height = height;

	NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS openParams{};
	openParams.version = NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS_VER;
	openParams.device = device;
	openParams.deviceType = NV_ENC_DEVICE_TYPE_DIRECTX;
	openParams.apiVersion = NVENCAPI_VERSION;

	CheckNvEnc(g_nvenc.nvEncOpenEncodeSessionEx(&openParams, &ctx.encoder),
		"nvEncOpenEncodeSessionEx");

	GUID encodeGUID = NV_ENC_CODEC_H264_GUID;
	GUID presetGUID = NV_ENC_PRESET_P3_GUID;
	NV_ENC_TUNING_INFO tuningInfo = NV_ENC_TUNING_INFO_LOW_LATENCY;

	NV_ENC_PRESET_CONFIG presetConfig{};
	presetConfig.version = NV_ENC_PRESET_CONFIG_VER;
	presetConfig.presetCfg.version = NV_ENC_CONFIG_VER;

	CheckNvEnc(
		g_nvenc.nvEncGetEncodePresetConfigEx(
			ctx.encoder,
			encodeGUID,
			presetGUID,
			tuningInfo,
			&presetConfig
		),
		"nvEncGetEncodePresetConfigEx"
	);

	NV_ENC_CONFIG encodeConfig = presetConfig.presetCfg;
	encodeConfig.version = NV_ENC_CONFIG_VER;

	encodeConfig.profileGUID = NV_ENC_H264_PROFILE_BASELINE_GUID;
	encodeConfig.gopLength = 60;
	encodeConfig.frameIntervalP = 1;
	encodeConfig.rcParams.rateControlMode = NV_ENC_PARAMS_RC_CBR;
	encodeConfig.rcParams.averageBitRate = 8 * 1000 * 1000;
	encodeConfig.rcParams.maxBitRate = 10 * 1000 * 1000;
	encodeConfig.rcParams.vbvBufferSize = 8 * 1000 * 1000;
	encodeConfig.rcParams.vbvInitialDelay = 8 * 1000 * 1000;

	// Low Latency
	encodeConfig.encodeCodecConfig.h264Config.repeatSPSPPS = 1;
	encodeConfig.encodeCodecConfig.h264Config.outputAUD = 0;
	encodeConfig.encodeCodecConfig.h264Config.idrPeriod = 60;
	encodeConfig.encodeCodecConfig.h264Config.maxNumRefFrames = 1;

	NV_ENC_INITIALIZE_PARAMS initParams{};
	initParams.version = NV_ENC_INITIALIZE_PARAMS_VER;
	initParams.encodeGUID = encodeGUID;
	initParams.presetGUID = presetGUID;
	initParams.tuningInfo = tuningInfo;
	initParams.encodeConfig = &encodeConfig;

	initParams.encodeWidth = width;
	initParams.encodeHeight = height;
	initParams.darWidth = width;
	initParams.darHeight = height;
	initParams.maxEncodeWidth = width;
	initParams.maxEncodeHeight = height;

	initParams.frameRateNum = 60;
	initParams.frameRateDen = 1;

	initParams.enablePTD = 1;
	initParams.enableEncodeAsync = 0;
	initParams.enableOutputInVidmem = 0;

	// Desktop Duplication B8G8R8A8
	// 色がおかしかったらABGRを試してみる
	initParams.bufferFormat = NV_ENC_BUFFER_FORMAT_ARGB;

	CheckNvEnc(g_nvenc.nvEncInitializeEncoder(ctx.encoder, &initParams),
		"nvEncInitializeEncoder");

	NV_ENC_CREATE_BITSTREAM_BUFFER createBs{};
	createBs.version = NV_ENC_CREATE_BITSTREAM_BUFFER_VER;
	CheckNvEnc(g_nvenc.nvEncCreateBitstreamBuffer(ctx.encoder, &createBs),
		"nvEncCreateBitstreamBuffer");
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

struct ScaleContext {
	ID3D11Texture2D* outputTex = nullptr;
	ID3D11VideoDevice* videoDevice = nullptr;
	ID3D11VideoContext* videoContext = nullptr;
	ID3D11VideoProcessorEnumerator* enumerator = nullptr;
	ID3D11VideoProcessor* processor = nullptr;
	ID3D11VideoProcessorInputView* inputView = nullptr;
	ID3D11VideoProcessorOutputView* outputView = nullptr;
	UINT outWidth = 0;
	UINT outHeight = 0;
};

static ScaleContext CreateScaler(
	ID3D11Device* device,
	ID3D11DeviceContext* context,
	UINT inWidth,
	UINT inHeight,
	UINT outWidth,
	UINT outHeight
) {
	ScaleContext sc{};
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

	D3D11_TEXTURE2D_DESC texDesc{};
	texDesc.Width = outWidth;
	texDesc.Height = outHeight;
	texDesc.MipLevels = 1;
	texDesc.ArraySize = 1;
	texDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
	texDesc.SampleDesc.Count = 1;
	texDesc.Usage = D3D11_USAGE_DEFAULT;
	texDesc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;

	CheckHr(device->CreateTexture2D(&texDesc, nullptr, &sc.outputTex),
		"CreateTexture2D(scale output)");

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

	D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC ovDesc{};
	ovDesc.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;
	ovDesc.Texture2D.MipSlice = 0;

	CheckHr(
		sc.videoDevice->CreateVideoProcessorOutputView(
			sc.outputTex, sc.enumerator, &ovDesc, &sc.outputView),
		"CreateVideoProcessorOutputView"
	);

	return sc;
}

static void ScaleTexture(
	ScaleContext& sc,
	ID3D11Texture2D* inputTex,
	UINT inWidth,
	UINT inHeight
) {
	ID3D11VideoProcessorInputView* inputView = nullptr;

	D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC ivDesc{};
	ivDesc.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
	ivDesc.Texture2D.ArraySlice = 0;
	ivDesc.Texture2D.MipSlice = 0;

	CheckHr(
		sc.videoDevice->CreateVideoProcessorInputView(
			inputTex, sc.enumerator, &ivDesc, &inputView),
		"CreateVideoProcessorInputView"
	);

	RECT srcRect{ 0, 0, (LONG)inWidth, (LONG)inHeight };
	RECT dstRect{ 0, 0, (LONG)sc.outWidth, (LONG)sc.outHeight };

	sc.videoContext->VideoProcessorSetStreamSourceRect(sc.processor, 0, TRUE, &srcRect);
	sc.videoContext->VideoProcessorSetStreamDestRect(sc.processor, 0, TRUE, &dstRect);
	sc.videoContext->VideoProcessorSetOutputTargetRect(sc.processor, TRUE, &dstRect);

	D3D11_VIDEO_PROCESSOR_STREAM stream{};
	stream.Enable = TRUE;
	stream.pInputSurface = inputView;

	CheckHr(
		sc.videoContext->VideoProcessorBlt(
			sc.processor,
			sc.outputView,
			0,
			1,
			&stream
		),
		"VideoProcessorBlt"
	);

	inputView->Release();
}

static void DestroyScaler(ScaleContext& sc) {
	SafeRelease(sc.inputView);
	SafeRelease(sc.outputView);
	SafeRelease(sc.processor);
	SafeRelease(sc.enumerator);
	SafeRelease(sc.videoContext);
	SafeRelease(sc.videoDevice);
	SafeRelease(sc.outputTex);
}

// -----------------------------
// Pre-frame encode
// -----------------------------
static bool EncodeOneTexture(
	EncoderContext& enc,
	ID3D11Texture2D* tex,
	uint64_t frameIndex,
	bool forceIDR
) {
	NV_ENC_REGISTER_RESOURCE reg{};
	reg.version = NV_ENC_REGISTER_RESOURCE_VER;
	reg.resourceType = NV_ENC_INPUT_RESOURCE_TYPE_DIRECTX;
	reg.resourceToRegister = tex;
	reg.width = enc.width;
	reg.height = enc.height;
	reg.pitch = 0;
	reg.bufferFormat = NV_ENC_BUFFER_FORMAT_ARGB;
	reg.bufferUsage = NV_ENC_INPUT_IMAGE;

	CheckNvEnc(g_nvenc.nvEncRegisterResource(enc.encoder, &reg),
		"nvEncRegisterResource");

	NV_ENC_MAP_INPUT_RESOURCE map{};
	map.version = NV_ENC_MAP_INPUT_RESOURCE_VER;
	map.registeredResource = reg.registeredResource;

	try {
		CheckNvEnc(g_nvenc.nvEncMapInputResource(enc.encoder, &map),
			"nvEncMapInputResource");

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

		NVENCSTATUS st = g_nvenc.nvEncEncodePicture(enc.encoder, &pic);
		if (st == NV_ENC_ERR_NEED_MORE_INPUT) {
			g_nvenc.nvEncUnmapInputResource(enc.encoder, map.mappedResource);
			g_nvenc.nvEncUnregisterResource(enc.encoder, reg.registeredResource);
			return false;
		}
		CheckNvEnc(st, "nvEncEncodePicture");

		NV_ENC_LOCK_BITSTREAM lock{};
		lock.version = NV_ENC_LOCK_BITSTREAM_VER;
		lock.outputBitstream = enc.bitstreamBuffer;
		lock.doNotWait = 0;

		CheckNvEnc(g_nvenc.nvEncLockBitstream(enc.encoder, &lock),
			"nvEncLockBitstream");

		try {
			if (!WritePacketToStdout(
				reinterpret_cast<const uint8_t*>(lock.bitstreamBufferPtr),
				static_cast<uint32_t>(lock.bitstreamSizeInBytes))) {
				g_nvenc.nvEncUnlockBitstream(enc.encoder, enc.bitstreamBuffer);
				g_nvenc.nvEncUnmapInputResource(enc.encoder, map.mappedResource);
				g_nvenc.nvEncUnregisterResource(enc.encoder, reg.registeredResource);

				return false;
			}
		}
		catch (...) {
			g_nvenc.nvEncUnlockBitstream(enc.encoder, enc.bitstreamBuffer);
			throw;
		}

		CheckNvEnc(g_nvenc.nvEncUnlockBitstream(enc.encoder, enc.bitstreamBuffer),
			"nvEncUnlockBitstream");
		CheckNvEnc(g_nvenc.nvEncUnmapInputResource(enc.encoder, map.mappedResource),
			"nvEncUnmapInputResource");
		CheckNvEnc(g_nvenc.nvEncUnregisterResource(enc.encoder, reg.registeredResource),
			"nvEncUnregisterResource");

		return true;
	}
	catch (...) {
		if (map.mappedResource) {
			g_nvenc.nvEncUnmapInputResource(enc.encoder, map.mappedResource);
		}
		if (reg.registeredResource) {
			g_nvenc.nvEncUnregisterResource(enc.encoder, reg.registeredResource);
		}

		throw;
	}
}

int main() {
	// stdout -> binary mode
	_setmode(_fileno(stdout), _O_BINARY);
	
	ID3D11Device* device = nullptr;
	ID3D11DeviceContext* context = nullptr;

	DuplicationContext dup{};
	EncoderContext enc{};

	try {
		D3D_FEATURE_LEVEL flOut = D3D_FEATURE_LEVEL_11_0;
		D3D_FEATURE_LEVEL fls[] = {
			D3D_FEATURE_LEVEL_11_1,
			D3D_FEATURE_LEVEL_11_0
		};

		UINT flags = 0;
#ifdef _DEBUG
		// flags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

		CheckHr(
			D3D11CreateDevice(
				nullptr,
				D3D_DRIVER_TYPE_HARDWARE,
				nullptr,
				flags,
				fls,
				static_cast<UINT>(std::size(fls)),
				D3D11_SDK_VERSION,
				&device,
				&flOut,
				&context
			),
			"D3D11CreateDevice"
		);

		std::cerr << "[INFO] D3D11 Device Created\n";

		LoadNvEnc();
		std::cerr << "[INFO] NVENC API Loaded\n";

		dup = CreateDuplication(device);
		std::cerr << "[INFO] Desktop Duplication created: " << dup.width << "x" << dup.height << "\n";

		const UINT ENCODE_W = 1920;
		const UINT ENCODE_H = 1080;

		ScaleContext scaler = CreateScaler(device, context, dup.width, dup.height, ENCODE_W, ENCODE_H);

		enc = CreateEncoder(device, ENCODE_W, ENCODE_H);
		std::cerr << "[INFO] Encoder initialized\n";

		uint64_t frameIndex = 0;
		bool firstFrame = true;

		while (true) {
			IDXGIResource* desktopResource = nullptr;
			ID3D11Texture2D* desktopTex = nullptr;

			DXGI_OUTDUPL_FRAME_INFO frameInfo{};
			HRESULT hr = dup.duplication->AcquireNextFrame(
				16,
				&frameInfo,
				&desktopResource
			);

			if (hr == DXGI_ERROR_WAIT_TIMEOUT) { continue; }

			if (hr == DXGI_ERROR_ACCESS_LOST) {
				throw std::runtime_error("DXGI duplication lost; Recreate Duplication");
			}

			CheckHr(hr, "AcquireNextFrame");

			try {
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
					throw std::runtime_error("Captured frame size changed; Recreate Duplication/Scaler");
				}

				// Send DXGI Duplication Texture to NVENC ()
				ScaleTexture(scaler, desktopTex, dup.width, dup.height);

				bool forceIDR = firstFrame || (frameIndex % 30 == 0);
				EncodeOneTexture(enc, scaler.outputTex, frameIndex, forceIDR);

				firstFrame = false;
				++frameIndex;

				SafeRelease(desktopTex);
				SafeRelease(desktopResource);

				CheckHr(dup.duplication->ReleaseFrame(), "ReleaseFrame");
			}
			catch (...) {
				SafeRelease(desktopTex);
				SafeRelease(desktopResource);
				dup.duplication->ReleaseFrame();
				throw;
			}
		}
		DestroyScaler(scaler);
	}
	catch (const std::exception& e) {
		std::cerr << "[ERROR] " << e.what() << "\n";
	}

	DestroyEncoder(enc);

	if (dup.duplication) {
		dup.duplication->Release();
		dup.duplication = nullptr;
	}

	SafeRelease(context);
	SafeRelease(device);

	return 0;
}