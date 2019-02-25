#include "stdafx.h"

#include "ApiSetMapProvider.h"
#include "ApiSetMapAnalyserV6.h"

namespace Dependencies::ApiSetMap
{
    std::unique_ptr<std::map<std::string, std::list<RealDll>>> ApiSetMapProvider::getApiSetMap() const
    {
        auto peb = NtCurrentTeb()->ProcessEnvironmentBlock;
        auto apiSetMap = static_cast<API_SET_NAMESPACE_PTR>(peb->Reserved9[0]);

        auto analyser = getAnalyser(apiSetMap->Version);

        if (analyser)
            return analyser->analyse(apiSetMap);

        return nullptr;
    }

    std::unique_ptr<ApiSetMapAnalyserBase> ApiSetMapProvider::getAnalyser(int apiVersion) const
    {
        if (apiVersion == 6)
            return std::make_unique<ApiSetMapAnalyserV6>();

        return nullptr;
    }
}
