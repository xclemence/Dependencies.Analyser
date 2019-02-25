#pragma once

namespace Dependencies::ApiSetMap
{
    typedef struct _API_SET_NAMESPACE {
        ULONG Version;
        ULONG Size;
        ULONG Flags;
        ULONG Count;
        ULONG EntryOffset;
        ULONG HashOffset;
        ULONG HashFactor;
    } API_SET_NAMESPACE, *API_SET_NAMESPACE_PTR;

    typedef struct _API_SET_HASH_ENTRY {
        ULONG Hash;
        ULONG Index;
    } API_SET_HASH_ENTRY, *REF_API_SET_HASH_ENTRY;

    typedef struct _API_SET_NAMESPACE_ENTRY {
        ULONG Flags;
        ULONG NameOffset;
        ULONG NameLength;
        ULONG HashedLength;
        ULONG ValueOffset;
        ULONG ValueCount;
    } API_SET_NAMESPACE_ENTRY, *API_SET_NAMESPACE_ENTRY_PTR;

    typedef struct _API_SET_VALUE_ENTRY {
        ULONG Flags;
        ULONG NameOffset;
        ULONG NameLength;
        ULONG ValueOffset;
        ULONG ValueLength;
    } API_SET_VALUE_ENTRY, *API_SET_VALUE_ENTRY_PTR;
}