#include "TightVNC\VncPassCrypt.h"
#include "WinVncPassword.h"
#include "StringHelper.h"
#include <iostream>
#include <string>
#include "combaseapi.h"


__declspec(dllexport) UINT8* EncryptVncPassword(char* password, int& size) {
	std::string strPassword = password;
	trimString(strPassword);

	UINT8 plainText[8] = {};
	int maxPasswdLen = 8;
	int passwdLen = static_cast<int>(strPassword.length());
	size = min(maxPasswdLen, passwdLen);

	UINT8* encryptedPassword = (UINT8*)CoTaskMemAlloc(size);
	
	//UINT8* encryptedPassword = new UINT8[size];

	for (int i = 0; i < size; i++)
	{
		plainText[i] = static_cast<UINT8>(strPassword[i]);
	}

	VncPassCrypt::getEncryptedPass(encryptedPassword, plainText);

	return encryptedPassword;
}