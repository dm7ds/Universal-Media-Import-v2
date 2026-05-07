// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This file is part of UMI - Universal Media Import.
//
//     UMI - Universal Media Import is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     UMI - Universal Media Import is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with UMI - Universal Media Import.  If not, see <http://www.gnu.org/licenses/>.

using System.CommandLine;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.Core.Utilities;

namespace UMI.CLI.Commands;

public class TestCameraCommand
{
    public static Command Create()
    {
        var command = new Command("test-camera", "Test camera detection for a video");

        var videoOption = new Option<string>(
            "--video",
            description: "Path to video")
        {
            IsRequired = true
        };
        command.AddOption(videoOption);

        command.SetHandler(async (context) =>
        {
            var videoPath = context.ParseResult.GetValueForOption(videoOption)!;
            var verbose   = context.ParseResult.GetValueForOption(Program.VerboseOption);

            ConsoleHelper.WriteBanner(CliStrings.TestCamera_Banner);
            Console.WriteLine();

            if (!File.Exists(videoPath))
            {
                ConsoleHelper.WriteError(string.Format(CliStrings.TestCamera_VideoNotFound, videoPath));
                return;
            }

            Console.WriteLine($"📹 Video: {Path.GetFileName(videoPath)}");
            Console.WriteLine($"📂 Pfad:  {videoPath}");
            Console.WriteLine();

            if (verbose)
            {
                ConsoleHelper.WriteSeparator();
                Console.WriteLine("🔍 HEADER-ANALYSE (erste 2048 Bytes)");
                ConsoleHelper.WriteSeparator();

                using var fs = File.OpenRead(videoPath);
                byte[] buffer = new byte[2048];
                int read = fs.Read(buffer, 0, buffer.Length);

                var ascii = System.Text.Encoding.ASCII.GetString(
                    buffer.Select(b => b >= 32 && b <= 126 ? b : (byte)' ').ToArray());

                for (int i = 0; i < ascii.Length; i += 80)
                {
                    int len = Math.Min(80, ascii.Length - i);
                    string line = ascii.Substring(i, len).Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                        Console.WriteLine($"  {line}");
                }
                ConsoleHelper.WriteSeparator();
                Console.WriteLine();
            }

            var parser = new Mp4Parser(null);

            Console.WriteLine("🔍 Analysiere MP4-Header...");
            var cameraInfo = await parser.DetectCameraModelAsync(videoPath);

            Console.WriteLine();
            ConsoleHelper.WriteSeparator();
            Console.WriteLine("📷 KAMERA-INFORMATION");
            ConsoleHelper.WriteSeparator();
            Console.WriteLine($"  Hersteller:  {cameraInfo.Manufacturer}");
            Console.WriteLine($"  Modell:      {cameraInfo.Model}");

            if (!string.IsNullOrEmpty(cameraInfo.ModelCode))
                Console.WriteLine($"  Modell-Code: {cameraInfo.ModelCode}");

            if (!string.IsNullOrEmpty(cameraInfo.FirmwareVersion))
                Console.WriteLine($"  Firmware:    {cameraInfo.FirmwareVersion}");

            ConsoleHelper.WriteSeparator();
            Console.WriteLine();

            Console.WriteLine("🔍 Prüfe EIS-Status...");
            var eisResult = await parser.DetectEisStatusAsync(videoPath);

            Console.WriteLine();
            ConsoleHelper.WriteSeparator();
            Console.WriteLine("🎯 EIS-STATUS");
            ConsoleHelper.WriteSeparator();

            switch (eisResult.Status)
            {
                case EisStatus.StabilizationOn:
                    ConsoleHelper.WriteError("  Status: EIS AN (Stabilisiert)");
                    break;
                case EisStatus.StabilizationOff:
                    ConsoleHelper.WriteSuccess("  Status: EIS AUS (Raw Gyro)");
                    break;
                case EisStatus.NoMetadataTrack:
                    ConsoleHelper.WriteWarning("  Status: Kein Metadata-Track");
                    break;
                default:
                    Console.WriteLine("  Status: ? UNBEKANNT");
                    break;
            }

            Console.WriteLine($"  Info:   {eisResult.Message}");
            ConsoleHelper.WriteSeparator();
            Console.WriteLine();

        });

        return command;
    }
}
