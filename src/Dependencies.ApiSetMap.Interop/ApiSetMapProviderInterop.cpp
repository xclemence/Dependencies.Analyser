#include "stdafx.h"
#include "ApiSetMapProviderInterop.h"

#include <ApiSetMapProvider.h>

namespace Dependencies::ApiSetMapInterop
{
    IDictionary<String^, IList<TargetDll>^>^ ApiSetMapProviderInterop::GetApiSetMap()
    {
        ApiSetMap::ApiSetMapProvider provider;

        auto natievMap = provider.getApiSetMap();

        auto result = gcnew Dictionary<String^, IList<TargetDll>^>();

        for (auto& item : *natievMap)
        {
            auto list = gcnew List<TargetDll>();

            for (auto& realDll : item.second)
            {
                TargetDll targetDll;
                targetDll.alias = gcnew String(realDll.alias.c_str());
                targetDll.name = gcnew String(realDll.name.c_str());

                list->Add(targetDll);
            }

            result->Add(gcnew String(item.first.c_str()), list);
        }

        return result;
    }
}
