#include "inttypes.h"
#include "StringHelper.h"
#include <iostream>
#include <Windows.h>
#include <ShlObj_core.h>
#include <string>

using namespace std;

static void writeHelp()
{
    cout << "WinVncPassword" << endl << endl;
    cout << "Encrypts a new password for TightVNC and sets it in the registry." << endl << endl;
    cout << "Syntax: WinVncPassword.exe {password} {machine/user}" << endl << endl;

    cout
        << "Password:" << endl
        << "  The first argument should be an 8-character password.  Passwords "
        << "longer than 8 characters will get trimmed, and only the first 8 "
        << "will get saved to the registry." << endl << endl;

    cout
        << "Machine/User:" << endl
        << "  The second argument should be literally \"machine\" or \"user\". "
        << "If \"machine\", the password will be saved to HKLM and apply to the "
        << "service.  If \"user\", it will be saved to HKCU and only apply when "
        << "tvnserver.exe is run in \"app\" mode." << endl;
}



int main(int argc, char* argv[])
{
    if (argc != 3) 
    {
        cout << "Incorrect number of arguments." << endl;
        writeHelp();
        return 1;
    }

    string regTarget = argv[2];
    trimString(regTarget);

    if (_strcmpi(regTarget.c_str(), "machine"))
    {

    }
    else if (_strcmpi(regTarget.c_str(), "user"))
    {

    }
    else
    {
        cout << "Invalid argument (machine/user)." << endl;
        writeHelp();
        return 1;
    }
}