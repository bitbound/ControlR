// Copyright (C) 2009,2010,2011,2012 GlavSoft LLC.
// All rights reserved.
//
//-------------------------------------------------------------------------
// This file is part of the TightVNC software.  Please visit our Web site:
//
//                       http://www.tightvnc.com/
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
//-------------------------------------------------------------------------
//

#ifndef __VNCPASSCRYPT_H__
#define __VNCPASSCRYPT_H__

#include "inttypes.h"
#include <vector>

class VncPassCrypt
{
public:

  VncPassCrypt();
  virtual ~VncPassCrypt();

  static const int VNC_PASSWORD_SIZE = 8;

  void updatePlain(const UINT8 cryptedPass[VNC_PASSWORD_SIZE]);

  // Encrypt the plain password and fills by encrypted values the
  // encryptedPass[8] array.
  static void getEncryptedPass(UINT8 encryptedPass[VNC_PASSWORD_SIZE],
                               const UINT8 plainPassword[VNC_PASSWORD_SIZE]);

  // Decrypt the encrypted password and fills by decrypted values the
  // plainPassword[8] array.
  static void getPlainPass(UINT8 plainPassword[VNC_PASSWORD_SIZE],
                                         const UINT8 encryptedPass[VNC_PASSWORD_SIZE]);

  // Returns true if it's matched.
  bool challengeAndResponseIsValid(const UINT8 challenge[16],
                                   const UINT8 response[16]);
  void clearPlainPass();

private:
  static const UINT8 m_key[];
  // The vector uses to protect a password from containing in the stack
  // because the stack available for a public on a bug.
  std::vector<UINT8> m_plainPassword;
};

#endif // __VNCPASSCRYPT_H__
