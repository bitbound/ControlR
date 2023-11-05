#include "StringHelper.h"
#include <string>

using namespace std;


void trimString(string& str) 
{
    size_t index = 0;
    while (isspace(str[index]))
    {
        str.erase(index, 1);
    }

    index = str.length() - 1;
    while (isspace(str[index]))
    {
        str.erase(index, 1);
        index--;
    }
}