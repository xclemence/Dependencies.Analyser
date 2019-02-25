#pragma once
#pragma once

#ifdef BUILDING_DEPENDENCIES_APISETMAP
#define DEPENDENCIES_APISETMAP __declspec(dllexport)
#else
#define DEPENDENCIES_APISETMAP __declspec(dllimport)
#endif

#ifdef _MSC_VER
#pragma warning(disable: 4251)
#endif