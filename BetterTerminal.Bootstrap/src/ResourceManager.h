#pragma once

#include <cstdint>
#include <windows.h>

#include "Errors.h"

namespace bt
{
    // A non-owning view over a block of bytes. The embedded resource stays mapped in the image for
    // the life of the process, so nothing here copies it - the extractor writes straight from it.
    struct ByteView
    {
        const uint8_t* data = nullptr;
        size_t size = 0;
    };

    // Reads an embedded resource out of this module's own image: find it, size it, and hand back a
    // view. Loading a resource does not allocate a separate copy - LockResource returns a pointer
    // into the already-mapped image.
    class ResourceManager
    {
    public:
        explicit ResourceManager(HMODULE module) : module_(module)
        {
        }

        ByteView Load(int resourceId) const
        {
            HRSRC info = FindResourceW(module_, MAKEINTRESOURCEW(resourceId), RT_RCDATA);
            if (info == nullptr)
            {
                ThrowWin(L"The embedded payload resource was not found", GetLastError());
            }

            DWORD size = SizeofResource(module_, info);
            HGLOBAL handle = LoadResource(module_, info);
            if (handle == nullptr || size == 0)
            {
                ThrowWin(L"The embedded payload resource could not be loaded", GetLastError());
            }

            const void* pointer = LockResource(handle);
            if (pointer == nullptr)
            {
                ThrowMsg(L"The embedded payload resource is empty");
            }

            return ByteView{ static_cast<const uint8_t*>(pointer), static_cast<size_t>(size) };
        }

    private:
        HMODULE module_;
    };
}
