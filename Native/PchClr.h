// PchClr.h : include file for standard system include files,
// or project specific include files that are used frequently,
// but are changed infrequently

#pragma once
#include <CodeAnalysis/sourceannotations.h>
// cannot #include <stdexcept> due to link time incompatibility between STL and C++/CLI COFF symbols

#define NOMINMAX // suppress windows macros for min and max which interfere which complicate use of std::min() and std::max()
#include <Windows.h>

#include "turbojpeg.h"