#pragma once

#include <cstdint>
#include <cstring>
#include <string>
#include <vector>
#include <windows.h>

#include "ResourceManager.h"
#include "Errors.h"

namespace bt
{
    // One file inside the payload: where it belongs, relative to the target directory, and a view of
    // its bytes inside the embedded resource.
    struct PayloadEntry
    {
        std::wstring relativePath;
        ByteView content;
    };

    // Parses the archive the packer wrote (see tools\pack-payload.ps1 for the format). Every read is
    // bounds-checked against the end of the resource, so a truncated or wrong-format resource fails
    // with a message instead of reading past the image.
    class PayloadArchive
    {
    public:
        explicit PayloadArchive(ByteView blob)
        {
            Parse(blob);
        }

        const std::vector<PayloadEntry>& Entries() const
        {
            return entries_;
        }

    private:
        static uint32_t ReadU32(const uint8_t*& cursor, const uint8_t* end)
        {
            if (cursor + 4 > end)
            {
                ThrowMsg(L"The embedded payload is truncated");
            }

            uint32_t value = 0;
            std::memcpy(&value, cursor, 4);
            cursor += 4;
            return value;
        }

        static uint64_t ReadU64(const uint8_t*& cursor, const uint8_t* end)
        {
            if (cursor + 8 > end)
            {
                ThrowMsg(L"The embedded payload is truncated");
            }

            uint64_t value = 0;
            std::memcpy(&value, cursor, 8);
            cursor += 8;
            return value;
        }

        static std::wstring Widen(const uint8_t* bytes, size_t count)
        {
            if (count == 0)
            {
                return std::wstring();
            }

            int size = MultiByteToWideChar(CP_UTF8, 0,
                reinterpret_cast<const char*>(bytes), static_cast<int>(count), nullptr, 0);

            std::wstring wide(static_cast<size_t>(size), L'\0');
            MultiByteToWideChar(CP_UTF8, 0, reinterpret_cast<const char*>(bytes),
                static_cast<int>(count), &wide[0], size);
            return wide;
        }

        void Parse(ByteView blob)
        {
            const uint8_t* cursor = blob.data;
            const uint8_t* end = blob.data + blob.size;

            if (blob.size < 8 || std::memcmp(cursor, "BTP1", 4) != 0)
            {
                ThrowMsg(L"The embedded payload has an unexpected format");
            }

            cursor += 4;
            uint32_t count = ReadU32(cursor, end);
            entries_.reserve(count);

            for (uint32_t index = 0; index < count; ++index)
            {
                uint32_t pathLength = ReadU32(cursor, end);
                if (cursor + pathLength > end)
                {
                    ThrowMsg(L"The embedded payload is truncated");
                }

                std::wstring relative = Widen(cursor, pathLength);
                cursor += pathLength;

                uint64_t dataLength = ReadU64(cursor, end);
                if (cursor + dataLength > end)
                {
                    ThrowMsg(L"The embedded payload is truncated");
                }

                entries_.push_back(PayloadEntry{ std::move(relative),
                    ByteView{ cursor, static_cast<size_t>(dataLength) } });
                cursor += dataLength;
            }
        }

        std::vector<PayloadEntry> entries_;
    };
}
