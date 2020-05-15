#include "stdafx.h"

#include "ApiSetMapAnalyserBase.h"
#include <codecvt>
#include <vector>

namespace Dependencies::ApiSetMap
{
    std::string ApiSetMapAnalyserBase::getString(const wchar_t* message, const ULONG memorySize) const
    {
        typedef std::codecvt<wchar_t, char, std::mbstate_t> ConverterType;

        std::locale const loc("");
        std::size_t const len = memorySize / sizeof(wchar_t);
        std::vector<char> buffer(len + 1);
        std::use_facet<std::ctype<wchar_t>>(loc).narrow(message, std::next(message, len), '_', &buffer.at(0));

        return std::string(&buffer.at(0), &buffer.at(len));
    }
}
