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

namespace UMI.Core.Models;

/// <summary>
/// Ergebnis einer Verification (Post-Import oder Standalone).
/// </summary>
public class VerifyResult
{
    public int TotalFiles { get; set; }
    public int Verified { get; set; }
    public int Missing { get; set; }
    public int SizeMismatch { get; set; }
    public int TimestampMismatch { get; set; }
    public int Corrupt { get; set; }
    public int NoBackup { get; set; }
    public List<VerifyIssue> Issues { get; set; } = new();

    public bool IsClean => Missing == 0 && SizeMismatch == 0 && Corrupt == 0;
}

/// <summary>
/// Einzelnes Verification-Problem.
/// </summary>
public class VerifyIssue
{
    public string FilePath { get; set; } = "";
    public string IssueType { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Message { get; set; } = "";
}
