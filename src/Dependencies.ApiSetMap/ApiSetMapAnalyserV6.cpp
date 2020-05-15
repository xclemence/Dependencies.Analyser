#include "stdafx.h"

#include "ApiSetMapAnalyserV6.h"

#include <vector> 

namespace Dependencies::ApiSetMap
{
    std::unique_ptr<std::map<std::string, std::list<RealDll>>> ApiSetMapAnalyserV6::analyse(const API_SET_NAMESPACE_PTR apiSetMap) const
    {
        std::map<std::string, std::list<RealDll>> results;
        
        [[gsl::suppress(type.1)]]
        {
            auto apiSetMapAddress = reinterpret_cast<ULONG_PTR>(apiSetMap);

            auto apiSetEntryIterator = reinterpret_cast<API_SET_NAMESPACE_ENTRY_PTR>(apiSetMap->EntryOffset + apiSetMapAddress);
            std::vector<API_SET_NAMESPACE_ENTRY> apiSetEntries(apiSetEntryIterator, std::next(apiSetEntryIterator, apiSetMap->Count));

            for (const auto& apiSetEntry : apiSetEntries)
            {
                // Retrieve api min-win contract name
                auto apiSetEntryNameBuffer = reinterpret_cast<const wchar_t*>(apiSetMapAddress + apiSetEntry.NameOffset);

                auto entryName = getString(apiSetEntryNameBuffer, apiSetEntry.NameLength);
                std::list<RealDll> mappedDlls;

                // Iterate over all the host dll for this contract
                auto valueEntry = reinterpret_cast<API_SET_VALUE_ENTRY_PTR>(apiSetMapAddress + apiSetEntry.ValueOffset);

                std::vector<API_SET_VALUE_ENTRY> data(valueEntry, std::next(valueEntry, apiSetEntry.ValueCount));

                for (const auto& item : data)
                {
                    // Retrieve dll name implementing the contract
                    auto apiSetEntryTargetBuffer = reinterpret_cast<const wchar_t*>(apiSetMapAddress + item.ValueOffset);
                    auto targetName = getString(apiSetEntryTargetBuffer, valueEntry->ValueLength);

                    std::string aliasName = "";

                    // If there's an alias...
                    if (valueEntry->NameLength != 0) {
                        auto apiSetEntryAliasBuffer = reinterpret_cast<const wchar_t*>(apiSetMapAddress + item.NameOffset);
                        aliasName = getString(apiSetEntryAliasBuffer, valueEntry->NameLength);
                    }

                    mappedDlls.push_back(RealDll{ targetName, aliasName });
                }

                results.emplace(entryName, std::move(mappedDlls));
            }

            return std::make_unique<std::map<std::string, std::list<RealDll>>>(std::move(results));
        }
    }
}