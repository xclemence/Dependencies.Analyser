#pragma once

#include "Api.h"

#include <memory>
#include <string>
#include <map>
#include <list>

#include "ApiSetModels.h"

namespace Dependencies::ApiSetMap
{
    struct DEPENDENCIES_APISETMAP RealDll {
        std::string name;
        std::string alias;
    };

    class ApiSetMapAnalyserBase {
    public:
        virtual std::unique_ptr<std::map<std::string, std::list<RealDll>>> analyse(const API_SET_NAMESPACE_PTR apiSetMap) const = 0;
    protected:
        std::string getString(const wchar_t* message, const ULONG size) const;
    };
}

