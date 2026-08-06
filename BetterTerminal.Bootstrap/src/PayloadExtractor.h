#pragma once

#include <filesystem>
#include <fstream>
#include <string>

#include "Log.h"
#include "PayloadArchive.h"
#include "Errors.h"

namespace bt
{
    // Writes every file in the payload into the target directory, recreating each file's folders as
    // it goes, and checks the size that landed on disk against the size that was embedded. A short
    // write - a full disk, a lost permission - is caught here rather than surfacing later as the
    // application failing to start.
    class PayloadExtractor
    {
    public:
        static void Extract(const PayloadArchive& archive, const std::filesystem::path& target)
        {
            for (const PayloadEntry& entry : archive.Entries())
            {
                std::filesystem::path destination = target / entry.relativePath;

                std::error_code ec;
                std::filesystem::create_directories(destination.parent_path(), ec);
                if (ec)
                {
                    ThrowMsg(L"Could not create the folder for " + destination.wstring());
                }

                {
                    std::ofstream output(destination, std::ios::binary | std::ios::trunc);
                    if (!output)
                    {
                        ThrowMsg(L"Could not create the file " + destination.wstring());
                    }

                    if (entry.content.size > 0)
                    {
                        output.write(reinterpret_cast<const char*>(entry.content.data),
                            static_cast<std::streamsize>(entry.content.size));
                    }

                    output.flush();
                    if (!output)
                    {
                        ThrowMsg(L"Could not write the file " + destination.wstring());
                    }
                }

                uintmax_t written = std::filesystem::file_size(destination, ec);
                if (ec || written != entry.content.size)
                {
                    ThrowMsg(L"The file was not written completely: " + destination.wstring());
                }
            }

            Log(L"Extracted " + std::to_wstring(archive.Entries().size()) + L" file(s)");
        }
    };
}
