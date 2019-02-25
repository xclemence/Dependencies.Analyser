#include "stdafx.h"

#include "ApiSetMapAnalyserBase.h"

namespace Dependencies::ApiSetMap
{
    std::string ApiSetMapAnalyserBase::getString(const wchar_t* message, const ULONG size) const
    {
        std::wstring wString(message, size / sizeof(wchar_t));
        return std::string(wString.begin(), wString.end());
    }
}
