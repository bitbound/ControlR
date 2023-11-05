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

#include "VncPassCrypt.h"
#include "DesCrypt.h"

const UINT8 VncPassCrypt::m_key[] = { 23, 82, 107, 6, 35, 78, 88, 7 };

VncPassCrypt::VncPassCrypt()
: m_plainPassword(8)
{
}

VncPassCrypt::~VncPassCrypt()
{
  clearPlainPass();
}

void VncPassCrypt::updatePlain(const UINT8 cryptedPass[VNC_PASSWORD_SIZE])
{
  DesCrypt desCrypt;
  desCrypt.decrypt(&m_plainPassword.front(), cryptedPass,
                   m_plainPassword.size(), m_key);
}

void VncPassCrypt::getEncryptedPass(UINT8 encryptedPass[VNC_PASSWORD_SIZE],
                                    const UINT8 plainPassword[VNC_PASSWORD_SIZE])
{
  DesCrypt desCrypt;
  desCrypt.encrypt(encryptedPass, plainPassword,
                   VNC_PASSWORD_SIZE, m_key);
}

void VncPassCrypt::getPlainPass(UINT8 plainPassword[VNC_PASSWORD_SIZE],
                                const UINT8 encryptedPass[VNC_PASSWORD_SIZE])
{
  DesCrypt desCrypt;
  desCrypt.decrypt(plainPassword, encryptedPass,
                   VNC_PASSWORD_SIZE, m_key);
}

bool VncPassCrypt::challengeAndResponseIsValid(const UINT8 challenge[16],
                                               const UINT8 response[16])
{
  UINT8 cryptedChallenge[16];
  DesCrypt desCrypt;
  desCrypt.encrypt(cryptedChallenge, challenge,
                   sizeof(cryptedChallenge), &m_plainPassword.front());
  if (memcmp(cryptedChallenge, response, sizeof(cryptedChallenge)) == 0) {
    return true;
  } else {
    return false;
  }
}

void VncPassCrypt::clearPlainPass()
{
  memset(&m_plainPassword.front(), 0, m_plainPassword.size());
}
