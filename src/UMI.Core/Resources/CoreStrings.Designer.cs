// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable

namespace UMI.Core.Resources;

/// <summary>
///   A strongly-typed resource class, for looking up localized core strings.
/// </summary>
[global::System.CodeDom.Compiler.GeneratedCodeAttribute("PublicResXFileCodeGenerator", "4.0.0.0")]
[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
public static class CoreStrings
{
    private static global::System.Resources.ResourceManager? resourceMan;

    private static global::System.Globalization.CultureInfo? resourceCulture;

    /// <summary>
    ///   Returns the cached ResourceManager instance used by this class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (resourceMan is null)
            {
                global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager(
                    "UMI.Core.Resources.CoreStrings", typeof(CoreStrings).Assembly);
                resourceMan = temp;
            }
            return resourceMan;
        }
    }

    /// <summary>
    ///   Overrides the current thread's CurrentUICulture property for all
    ///   resource lookups using this strongly typed resource class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    public static global::System.Globalization.CultureInfo? Culture
    {
        get => resourceCulture;
        set => resourceCulture = value;
    }

    /// <summary>
    ///   Looks up a localized string: display name for AppMode.Dau.
    /// </summary>
    public static string AppMode_Dau
        => ResourceManager.GetString("AppMode_Dau", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display name for AppMode.Simple.
    /// </summary>
    public static string AppMode_Simple
        => ResourceManager.GetString("AppMode_Simple", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display name for AppMode.Advanced.
    /// </summary>
    public static string AppMode_Advanced
        => ResourceManager.GetString("AppMode_Advanced", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for GPS injection feature.
    /// </summary>
    public static string Feature_GpsInjection
        => ResourceManager.GetString("Feature_GpsInjection", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for Gyroflow stabilization feature.
    /// </summary>
    public static string Feature_Gyroflow
        => ResourceManager.GetString("Feature_Gyroflow", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for burst detection feature.
    /// </summary>
    public static string Feature_BurstDetection
        => ResourceManager.GetString("Feature_BurstDetection", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for metadata backup feature.
    /// </summary>
    public static string Feature_MetadataBackup
        => ResourceManager.GetString("Feature_MetadataBackup", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for EIS detection feature.
    /// </summary>
    public static string Feature_EisDetection
        => ResourceManager.GetString("Feature_EisDetection", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for post-processing feature.
    /// </summary>
    public static string Feature_PostProcess
        => ResourceManager.GetString("Feature_PostProcess", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for rename videos feature.
    /// </summary>
    public static string Feature_RenameVideos
        => ResourceManager.GetString("Feature_RenameVideos", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for GoPro rename feature.
    /// </summary>
    public static string Feature_GoProRename
        => ResourceManager.GetString("Feature_GoProRename", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for GPS injection feature.
    /// </summary>
    public static string Feature_GpsInjection_Desc
        => ResourceManager.GetString("Feature_GpsInjection_Desc", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for Gyroflow stabilization feature.
    /// </summary>
    public static string Feature_Gyroflow_Desc
        => ResourceManager.GetString("Feature_Gyroflow_Desc", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for burst detection feature.
    /// </summary>
    public static string Feature_BurstDetection_Desc
        => ResourceManager.GetString("Feature_BurstDetection_Desc", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for metadata backup feature.
    /// </summary>
    public static string Feature_MetadataBackup_Desc
        => ResourceManager.GetString("Feature_MetadataBackup_Desc", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for EIS detection feature.
    /// </summary>
    public static string Feature_EisDetection_Desc
        => ResourceManager.GetString("Feature_EisDetection_Desc", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for post-processing feature.
    /// </summary>
    public static string Feature_PostProcess_Desc
        => ResourceManager.GetString("Feature_PostProcess_Desc", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for rename videos feature.
    /// </summary>
    public static string Feature_RenameVideos_Desc
        => ResourceManager.GetString("Feature_RenameVideos_Desc", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for GoPro rename feature.
    /// </summary>
    public static string Feature_GoProRename_Desc
        => ResourceManager.GetString("Feature_GoProRename_Desc", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: display label for generate thumbnails feature.
    /// </summary>
    public static string Feature_GenerateThumbnails
        => ResourceManager.GetString("Feature_GenerateThumbnails", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string: tooltip description for generate thumbnails feature.
    /// </summary>
    public static string Feature_GenerateThumbnails_Desc
        => ResourceManager.GetString("Feature_GenerateThumbnails_Desc", resourceCulture) ?? string.Empty;
}
