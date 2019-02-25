#pragma once

#include "api.h"

#include <string>
#include <map>
#include <list>
#include <memory>
#include "ApiSetMapAnalyserBase.h"

namespace Dependencies::ApiSetMap
{
    class DEPENDENCIES_APISETMAP ApiSetMapProvider {
    public:
        std::unique_ptr<std::map<std::string, std::list<RealDll>>> getApiSetMap() const;
    private:
        std::unique_ptr<ApiSetMapAnalyserBase> getAnalyser(int apiVersion) const;
    };
}