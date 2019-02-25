#pragma once

#include "ApiSetMapAnalyserBase.h"

namespace Dependencies::ApiSetMap
{
    class ApiSetMapAnalyserV6 : public ApiSetMapAnalyserBase {
    public:
        std::unique_ptr<std::map<std::string, std::list<RealDll>>> analyse(const API_SET_NAMESPACE_PTR apiSetMap) const override;
    };
}
