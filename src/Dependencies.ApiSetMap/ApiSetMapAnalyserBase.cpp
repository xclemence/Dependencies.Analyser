#include "stdafx.h"

#include "ApiSetMapAnalyserBase.h"
#include <codecvt>
#include <vector>

namespace Dependencies::ApiSetMap
{
    std::string ApiSetMapAnalyserBase::getString(const wchar_t* message, const ULONG size) const
    {
        typedef std::codecvt<wchar_t, char, std::mbstate_t> ConverterType;

        std::locale const loc("");
        std::size_t const len = size / sizeof(wchar_t);
        std::vector<char> buffer(len + 1);
        std::use_facet<std::ctype<wchar_t> >(loc).narrow(message, message + size, '_', &buffer.at(0));

        return std::string(&buffer.at(0), &buffer.at(len));

    }
}
