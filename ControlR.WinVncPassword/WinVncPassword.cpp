#include "TightVNC\VncPassCrypt.h"
#include "WinVncPassword.h"
#include "StringHelper.h"
#include <iostream>
#include <string>
#include "combaseapi.h"
#include "TightVnc/VncPassCrypt.h"

__declspec(dllexport) UINT8* EncryptVncPassword(char* password, int& size)
{
	size = VncPassCrypt::VNC_PASSWORD_SIZE;

	std::string strPassword = password;
	TrimString(strPassword);

	int passwordLen = strPassword.length();

	UINT8* plainText = new UINT8[size];

	for (int i = 0; i < size; i++)
	{
		if (i < passwordLen) {
			plainText[i] = static_cast<UINT8>(strPassword[i]);
		}
		else {
			plainText[i] = 0;
		}
	}

	UINT8* encryptedPassword = (UINT8*)CoTaskMemAlloc(size);

	VncPassCrypt::getEncryptedPass(encryptedPassword, plainText);

	return encryptedPassword;
}