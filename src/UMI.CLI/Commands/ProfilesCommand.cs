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
using Microsoft.Extensions.DependencyInjection;
using UMI.CLI.Helpers;
using UMI.CLI.Resources;
using UMI.Core.Services;

namespace UMI.CLI.Commands;

/// <summary>
/// Command für Config-Profile-Management.
/// </summary>
public static class ProfilesCommand
{
    public static Command Create()
    {
        var command = new Command("profiles", "Manage config profiles");

        var listCommand = new Command("list", "List all available profiles");
        listCommand.SetHandler(async () =>
        {
            await ExecuteListAsync();
        });

        var showCommand = new Command("show", "Show profile contents");
        var nameArgument = new Argument<string>("name", "Profilname");
        showCommand.AddArgument(nameArgument);
        showCommand.SetHandler(async (name) =>
        {
            await ExecuteShowAsync(name);
        }, nameArgument);

        var deleteCommand = new Command("delete", "Delete a profile");
        var deleteNameArgument = new Argument<string>("name", "Profilname");
        deleteCommand.AddArgument(deleteNameArgument);
        deleteCommand.SetHandler(async (name) =>
        {
            await ExecuteDeleteAsync(name);
        }, deleteNameArgument);

        command.AddCommand(listCommand);
        command.AddCommand(showCommand);
        command.AddCommand(deleteCommand);

        return command;
    }

    private static async Task ExecuteListAsync()
    {
        var serviceProvider = await Program.BuildServiceProviderAsync();
        var profileService = serviceProvider.GetRequiredService<ProfileService>();

        ConsoleHelper.WriteBanner("Profiles");

        var profiles = profileService.ListProfiles();

        if (profiles.Count == 0)
        {
            Console.WriteLine(CliStrings.Profiles_None);
            Console.WriteLine($"Erstelle Profile in: {profileService.ProfilesDirectory}");
            return;
        }

        Console.WriteLine($"Verfügbare Profile ({profiles.Count}):");
        Console.WriteLine();

        foreach (var profile in profiles)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {profile.Name}");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(profile.Description))
            {
                Console.Write($" - {profile.Description}");
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    Erstellt: {profile.Created:yyyy-MM-dd HH:mm}");
            Console.ResetColor();
        }
    }

    private static async Task ExecuteShowAsync(string name)
    {
        var serviceProvider = await Program.BuildServiceProviderAsync();
        var profileService = serviceProvider.GetRequiredService<ProfileService>();

        ConsoleHelper.WriteBanner($"Profile: {name}");

        var profile = profileService.LoadProfile(name);

        if (profile == null)
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Profiles_NotFound, name));
            return;
        }

        Console.WriteLine("Profil-Inhalt:");
        Console.WriteLine();
        Console.WriteLine(profile.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static async Task ExecuteDeleteAsync(string name)
    {
        var serviceProvider = await Program.BuildServiceProviderAsync();
        var profileService = serviceProvider.GetRequiredService<ProfileService>();

        ConsoleHelper.WriteBanner($"Profile: {name}");

        if (!profileService.ProfileExists(name))
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Profiles_DoesNotExist, name));
            return;
        }

        Console.Write(string.Format(CliStrings.Profiles_ConfirmDelete, name));
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (answer != "y" && answer != "yes")
        {
            Console.WriteLine(CliStrings.Common_Cancelled);
            return;
        }

        var success = profileService.DeleteProfile(name);

        if (success)
        {
            ConsoleHelper.WriteSuccess(string.Format(CliStrings.Profiles_Deleted, name));
        }
        else
        {
            ConsoleHelper.WriteError(string.Format(CliStrings.Profiles_DeleteError, name));
        }
    }
}
