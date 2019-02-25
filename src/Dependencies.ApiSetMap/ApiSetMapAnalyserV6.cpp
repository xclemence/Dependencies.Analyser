#include "stdafx.h"

#include "ApiSetMapAnalyserV6.h"

namespace Dependencies::ApiSetMap
{
    std::unique_ptr<std::map<std::string, std::list<RealDll>>> ApiSetMapAnalyserV6::analyse(const API_SET_NAMESPACE_PTR apiSetMap) const
    {
        auto apiSetMapAddress = reinterpret_cast<ULONG_PTR>(apiSetMap);
        auto apiSetEntryIterator = reinterpret_cast<API_SET_NAMESPACE_ENTRY_PTR>((apiSetMap->EntryOffset + apiSetMapAddress));
        std::map<std::string, std::list<RealDll>> results;
        for (unsigned i = 0; i < apiSetMap->Count; i++)
        {
            // Retrieve api min-win contract name
            auto apiSetEntryNameBuffer = reinterpret_cast<wchar_t*>(apiSetMapAddress + apiSetEntryIterator->NameOffset);
            auto entryName = getString(apiSetEntryNameBuffer, apiSetEntryIterator->NameLength);
            std::list<RealDll> mappedDlls;

            // Iterate over all the host dll for this contract
            auto valueEntry = reinterpret_cast<API_SET_VALUE_ENTRY_PTR>(apiSetMapAddress + apiSetEntryIterator->ValueOffset);
            for (unsigned j = 0; j < apiSetEntryIterator->ValueCount; j++)
            {
                // Retrieve dll name implementing the contract
                auto apiSetEntryTargetBuffer = reinterpret_cast<wchar_t*>(apiSetMapAddress + valueEntry->ValueOffset);
                auto targetName = getString(apiSetEntryTargetBuffer, valueEntry->ValueLength);

                std::string aliasName = "";

                // If there's an alias...
                if (valueEntry->NameLength != 0) {
                    wchar_t* apiSetEntryAliasBuffer = reinterpret_cast<wchar_t*>(apiSetMapAddress + valueEntry->NameOffset);
                    std::wstring aliasBuffer(apiSetEntryAliasBuffer, valueEntry->NameLength / sizeof(wchar_t));
                    aliasName = getString(apiSetEntryAliasBuffer, valueEntry->NameLength);
                }

                mappedDlls.push_back(RealDll{ targetName, aliasName });
                valueEntry++;
            }

            results.emplace(entryName, std::move(mappedDlls));
            apiSetEntryIterator++;
        }

        return std::make_unique<std::map<std::string, std::list<RealDll>>>(std::move(results));
    }
}