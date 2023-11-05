#pragma once

extern "C" {
	__declspec(dllexport) UINT8* EncryptVncPassword(char* password, int& size);
}
