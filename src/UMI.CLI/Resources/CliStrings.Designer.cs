// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable

namespace UMI.CLI.Resources;

/// <summary>
///   A strongly-typed resource class, for looking up localized CLI strings.
/// </summary>
[global::System.CodeDom.Compiler.GeneratedCodeAttribute("PublicResXFileCodeGenerator", "4.0.0.0")]
[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
public static class CliStrings
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
                    "UMI.CLI.Resources.CliStrings", typeof(CliStrings).Assembly);
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
    ///   Looks up a localized string similar to "UMI - Project Archive".
    /// </summary>
    public static string Archive_Banner
        => ResourceManager.GetString("Archive_Banner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Archive created: {0}".
    /// </summary>
    public static string Archive_Created
        => ResourceManager.GetString("Archive_Created", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Use --include-delivery to archive them as well.".
    /// </summary>
    public static string Archive_DeliveryHint
        => ResourceManager.GetString("Archive_DeliveryHint", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Note: DVR exports were NOT archived.".
    /// </summary>
    public static string Archive_DeliveryNotIncluded
        => ResourceManager.GetString("Archive_DeliveryNotIncluded", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Add Camera".
    /// </summary>
    public static string Camera_AddBanner
        => ResourceManager.GetString("Camera_AddBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "'umi config camera add' requires an interactive terminal.".
    /// </summary>
    public static string Camera_AddNeedInteractive
        => ResourceManager.GetString("Camera_AddNeedInteractive", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera {0} is already {1}.".
    /// </summary>
    public static string Camera_AlreadyState
        => ResourceManager.GetString("Camera_AlreadyState", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Card Assignment".
    /// </summary>
    public static string Camera_AssignBanner
        => ResourceManager.GetString("Camera_AssignBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Currently assigned: {0} SD card(s), {1} MTP device(s)".
    /// </summary>
    public static string Camera_AssignCurrent
        => ResourceManager.GetString("Camera_AssignCurrent", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Done".
    /// </summary>
    public static string Camera_AssignDone_Label
        => ResourceManager.GetString("Camera_AssignDone_Label", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Enter SD card manually (VSN)".
    /// </summary>
    public static string Camera_AssignManualVsn
        => ResourceManager.GetString("Camera_AssignManualVsn", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Assign MTP device".
    /// </summary>
    public static string Camera_AssignMtp
        => ResourceManager.GetString("Camera_AssignMtp", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "'umi config camera assign' requires an interactive terminal.".
    /// </summary>
    public static string Camera_AssignNeedInteractive
        => ResourceManager.GetString("Camera_AssignNeedInteractive", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No changes.".
    /// </summary>
    public static string Camera_AssignNoChanges
        => ResourceManager.GetString("Camera_AssignNoChanges", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Remove assignment".
    /// </summary>
    public static string Camera_AssignRemove
        => ResourceManager.GetString("Camera_AssignRemove", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Changes saved".
    /// </summary>
    public static string Camera_AssignSaved
        => ResourceManager.GetString("Camera_AssignSaved", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Scan SD card (inserted)".
    /// </summary>
    public static string Camera_AssignScanSd
        => ResourceManager.GetString("Camera_AssignScanSd", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "What would you like to do?".
    /// </summary>
    public static string Camera_AssignWhat
        => ResourceManager.GetString("Camera_AssignWhat", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Available cameras:".
    /// </summary>
    public static string Camera_AvailableCameras
        => ResourceManager.GetString("Camera_AvailableCameras", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Configured cameras:".
    /// </summary>
    public static string Camera_ConfiguredCameras
        => ResourceManager.GetString("Camera_ConfiguredCameras", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Really remove camera {0} ({1})? [y/N]: ".
    /// </summary>
    public static string Camera_ConfirmRemove
        => ResourceManager.GetString("Camera_ConfirmRemove", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera {0} ({1}) disabled".
    /// </summary>
    public static string Camera_Disabled
        => ResourceManager.GetString("Camera_Disabled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera {0} ({1}) enabled".
    /// </summary>
    public static string Camera_Enabled
        => ResourceManager.GetString("Camera_Enabled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  {0} camera(s) configured ({1} active)".
    /// </summary>
    public static string Camera_FooterTotal
        => ResourceManager.GetString("Camera_FooterTotal", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Camera Overview".
    /// </summary>
    public static string Camera_ListBanner
        => ResourceManager.GetString("Camera_ListBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Label (optional):".
    /// </summary>
    public static string Camera_ManualLabelPrompt
        => ResourceManager.GetString("Camera_ManualLabelPrompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Volume Serial Number (e.g. A4F2-8B31):".
    /// </summary>
    public static string Camera_ManualVsnPrompt
        => ResourceManager.GetString("Camera_ManualVsnPrompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "MTP device is already registered for {0}.".
    /// </summary>
    public static string Camera_MtpAlreadyRegistered
        => ResourceManager.GetString("Camera_MtpAlreadyRegistered", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "MTP device {0}{1} assigned to {2} [Fixed]".
    /// </summary>
    public static string Camera_MtpAssigned
        => ResourceManager.GetString("Camera_MtpAssigned", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Error detecting MTP devices: {0}".
    /// </summary>
    public static string Camera_MtpDetectError
        => ResourceManager.GetString("Camera_MtpDetectError", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "MTP device {0} removed".
    /// </summary>
    public static string Camera_MtpDeviceRemoved
        => ResourceManager.GetString("Camera_MtpDeviceRemoved", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No MTP devices connected.".
    /// </summary>
    public static string Camera_MtpNoDevices
        => ResourceManager.GetString("Camera_MtpNoDevices", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "MTP service not available (Windows only).".
    /// </summary>
    public static string Camera_MtpNotAvailable
        => ResourceManager.GetString("Camera_MtpNotAvailable", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Which MTP device to assign?".
    /// </summary>
    public static string Camera_MtpWhichDevice
        => ResourceManager.GetString("Camera_MtpWhichDevice", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Remove Camera".
    /// </summary>
    public static string Camera_RemoveBanner
        => ResourceManager.GetString("Camera_RemoveBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Assigned cards/devices were not removed. Use 'umi config cards remove' to clean up.".
    /// </summary>
    public static string Camera_RemoveCleanupHint
        => ResourceManager.GetString("Camera_RemoveCleanupHint", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera {0} still has {1} assigned.".
    /// </summary>
    public static string Camera_RemoveHasDevices
        => ResourceManager.GetString("Camera_RemoveHasDevices", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "{0} MTP device(s)".
    /// </summary>
    public static string Camera_RemoveMtpDevices
        => ResourceManager.GetString("Camera_RemoveMtpDevices", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No assignments present.".
    /// </summary>
    public static string Camera_RemoveNoAssignments
        => ResourceManager.GetString("Camera_RemoveNoAssignments", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "{0} SD card(s)".
    /// </summary>
    public static string Camera_RemoveSdCards
        => ResourceManager.GetString("Camera_RemoveSdCards", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Which assignments to remove?".
    /// </summary>
    public static string Camera_RemoveWhich
        => ResourceManager.GetString("Camera_RemoveWhich", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera {0} ({1}) removed".
    /// </summary>
    public static string Camera_Removed
        => ResourceManager.GetString("Camera_Removed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Card {0} is already registered for {1}.".
    /// </summary>
    public static string Camera_ScanAlreadyRegistered
        => ResourceManager.GetString("Camera_ScanAlreadyRegistered", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "SD card {0} assigned to {1} [{2}]".
    /// </summary>
    public static string Camera_ScanAssigned
        => ResourceManager.GetString("Camera_ScanAssigned", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No removable drives found. Please insert an SD card.".
    /// </summary>
    public static string Camera_ScanNoRemovable
        => ResourceManager.GetString("Camera_ScanNoRemovable", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Reassign to {0}? [y/N]: ".
    /// </summary>
    public static string Camera_ScanReassign
        => ResourceManager.GetString("Camera_ScanReassign", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Card {0} is currently registered for {1}.".
    /// </summary>
    public static string Camera_ScanRegisteredOther
        => ResourceManager.GetString("Camera_ScanRegisteredOther", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Which drive?".
    /// </summary>
    public static string Camera_ScanWhichDrive
        => ResourceManager.GetString("Camera_ScanWhichDrive", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "SD card {0} removed".
    /// </summary>
    public static string Camera_SdCardRemoved
        => ResourceManager.GetString("Camera_SdCardRemoved", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Assigned cards:".
    /// </summary>
    public static string Camera_ShowAssignedCards
        => ResourceManager.GetString("Camera_ShowAssignedCards", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Enabled:        {0}".
    /// </summary>
    public static string Camera_ShowEnabled
        => ResourceManager.GetString("Camera_ShowEnabled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Features:".
    /// </summary>
    public static string Camera_ShowFeatures
        => ResourceManager.GetString("Camera_ShowFeatures", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  File types:     Video: {0} | Photo: {1}".
    /// </summary>
    public static string Camera_ShowFileTypes
        => ResourceManager.GetString("Camera_ShowFileTypes", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Folder name:    {0}".
    /// </summary>
    public static string Camera_ShowFolder
        => ResourceManager.GetString("Camera_ShowFolder", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Camera: {0} — {1}".
    /// </summary>
    public static string Camera_ShowHeader
        => ResourceManager.GetString("Camera_ShowHeader", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Manufacturer:   {0}".
    /// </summary>
    public static string Camera_ShowManufacturer
        => ResourceManager.GetString("Camera_ShowManufacturer", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Assigned cards: (none)".
    /// </summary>
    public static string Camera_ShowNoCards
        => ResourceManager.GetString("Camera_ShowNoCards", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Serial number:  {0}".
    /// </summary>
    public static string Camera_ShowSerial
        => ResourceManager.GetString("Camera_ShowSerial", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Source:         {0}".
    /// </summary>
    public static string Camera_ShowSource
        => ResourceManager.GetString("Camera_ShowSource", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Type:           {0}".
    /// </summary>
    public static string Camera_ShowType
        => ResourceManager.GetString("Camera_ShowType", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "disabled".
    /// </summary>
    public static string Camera_StateDisabled
        => ResourceManager.GetString("Camera_StateDisabled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "enabled".
    /// </summary>
    public static string Camera_StateEnabled
        => ResourceManager.GetString("Camera_StateEnabled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Tip: Use 'umi config camera add' to set up.".
    /// </summary>
    public static string Camera_TipAdd
        => ResourceManager.GetString("Camera_TipAdd", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "[{0}/{1}] Insert SD card for {2}".
    /// </summary>
    public static string CardSwap_InsertCard
        => ResourceManager.GetString("CardSwap_InsertCard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Enter = Start import, S = Skip, Q = Quit > ".
    /// </summary>
    public static string CardSwap_Prompt
        => ResourceManager.GetString("CardSwap_Prompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to " → Quit".
    /// </summary>
    public static string CardSwap_Quit
        => ResourceManager.GetString("CardSwap_Quit", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to " → Skipped".
    /// </summary>
    public static string CardSwap_Skipped
        => ResourceManager.GetString("CardSwap_Skipped", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Accept? [Y/n]: ".
    /// </summary>
    public static string Cards_AcceptPrompt
        => ResourceManager.GetString("Cards_AcceptPrompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Register SD Card".
    /// </summary>
    public static string Cards_AddBanner
        => ResourceManager.GetString("Cards_AddBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "✓ Done: {0} newly registered, {1} updated, {2} overwritten".
    /// </summary>
    public static string Cards_AddDone
        => ResourceManager.GetString("Cards_AddDone", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  {0}: Already registered for {1}, updated".
    /// </summary>
    public static string Cards_AlreadyRegistered
        => ResourceManager.GetString("Cards_AlreadyRegistered", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "{0} is already registered for {1}.".
    /// </summary>
    public static string Cards_AlreadyRegisteredOther
        => ResourceManager.GetString("Cards_AlreadyRegisteredOther", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Card Assignment".
    /// </summary>
    public static string Cards_AssignBanner
        => ResourceManager.GetString("Cards_AssignBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Card type?".
    /// </summary>
    public static string Cards_AssignCardType
        => ResourceManager.GetString("Cards_AssignCardType", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Currently assigned: {0} ({1}) [{2}]".
    /// </summary>
    public static string Cards_AssignCurrentlyAssigned
        => ResourceManager.GetString("Cards_AssignCurrentlyAssigned", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Card {0} assigned to {1} [{2}]".
    /// </summary>
    public static string Cards_AssignDone
        => ResourceManager.GetString("Cards_AssignDone", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Fixed (always belongs to this camera)".
    /// </summary>
    public static string Cards_AssignFixedOption
        => ResourceManager.GetString("Cards_AssignFixedOption", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Floating (switches between cameras)".
    /// </summary>
    public static string Cards_AssignFloatingOption
        => ResourceManager.GetString("Cards_AssignFloatingOption", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "'umi config cards assign' requires an interactive terminal.".
    /// </summary>
    public static string Cards_AssignNeedInteractive
        => ResourceManager.GetString("Cards_AssignNeedInteractive", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No cameras configured. Use 'umi config camera add' to set up.".
    /// </summary>
    public static string Cards_AssignNoCameras
        => ResourceManager.GetString("Cards_AssignNoCameras", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Currently: not assigned".
    /// </summary>
    public static string Cards_AssignNotAssigned
        => ResourceManager.GetString("Cards_AssignNotAssigned", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Card {0} is not registered.".
    /// </summary>
    public static string Cards_AssignNotRegistered
        => ResourceManager.GetString("Cards_AssignNotRegistered", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "(Remove assignment)".
    /// </summary>
    public static string Cards_AssignRemoveOption
        => ResourceManager.GetString("Cards_AssignRemoveOption", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Assignment for {0} removed".
    /// </summary>
    public static string Cards_AssignRemoved
        => ResourceManager.GetString("Cards_AssignRemoved", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Which camera to assign?".
    /// </summary>
    public static string Cards_AssignWhichCamera
        => ResourceManager.GetString("Cards_AssignWhichCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Batch registration: {0} cards".
    /// </summary>
    public static string Cards_BatchRegistration
        => ResourceManager.GetString("Cards_BatchRegistration", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Camera (EXIF): {0}".
    /// </summary>
    public static string Cards_CameraExif
        => ResourceManager.GetString("Cards_CameraExif", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera '{0}' does not exist in config.".
    /// </summary>
    public static string Cards_CameraNotExists
        => ResourceManager.GetString("Cards_CameraNotExists", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Cannot read Volume Serial from {0}".
    /// </summary>
    public static string Cards_CannotReadVsn
        => ResourceManager.GetString("Cards_CannotReadVsn", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "SD card detected: {0} ({1})".
    /// </summary>
    public static string Cards_CardDetected
        => ResourceManager.GetString("Cards_CardDetected", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Card {0} registered for {1}{2}".
    /// </summary>
    public static string Cards_CardRegisteredFor
        => ResourceManager.GetString("Cards_CardRegisteredFor", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "✓ Card {0} registered for {1}{2}{3}".
    /// </summary>
    public static string Cards_CardRegisteredFull
        => ResourceManager.GetString("Cards_CardRegisteredFull", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "✓ Card {0} removed".
    /// </summary>
    public static string Cards_CardRemoved
        => ResourceManager.GetString("Cards_CardRemoved", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "SD cards for {0} ({1}):".
    /// </summary>
    public static string Cards_CardsForCamera
        => ResourceManager.GetString("Cards_CardsForCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Sure? [y/N]: ".
    /// </summary>
    public static string Cards_ConfirmPrompt
        => ResourceManager.GetString("Cards_ConfirmPrompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Remove {0} (was: {1}{2}).".
    /// </summary>
    public static string Cards_ConfirmRemove
        => ResourceManager.GetString("Cards_ConfirmRemove", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  (0) Don't register".
    /// </summary>
    public static string Cards_DontRegister
        => ResourceManager.GetString("Cards_DontRegister", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  {0} card(s)".
    /// </summary>
    public static string Cards_FooterSimple
        => ResourceManager.GetString("Cards_FooterSimple", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  {0} card(s) registered ({1}x Fixed, {2}x Floating)".
    /// </summary>
    public static string Cards_FooterTotal
        => ResourceManager.GetString("Cards_FooterTotal", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "SD card: {0}{1}".
    /// </summary>
    public static string Cards_HistoryCardInfo
        => ResourceManager.GetString("Cards_HistoryCardInfo", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No imports recorded for this card yet.".
    /// </summary>
    public static string Cards_HistoryNone
        => ResourceManager.GetString("Cards_HistoryNone", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Total imports: {0}".
    /// </summary>
    public static string Cards_HistoryTotalImports
        => ResourceManager.GetString("Cards_HistoryTotalImports", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Type: {0}".
    /// </summary>
    public static string Cards_HistoryType
        => ResourceManager.GetString("Cards_HistoryType", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Disk Serial:  {0}".
    /// </summary>
    public static string Cards_InfoDiskSerial
        => ResourceManager.GetString("Cards_InfoDiskSerial", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Label:        {0}".
    /// </summary>
    public static string Cards_InfoLabel
        => ResourceManager.GetString("Cards_InfoLabel", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Path:         {0}".
    /// </summary>
    public static string Cards_InfoPath
        => ResourceManager.GetString("Cards_InfoPath", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Size:         {0}".
    /// </summary>
    public static string Cards_InfoSize
        => ResourceManager.GetString("Cards_InfoSize", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Type:         {0}".
    /// </summary>
    public static string Cards_InfoType
        => ResourceManager.GetString("Cards_InfoType", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  VSN:          {0}".
    /// </summary>
    public static string Cards_InfoVsn
        => ResourceManager.GetString("Cards_InfoVsn", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Invalid type '{0}'. Allowed: fixed, floating".
    /// </summary>
    public static string Cards_InvalidType
        => ResourceManager.GetString("Cards_InvalidType", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "✓ Known card: {0} (VSN {1}){2}".
    /// </summary>
    public static string Cards_KnownCard
        => ResourceManager.GetString("Cards_KnownCard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Label [{0}]: ".
    /// </summary>
    public static string Cards_LabelPrompt
        => ResourceManager.GetString("Cards_LabelPrompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - SD Card Registry".
    /// </summary>
    public static string Cards_ListBanner
        => ResourceManager.GetString("Cards_ListBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No inserted SD cards detected.".
    /// </summary>
    public static string Cards_NoCardsDetected
        => ResourceManager.GetString("Cards_NoCardsDetected", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No SD cards registered for {0}.".
    /// </summary>
    public static string Cards_NoCardsForCamera
        => ResourceManager.GetString("Cards_NoCardsForCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No SD cards registered.".
    /// </summary>
    public static string Cards_NoCardsRegistered
        => ResourceManager.GetString("Cards_NoCardsRegistered", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "(not available)".
    /// </summary>
    public static string Cards_NotAvailable
        => ResourceManager.GetString("Cards_NotAvailable", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "{0} is not a Removable Drive ({1}).".
    /// </summary>
    public static string Cards_NotRemovableDrive
        => ResourceManager.GetString("Cards_NotRemovableDrive", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Overwrite with {0}? [y/N]: ".
    /// </summary>
    public static string Cards_OverwritePrompt
        => ResourceManager.GetString("Cards_OverwritePrompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Registered SD cards:".
    /// </summary>
    public static string Cards_RegisteredCards
        => ResourceManager.GetString("Cards_RegisteredCards", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Registration cancelled.".
    /// </summary>
    public static string Cards_RegistrationCancelled
        => ResourceManager.GetString("Cards_RegistrationCancelled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "{0} removable drives detected:".
    /// </summary>
    public static string Cards_RemovableDrivesDetected
        => ResourceManager.GetString("Cards_RemovableDrivesDetected", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Remove SD Card".
    /// </summary>
    public static string Cards_RemoveBanner
        => ResourceManager.GetString("Cards_RemoveBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Scan anyway? [y/N]: ".
    /// </summary>
    public static string Cards_ScanAnyway
        => ResourceManager.GetString("Cards_ScanAnyway", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Scan SD Card".
    /// </summary>
    public static string Cards_ScanBanner
        => ResourceManager.GetString("Cards_ScanBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "SD card scanned:".
    /// </summary>
    public static string Cards_ScannedInfo
        => ResourceManager.GetString("Cards_ScannedInfo", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Selection [0-{0}]: ".
    /// </summary>
    public static string Cards_Selection
        => ResourceManager.GetString("Cards_Selection", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Selection [0-{0}] (Default: {1}): ".
    /// </summary>
    public static string Cards_SelectionDefault
        => ResourceManager.GetString("Cards_SelectionDefault", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "✓ Card {0}{1} → {2}".
    /// </summary>
    public static string Cards_SetDone
        => ResourceManager.GetString("Cards_SetDone", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Card is set to Fixed but has no camera ID. Use 'umi config cards assign' to assign.".
    /// </summary>
    public static string Cards_SetNoCamera
        => ResourceManager.GetString("Cards_SetNoCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  {0}: Skipped".
    /// </summary>
    public static string Cards_Skipped
        => ResourceManager.GetString("Cards_Skipped", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "{0} suggests: {1} ({2})".
    /// </summary>
    public static string Cards_SourceSuggests
        => ResourceManager.GetString("Cards_SourceSuggests", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Tip: Use --force to reassign camera.".
    /// </summary>
    public static string Cards_TipForce
        => ResourceManager.GetString("Cards_TipForce", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Tip: Insert card or specify manually with --path F:\".
    /// </summary>
    public static string Cards_TipInsertOrPath
        => ResourceManager.GetString("Cards_TipInsertOrPath", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Use 'umi config cards list' to show all registrations.".
    /// </summary>
    public static string Cards_TipList
        => ResourceManager.GetString("Cards_TipList", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Tip: Insert an SD card and use 'umi config cards scan --path F:\' to register.".
    /// </summary>
    public static string Cards_TipScan
        => ResourceManager.GetString("Cards_TipScan", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Unknown".
    /// </summary>
    public static string Cards_Unknown
        => ResourceManager.GetString("Cards_Unknown", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Unusual VSN format: {0} (expected: XXXX-XXXX)".
    /// </summary>
    public static string Cards_UnusualVsn
        => ResourceManager.GetString("Cards_UnusualVsn", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "version.txt says '{0}', label says '{1}' – version.txt probably outdated".
    /// </summary>
    public static string Cards_VersionTxtConflict
        => ResourceManager.GetString("Cards_VersionTxtConflict", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "version.txt says '{0}', registry says '{1}' – version.txt probably outdated".
    /// </summary>
    public static string Cards_VersionTxtRegistryConflict
        => ResourceManager.GetString("Cards_VersionTxtRegistryConflict", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "VSN {0} is not registered".
    /// </summary>
    public static string Cards_VsnNotRegistered
        => ResourceManager.GetString("Cards_VsnNotRegistered", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Which camera to assign?".
    /// </summary>
    public static string Cards_WhichCamera
        => ResourceManager.GetString("Cards_WhichCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Which card? [1-{0}]: ".
    /// </summary>
    public static string Cards_WhichCard
        => ResourceManager.GetString("Cards_WhichCard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Available cameras:".
    /// </summary>
    public static string Common_AvailableCameras
        => ResourceManager.GetString("Common_AvailableCameras", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Blocked by:".
    /// </summary>
    public static string Common_BlockedBy
        => ResourceManager.GetString("Common_BlockedBy", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Another UMI process is already running!".
    /// </summary>
    public static string Common_BlockedByProcess
        => ResourceManager.GetString("Common_BlockedByProcess", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera '{0}' not found.".
    /// </summary>
    public static string Common_CameraNotFound
        => ResourceManager.GetString("Common_CameraNotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera '{0}' not found in config.".
    /// </summary>
    public static string Common_CameraNotInConfig
        => ResourceManager.GetString("Common_CameraNotInConfig", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Cancellation requested, waiting for running operations...".
    /// </summary>
    public static string Common_CancelRequested
        => ResourceManager.GetString("Common_CancelRequested", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Proceed? [Y/n]: ".
    /// </summary>
    public static string Archive_ConfirmProceed
        => ResourceManager.GetString("Archive_ConfirmProceed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Cancelled.".
    /// </summary>
    public static string Common_Cancelled
        => ResourceManager.GetString("Common_Cancelled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Disabled".
    /// </summary>
    public static string Common_Disabled
        => ResourceManager.GetString("Common_Disabled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Dry-Run: No".
    /// </summary>
    public static string Common_DryRunNo
        => ResourceManager.GetString("Common_DryRunNo", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Dry-Run: YES".
    /// </summary>
    public static string Common_DryRunYes
        => ResourceManager.GetString("Common_DryRunYes", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Enabled".
    /// </summary>
    public static string Common_Enabled
        => ResourceManager.GetString("Common_Enabled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Lock info: {0}".
    /// </summary>
    public static string Common_LockInfo
        => ResourceManager.GetString("Common_LockInfo", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Command: {0}".
    /// </summary>
    public static string Common_LockInfoCommand
        => ResourceManager.GetString("Common_LockInfoCommand", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  PID:     {0}".
    /// </summary>
    public static string Common_LockInfoPid
        => ResourceManager.GetString("Common_LockInfoPid", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Source:  {0}".
    /// </summary>
    public static string Common_LockInfoSource
        => ResourceManager.GetString("Common_LockInfoSource", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Started: {0}".
    /// </summary>
    public static string Common_LockInfoStarted
        => ResourceManager.GetString("Common_LockInfoStarted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No".
    /// </summary>
    public static string Common_No
        => ResourceManager.GetString("Common_No", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No cameras configured.".
    /// </summary>
    public static string Common_NoCamerasConfigured
        => ResourceManager.GetString("Common_NoCamerasConfigured", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No handler for '{0}' registered".
    /// </summary>
    public static string Common_NoHandlerForCamera
        => ResourceManager.GetString("Common_NoHandlerForCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "(no label)".
    /// </summary>
    public static string Common_NoLabel
        => ResourceManager.GetString("Common_NoLabel", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Wait until the other process finishes or terminate it.".
    /// </summary>
    public static string Common_WaitForProcess
        => ResourceManager.GetString("Common_WaitForProcess", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Yes".
    /// </summary>
    public static string Common_Yes
        => ResourceManager.GetString("Common_Yes", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "detected, assignment pending".
    /// </summary>
    public static string Dashboard_Detected
        => ResourceManager.GetString("Dashboard_Detected", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "in queue".
    /// </summary>
    public static string Dashboard_Queued
        => ResourceManager.GetString("Dashboard_Queued", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Scan...".
    /// </summary>
    public static string Dashboard_Scanning
        => ResourceManager.GetString("Dashboard_Scanning", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → ⚠ Already imported in this session (VSN {0})".
    /// </summary>
    public static string Detection_AlreadyImported
        => ResourceManager.GetString("Detection_AlreadyImported", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Cannot read VSN".
    /// </summary>
    public static string Detection_CannotReadVsn
        => ResourceManager.GetString("Detection_CannotReadVsn", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  ✓ Card registered as {0}".
    /// </summary>
    public static string Detection_CardRegistered
        => ResourceManager.GetString("Detection_CardRegistered", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → {0} (fixed assignment)".
    /// </summary>
    public static string Detection_FixedAssignment
        => ResourceManager.GetString("Detection_FixedAssignment", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "fixed".
    /// </summary>
    public static string Detection_FixedLabel
        => ResourceManager.GetString("Detection_FixedLabel", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → Floating card (changing assignment)".
    /// </summary>
    public static string Detection_FloatingCard
        => ResourceManager.GetString("Detection_FloatingCard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Import again? [y/N]: ".
    /// </summary>
    public static string Detection_ImportAgain
        => ResourceManager.GetString("Detection_ImportAgain", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Register card? [Y/n]: ".
    /// </summary>
    public static string Detection_RegisterCard
        => ResourceManager.GetString("Detection_RegisterCard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "    (0) Skip".
    /// </summary>
    public static string Detection_Skip
        => ResourceManager.GetString("Detection_Skip", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → {0}: skipped".
    /// </summary>
    public static string Detection_Skipped
        => ResourceManager.GetString("Detection_Skipped", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to " ← Suggestion".
    /// </summary>
    public static string Detection_Suggestion
        => ResourceManager.GetString("Detection_Suggestion", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → ⚠ Unknown card!".
    /// </summary>
    public static string Detection_UnknownCard
        => ResourceManager.GetString("Detection_UnknownCard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Which camera?".
    /// </summary>
    public static string Detection_WhichCamera
        => ResourceManager.GetString("Detection_WhichCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - EXIF Field Analysis".
    /// </summary>
    public static string ExifScan_Banner
        => ResourceManager.GetString("ExifScan_Banner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Folder does not exist: {0}".
    /// </summary>
    public static string ExifScan_FolderNotFound
        => ResourceManager.GetString("ExifScan_FolderNotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Invalid format: {0}. Use 'table' or 'json'.".
    /// </summary>
    public static string ExifScan_InvalidFormat
        => ResourceManager.GetString("ExifScan_InvalidFormat", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No fields found in category '{0}'.".
    /// </summary>
    public static string ExifScan_NoCategoryFields
        => ResourceManager.GetString("ExifScan_NoCategoryFields", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No photos found.".
    /// </summary>
    public static string ExifScan_NoPhotos
        => ResourceManager.GetString("ExifScan_NoPhotos", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "EXIF Analysis: {0} photos, {1} fields in all images".
    /// </summary>
    public static string ExifScan_PanelHeader
        => ResourceManager.GetString("ExifScan_PanelHeader", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Scanning EXIF fields".
    /// </summary>
    public static string ExifScan_ScanningFields
        => ResourceManager.GetString("ExifScan_ScanningFields", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Tip: Use these fields for burst profiles:".
    /// </summary>
    public static string ExifScan_Tip
        => ResourceManager.GetString("ExifScan_Tip", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "All Dates/Sources".
    /// </summary>
    public static string Gps_AllDatesSources
        => ResourceManager.GetString("Gps_AllDatesSources", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - GPS Create".
    /// </summary>
    public static string Gps_CreateBanner
        => ResourceManager.GetString("Gps_CreateBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPS Create: {0} videos found, {1} GPX created, {2} skipped (present), {3} no GPS match".
    /// </summary>
    public static string Gps_CreateSummary
        => ResourceManager.GetString("Gps_CreateSummary", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "All".
    /// </summary>
    public static string Gps_DateAll
        => ResourceManager.GetString("Gps_DateAll", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Date:  {0}".
    /// </summary>
    public static string Gps_DateLabel
        => ResourceManager.GetString("Gps_DateLabel", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "[DRY-RUN] Would finalize {0} videos (GPS inject + move + cleanup)".
    /// </summary>
    public static string Gps_DryRunFinalize
        => ResourceManager.GetString("Gps_DryRunFinalize", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Expected: {0}".
    /// </summary>
    public static string Gps_ExpectedPath
        => ResourceManager.GetString("Gps_ExpectedPath", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  {0} exported video(s) found".
    /// </summary>
    public static string Gps_ExportedFound
        => ResourceManager.GetString("Gps_ExportedFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Filter: {0}".
    /// </summary>
    public static string Gps_FilterLabel
        => ResourceManager.GetString("Gps_FilterLabel", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Finalize check: Searching for exported videos...".
    /// </summary>
    public static string Gps_FinalizeCheck
        => ResourceManager.GetString("Gps_FinalizeCheck", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Finalize: {0} exported videos → GPS inject → Video/ → Cleanup ({1} errors)".
    /// </summary>
    public static string Gps_FinalizeSummary
        => ResourceManager.GetString("Gps_FinalizeSummary", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPS injected".
    /// </summary>
    public static string Gps_GpsInjected
        => ResourceManager.GetString("Gps_GpsInjected", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPX created".
    /// </summary>
    public static string Gps_GpxCreated
        => ResourceManager.GetString("Gps_GpxCreated", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "(not configured)".
    /// </summary>
    public static string Gps_GpxNotConfigured
        => ResourceManager.GetString("Gps_GpxNotConfigured", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPX source not found: {0}".
    /// </summary>
    public static string Gps_GpxSourceNotFound
        => ResourceManager.GetString("Gps_GpxSourceNotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - GPS Inject".
    /// </summary>
    public static string Gps_InjectBanner
        => ResourceManager.GetString("Gps_InjectBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPS Inject: {0} injected, {1} skipped (GPS present), {2} no GPX".
    /// </summary>
    public static string Gps_InjectSummary
        => ResourceManager.GetString("Gps_InjectSummary", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPS injection: Processing videos in Video/ folders...".
    /// </summary>
    public static string Gps_InjectionHeader
        => ResourceManager.GetString("Gps_InjectionHeader", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  No exported videos in postprocess/exported/ found".
    /// </summary>
    public static string Gps_NoExportedVideos
        => ResourceManager.GetString("Gps_NoExportedVideos", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "no GPS match".
    /// </summary>
    public static string Gps_NoGpsMatch
        => ResourceManager.GetString("Gps_NoGpsMatch", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "no GPX match".
    /// </summary>
    public static string Gps_NoGpxMatch
        => ResourceManager.GetString("Gps_NoGpxMatch", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "⚠ No GPX source directory configured or not found.".
    /// </summary>
    public static string Gps_NoGpxSourceWarning
        => ResourceManager.GetString("Gps_NoGpxSourceWarning", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No videos for GPS injection found.".
    /// </summary>
    public static string Gps_NoInjectionVideos
        => ResourceManager.GetString("Gps_NoInjectionVideos", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No videos found.".
    /// </summary>
    public static string Gps_NoVideosFound
        => ResourceManager.GetString("Gps_NoVideosFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "inject needed".
    /// </summary>
    public static string Gps_NoteInjectNeeded
        => ResourceManager.GetString("Gps_NoteInjectNeeded", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "no GPS match".
    /// </summary>
    public static string Gps_NoteNoGpsMatch
        => ResourceManager.GetString("Gps_NoteNoGpsMatch", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "skipped (GPS present)".
    /// </summary>
    public static string Gps_SkippedGpsPresent
        => ResourceManager.GetString("Gps_SkippedGpsPresent", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Tip: 'umi gps inject' to inject GPS into missing videos".
    /// </summary>
    public static string Gps_TipInject
        => ResourceManager.GetString("Gps_TipInject", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - GPS Verify".
    /// </summary>
    public static string Gps_VerifyBanner
        => ResourceManager.GetString("Gps_VerifyBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPS Verify: {0}".
    /// </summary>
    public static string Gps_VerifyLabel
        => ResourceManager.GetString("Gps_VerifyLabel", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Summary: {0}/{1} with GPS, {2}/{3} GPX present".
    /// </summary>
    public static string Gps_VerifySummary
        => ResourceManager.GetString("Gps_VerifySummary", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Found: {0} videos".
    /// </summary>
    public static string Gps_VideosFound
        => ResourceManager.GetString("Gps_VideosFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Import aborted.".
    /// </summary>
    public static string Import_Aborted
        => ResourceManager.GetString("Import_Aborted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Ad-hoc Import".
    /// </summary>
    public static string Import_AdHocBanner
        => ResourceManager.GetString("Import_AdHocBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Another import is already running!".
    /// </summary>
    public static string Import_AnotherImportRunning
        => ResourceManager.GetString("Import_AnotherImportRunning", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Universal Media Import".
    /// </summary>
    public static string Import_Banner
        => ResourceManager.GetString("Import_Banner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "SD card registered: {0} → {1}".
    /// </summary>
    public static string Import_CardRegistered
        => ResourceManager.GetString("Import_CardRegistered", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Import completed!".
    /// </summary>
    public static string Import_Completed
        => ResourceManager.GetString("Import_Completed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Import now? [Y/n] ".
    /// </summary>
    public static string Import_ConfirmImport
        => ResourceManager.GetString("Import_ConfirmImport", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  {0} files are without subfolder.".
    /// </summary>
    public static string Import_ConflictExisting
        => ResourceManager.GetString("Import_ConflictExisting", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "⚠️ Structure note for {0}/{1}:".
    /// </summary>
    public static string Import_ConflictHeader
        => ResourceManager.GetString("Import_ConflictHeader", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Now new files with different media type are coming.".
    /// </summary>
    public static string Import_ConflictNewType
        => ResourceManager.GetString("Import_ConflictNewType", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  (1) Create subfolder for new files, existing stay [Default]".
    /// </summary>
    public static string Import_ConflictOption1
        => ResourceManager.GetString("Import_ConflictOption1", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "      → Existing files: stay where they are".
    /// </summary>
    public static string Import_ConflictOption1Detail1
        => ResourceManager.GetString("Import_ConflictOption1Detail1", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "      → New files: go into Photo/ or Video/ folder".
    /// </summary>
    public static string Import_ConflictOption1Detail2
        => ResourceManager.GetString("Import_ConflictOption1Detail2", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  (2) Keep everything flat".
    /// </summary>
    public static string Import_ConflictOption2
        => ResourceManager.GetString("Import_ConflictOption2", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "      → New files go next to existing files".
    /// </summary>
    public static string Import_ConflictOption2Detail
        => ResourceManager.GetString("Import_ConflictOption2Detail", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  (3) Reorganize: Move everything into subfolders".
    /// </summary>
    public static string Import_ConflictOption3
        => ResourceManager.GetString("Import_ConflictOption3", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "      → Existing files: moved to Video/ or Photo/".
    /// </summary>
    public static string Import_ConflictOption3Detail1
        => ResourceManager.GetString("Import_ConflictOption3Detail1", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "      → New files: also go into subfolders".
    /// </summary>
    public static string Import_ConflictOption3Detail2
        => ResourceManager.GetString("Import_ConflictOption3Detail2", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "      ⚠️ Only if not yet imported into editing software!".
    /// </summary>
    public static string Import_ConflictOption3Warning
        => ResourceManager.GetString("Import_ConflictOption3Warning", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Choice (1/2/3) [1]: ".
    /// </summary>
    public static string Import_ConflictPrompt
        => ResourceManager.GetString("Import_ConflictPrompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  ⚠️ WARNING: Existing files will be moved!".
    /// </summary>
    public static string Import_ConflictReorgWarning
        => ResourceManager.GetString("Import_ConflictReorgWarning", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Continue anyway? [Y/n] ".
    /// </summary>
    public static string Import_ContinueAnyway
        => ResourceManager.GetString("Import_ContinueAnyway", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "▶ Copying to workbench...".
    /// </summary>
    public static string Import_CopyingToWorkbench
        => ResourceManager.GetString("Import_CopyingToWorkbench", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "or delete manually: {0}".
    /// </summary>
    public static string Import_DeleteLockManually
        => ResourceManager.GetString("Import_DeleteLockManually", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Details: {0}".
    /// </summary>
    public static string Import_Details
        => ResourceManager.GetString("Import_Details", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Folder: {0}".
    /// </summary>
    public static string Import_FolderLabel
        => ResourceManager.GetString("Import_FolderLabel", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Folder not found: {0}".
    /// </summary>
    public static string Import_FolderNotFound
        => ResourceManager.GetString("Import_FolderNotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPU Queue: Waiting for {0} pending tasks...".
    /// </summary>
    public static string Import_GpuQueueWaiting
        => ResourceManager.GetString("Import_GpuQueueWaiting", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "History reconciled: {0} stale entries for {1} removed.".
    /// </summary>
    public static string Import_HistoryReconciled
        => ResourceManager.GetString("Import_HistoryReconciled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Import history for {0} reset.".
    /// </summary>
    public static string Import_HistoryReset
        => ResourceManager.GetString("Import_HistoryReset", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Import for {0} camera(s):".
    /// </summary>
    public static string Import_ImportForCameras
        => ResourceManager.GetString("Import_ImportForCameras", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "▶ Importing: {0}".
    /// </summary>
    public static string Import_Importing
        => ResourceManager.GetString("Import_Importing", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  ⚠ Layout conflict detected → Default (new files in folder, existing stay)".
    /// </summary>
    public static string Import_LayoutConflict
        => ResourceManager.GetString("Import_LayoutConflict", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → MTP device: {0}".
    /// </summary>
    public static string Import_MtpDevice
        => ResourceManager.GetString("Import_MtpDevice", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → {0} files downloaded ({1})".
    /// </summary>
    public static string Import_MtpDownloaded
        => ResourceManager.GetString("Import_MtpDownloaded", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → {0} errors".
    /// </summary>
    public static string Import_MtpErrors
        => ResourceManager.GetString("Import_MtpErrors", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "[{0}] No registered MTP device connected.".
    /// </summary>
    public static string Import_MtpNoDevice
        => ResourceManager.GetString("Import_MtpNoDevice", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No matching cameras found!".
    /// </summary>
    public static string Import_NoCamerasFound
        => ResourceManager.GetString("Import_NoCamerasFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "(No files to import)".
    /// </summary>
    public static string Import_NoFilesToImport
        => ResourceManager.GetString("Import_NoFilesToImport", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  PID {0}, started {1}".
    /// </summary>
    public static string Import_PidInfo
        => ResourceManager.GetString("Import_PidInfo", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Register as '{0}'? [Y/n] ".
    /// </summary>
    public static string Import_RegisterAsCamera
        => ResourceManager.GetString("Import_RegisterAsCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → {0} photos, {1} videos, {2} sequences detected".
    /// </summary>
    public static string Import_ScanResult
        => ResourceManager.GetString("Import_ScanResult", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "SD card in {0} belongs to '{1}', not to '{2}'".
    /// </summary>
    public static string Import_SdCardBelongsTo
        => ResourceManager.GetString("Import_SdCardBelongsTo", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "    {0}  │ {1} photos │ Mode: {2}".
    /// </summary>
    public static string Import_SequenceDetail
        => ResourceManager.GetString("Import_SequenceDetail", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Sequences:".
    /// </summary>
    public static string Import_Sequences
        => ResourceManager.GetString("Import_Sequences", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Total: {0} files, {1} → {2}".
    /// </summary>
    public static string Import_SimTotal
        => ResourceManager.GetString("Import_SimTotal", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Simulation finished. No files were copied.".
    /// </summary>
    public static string Import_SimulationDone
        => ResourceManager.GetString("Import_SimulationDone", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "▶ This is how the import would look:".
    /// </summary>
    public static string Import_SimulationPreview
        => ResourceManager.GetString("Import_SimulationPreview", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "{0}x single photos".
    /// </summary>
    public static string Import_SinglePhotos
        => ResourceManager.GetString("Import_SinglePhotos", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Source: {0}, Command: {1}".
    /// </summary>
    public static string Import_SourceCommand
        => ResourceManager.GetString("Import_SourceCommand", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Flatten (subfolders as prefix)".
    /// </summary>
    public static string Import_StructureFlatten
        => ResourceManager.GetString("Import_StructureFlatten", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Keep".
    /// </summary>
    public static string Import_StructureKeep
        => ResourceManager.GetString("Import_StructureKeep", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Unknown camera IDs: {0}".
    /// </summary>
    public static string Import_UnknownCameraIds
        => ResourceManager.GetString("Import_UnknownCameraIds", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Unknown SD card in {0} (VSN: {1})".
    /// </summary>
    public static string Import_UnknownCard
        => ResourceManager.GetString("Import_UnknownCard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Wait until the running import finishes,".
    /// </summary>
    public static string Import_WaitOrDelete
        => ResourceManager.GetString("Import_WaitOrDelete", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Card type?".
    /// </summary>
    public static string Match_CardType
        => ResourceManager.GetString("Match_CardType", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "    (1) Fixed    – This card always belongs to {0}".
    /// </summary>
    public static string Match_FixedOption
        => ResourceManager.GetString("Match_FixedOption", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "    (2) Floating – This card switches between cameras".
    /// </summary>
    public static string Match_FloatingOption
        => ResourceManager.GetString("Match_FloatingOption", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Selection [1]: ".
    /// </summary>
    public static string Match_Selection
        => ResourceManager.GetString("Match_Selection", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Post-Processing".
    /// </summary>
    public static string Process_Banner
        => ResourceManager.GetString("Process_Banner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Processing completed!".
    /// </summary>
    public static string Process_Completed
        => ResourceManager.GetString("Process_Completed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "[DRY-RUN] Would stabilize {0} videos (mode: {1})".
    /// </summary>
    public static string Process_DryRunStabilize
        => ResourceManager.GetString("Process_DryRunStabilize", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "[DRY-RUN] would be moved".
    /// </summary>
    public static string Process_DryRunWouldMove
        => ResourceManager.GetString("Process_DryRunWouldMove", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Done: {0} stabilized, {1} errors".
    /// </summary>
    public static string Process_GpuDone
        => ResourceManager.GetString("Process_GpuDone", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "GPU Queue: {0} videos queued (Batch: {1})".
    /// </summary>
    public static string Process_GpuQueueEnqueued
        => ResourceManager.GetString("Process_GpuQueueEnqueued", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "At least one option required: --stabilize or --sort".
    /// </summary>
    public static string Process_MinOneOption
        => ResourceManager.GetString("Process_MinOneOption", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Error while moving: {0}: {1}".
    /// </summary>
    public static string Process_MoveError
        => ResourceManager.GetString("Process_MoveError", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "{0} errors while moving".
    /// </summary>
    public static string Process_MoveErrors
        => ResourceManager.GetString("Process_MoveErrors", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No videos for Gyroflow found.".
    /// </summary>
    public static string Process_NoGyroflowVideos
        => ResourceManager.GetString("Process_NoGyroflowVideos", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No media files to sort found.".
    /// </summary>
    public static string Process_NoMediaFiles
        => ResourceManager.GetString("Process_NoMediaFiles", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  → {0} skipped (already at target)".
    /// </summary>
    public static string Process_SkippedAtTarget
        => ResourceManager.GetString("Process_SkippedAtTarget", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  ✓ {0} files {1} into {2} date folders".
    /// </summary>
    public static string Process_SortResult
        => ResourceManager.GetString("Process_SortResult", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "sorted".
    /// </summary>
    public static string Process_Sorted
        => ResourceManager.GetString("Process_Sorted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  Sorting files by EXIF date...".
    /// </summary>
    public static string Process_SortingFiles
        => ResourceManager.GetString("Process_SortingFiles", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Unknown --sort value: '{0}'. Allowed: 'full' or 'date'.".
    /// </summary>
    public static string Process_UnknownSortValue
        => ResourceManager.GetString("Process_UnknownSortValue", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Really delete profile '{0}'? (y/N): ".
    /// </summary>
    public static string Profiles_ConfirmDelete
        => ResourceManager.GetString("Profiles_ConfirmDelete", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Error deleting profile '{0}'.".
    /// </summary>
    public static string Profiles_DeleteError
        => ResourceManager.GetString("Profiles_DeleteError", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Profile '{0}' was deleted.".
    /// </summary>
    public static string Profiles_Deleted
        => ResourceManager.GetString("Profiles_Deleted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Profile '{0}' does not exist.".
    /// </summary>
    public static string Profiles_DoesNotExist
        => ResourceManager.GetString("Profiles_DoesNotExist", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No profiles found.".
    /// </summary>
    public static string Profiles_None
        => ResourceManager.GetString("Profiles_None", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Profile '{0}' not found.".
    /// </summary>
    public static string Profiles_NotFound
        => ResourceManager.GetString("Profiles_NotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "already connected".
    /// </summary>
    public static string Quick_AlreadyConnected
        => ResourceManager.GetString("Quick_AlreadyConnected", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "already inserted".
    /// </summary>
    public static string Quick_AlreadyInserted
        => ResourceManager.GetString("Quick_AlreadyInserted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Change? [Enter = OK, or enter path/name]:".
    /// </summary>
    public static string Quick_ChangeTarget
        => ResourceManager.GetString("Quick_ChangeTarget", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "files copied".
    /// </summary>
    public static string Quick_FilesCopied
        => ResourceManager.GetString("Quick_FilesCopied", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "files downloaded".
    /// </summary>
    public static string Quick_FilesDownloaded
        => ResourceManager.GetString("Quick_FilesDownloaded", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "file(s) found".
    /// </summary>
    public static string Quick_FilesFound
        => ResourceManager.GetString("Quick_FilesFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Gyroflow failed: {0}".
    /// </summary>
    public static string Quick_GyroflowFailed
        => ResourceManager.GetString("Quick_GyroflowFailed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Quick-Import completed".
    /// </summary>
    public static string Quick_ImportCompleted
        => ResourceManager.GetString("Quick_ImportCompleted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Metadata backup...".
    /// </summary>
    public static string Quick_MetadataBackup
        => ResourceManager.GetString("Quick_MetadataBackup", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Metadata backup failed: {0}: {1}".
    /// </summary>
    public static string Quick_MetadataBackupFailed
        => ResourceManager.GetString("Quick_MetadataBackupFailed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Quick-Import Mode".
    /// </summary>
    public static string Quick_Mode
        => ResourceManager.GetString("Quick_Mode", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "MTP transfer started".
    /// </summary>
    public static string Quick_MtpTransferStarted
        => ResourceManager.GetString("Quick_MtpTransferStarted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No files imported.".
    /// </summary>
    public static string Quick_NoFilesImported
        => ResourceManager.GetString("Quick_NoFilesImported", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No importable files on {0}".
    /// </summary>
    public static string Quick_NoImportableFiles
        => ResourceManager.GetString("Quick_NoImportableFiles", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No importable files on device.".
    /// </summary>
    public static string Quick_NoImportableFilesDevice
        => ResourceManager.GetString("Quick_NoImportableFilesDevice", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No importable files on {0}".
    /// </summary>
    public static string Quick_NoImportableFilesSource
        => ResourceManager.GetString("Quick_NoImportableFilesSource", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No target folder specified or input cancelled.".
    /// </summary>
    public static string Quick_NoTarget
        => ResourceManager.GetString("Quick_NoTarget", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Starting Gyroflow stabilization...".
    /// </summary>
    public static string Quick_StartingGyroflow
        => ResourceManager.GetString("Quick_StartingGyroflow", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Target folder".
    /// </summary>
    public static string Quick_TargetFolder
        => ResourceManager.GetString("Quick_TargetFolder", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Waiting for cards... (Ctrl+C to exit)".
    /// </summary>
    public static string Quick_WaitingForCards
        => ResourceManager.GetString("Quick_WaitingForCards", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Waiting for next card...".
    /// </summary>
    public static string Quick_WaitingForNextCard
        => ResourceManager.GetString("Quick_WaitingForNextCard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Waiting for next card/device...".
    /// </summary>
    public static string Quick_WaitingForNextCardDevice
        => ResourceManager.GetString("Quick_WaitingForNextCardDevice", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Metadata Restore".
    /// </summary>
    public static string Restore_Banner
        => ResourceManager.GetString("Restore_Banner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Cancelled by user".
    /// </summary>
    public static string Restore_CancelledByUser
        => ResourceManager.GetString("Restore_CancelledByUser", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Done: {0} restored, {1} errors".
    /// </summary>
    public static string Restore_Done
        => ResourceManager.GetString("Restore_Done", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Error ({0})".
    /// </summary>
    public static string Restore_Error
        => ResourceManager.GetString("Restore_Error", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Found: {0} videos".
    /// </summary>
    public static string Restore_VideosFound
        => ResourceManager.GetString("Restore_VideosFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Workbench not found: {0}".
    /// </summary>
    public static string Restore_WorkbenchNotFound
        => ResourceManager.GetString("Restore_WorkbenchNotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Setup cancelled. No changes saved.".
    /// </summary>
    public static string Setup_Aborted
        => ResourceManager.GetString("Setup_Aborted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI Setup Wizard".
    /// </summary>
    public static string Setup_Banner
        => ResourceManager.GetString("Setup_Banner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera setup cancelled.".
    /// </summary>
    public static string Setup_CameraAborted
        => ResourceManager.GetString("Setup_CameraAborted", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Add another camera: umi setup camera".
    /// </summary>
    public static string Setup_CameraAddMore
        => ResourceManager.GetString("Setup_CameraAddMore", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI Camera Setup Wizard".
    /// </summary>
    public static string Setup_CameraBanner
        => ResourceManager.GetString("Setup_CameraBanner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "'umi setup camera' requires an interactive terminal.".
    /// </summary>
    public static string Setup_CameraNeedInteractive
        => ResourceManager.GetString("Setup_CameraNeedInteractive", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Register cards:        umi cards scan".
    /// </summary>
    public static string Setup_CameraRegisterCards
        => ResourceManager.GetString("Setup_CameraRegisterCards", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "New config will be created: {0}".
    /// </summary>
    public static string Setup_CreatingConfig
        => ResourceManager.GetString("Setup_CreatingConfig", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Editing existing config: {0}".
    /// </summary>
    public static string Setup_EditingConfig
        => ResourceManager.GetString("Setup_EditingConfig", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "'umi setup' requires an interactive terminal.".
    /// </summary>
    public static string Setup_NeedInteractive
        => ResourceManager.GetString("Setup_NeedInteractive", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "(not configured)".
    /// </summary>
    public static string Setup_NotConfigured
        => ResourceManager.GetString("Setup_NotConfigured", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "(not set)".
    /// </summary>
    public static string Setup_NotSet
        => ResourceManager.GetString("Setup_NotSet", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Save cancelled. No changes saved.".
    /// </summary>
    public static string Setup_SaveCancelled
        => ResourceManager.GetString("Setup_SaveCancelled", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Save configuration?".
    /// </summary>
    public static string Setup_SavePrompt
        => ResourceManager.GetString("Setup_SavePrompt", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Configuration saved: {0}".
    /// </summary>
    public static string Setup_Saved
        => ResourceManager.GetString("Setup_Saved", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Set up camera — Camera Setup Wizard starts...".
    /// </summary>
    public static string Setup_StartCameraWizard
        => ResourceManager.GetString("Setup_StartCameraWizard", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Camera Detection Test".
    /// </summary>
    public static string TestCamera_Banner
        => ResourceManager.GetString("TestCamera_Banner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Video not found: {0}".
    /// </summary>
    public static string TestCamera_VideoNotFound
        => ResourceManager.GetString("TestCamera_VideoNotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "All files are correct!".
    /// </summary>
    public static string Verify_AllCorrect
        => ResourceManager.GetString("Verify_AllCorrect", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI - Verification".
    /// </summary>
    public static string Verify_Banner
        => ResourceManager.GetString("Verify_Banner", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Import database not found: {0}".
    /// </summary>
    public static string Verify_DbNotFound
        => ResourceManager.GetString("Verify_DbNotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "✗ {0} errors".
    /// </summary>
    public static string Verify_Errors
        => ResourceManager.GetString("Verify_Errors", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Errors:".
    /// </summary>
    public static string Verify_ErrorsHeader
        => ResourceManager.GetString("Verify_ErrorsHeader", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Verification failed: {0} errors found".
    /// </summary>
    public static string Verify_Failed
        => ResourceManager.GetString("Verify_Failed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "FFprobe not available - video integrity will not be checked".
    /// </summary>
    public static string Verify_FfprobeNotAvailable
        => ResourceManager.GetString("Verify_FfprobeNotAvailable", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Verification: {0} files checked".
    /// </summary>
    public static string Verify_FilesChecked
        => ResourceManager.GetString("Verify_FilesChecked", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to " for {0}".
    /// </summary>
    public static string Verify_ForCamera
        => ResourceManager.GetString("Verify_ForCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "  ... and {0} more warnings".
    /// </summary>
    public static string Verify_MoreWarnings
        => ResourceManager.GetString("Verify_MoreWarnings", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "✓ {0} OK".
    /// </summary>
    public static string Verify_Ok
        => ResourceManager.GetString("Verify_Ok", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Post-Import Verification{0}...".
    /// </summary>
    public static string Verify_PostImport
        => ResourceManager.GetString("Verify_PostImport", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Run 'umi import' first or use standalone mode without --post-import".
    /// </summary>
    public static string Verify_RunImportFirst
        => ResourceManager.GetString("Verify_RunImportFirst", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "⚠ {0} warnings".
    /// </summary>
    public static string Verify_Warnings
        => ResourceManager.GetString("Verify_Warnings", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Warnings:".
    /// </summary>
    public static string Verify_WarningsHeader
        => ResourceManager.GetString("Verify_WarningsHeader", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Workbench verification{0}...".
    /// </summary>
    public static string Verify_WorkbenchCheck
        => ResourceManager.GetString("Verify_WorkbenchCheck", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Workbench not found: {0}".
    /// </summary>
    public static string Verify_WorkbenchNotFound
        => ResourceManager.GetString("Verify_WorkbenchNotFound", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Waiting for changes...".
    /// </summary>
    public static string Watch_WaitingForChanges
        => ResourceManager.GetString("Watch_WaitingForChanges", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Waiting for next card (→ {0})...".
    /// </summary>
    public static string Watch_WaitingForNextCardCamera
        => ResourceManager.GetString("Watch_WaitingForNextCardCamera", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "UMI Update Check".
    /// </summary>
    public static string Update_CheckTitle
        => ResourceManager.GetString("Update_CheckTitle", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Current version : {0}".
    /// </summary>
    public static string Update_CurrentVersion
        => ResourceManager.GetString("Update_CurrentVersion", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Latest version  : {0}".
    /// </summary>
    public static string Update_LatestVersion
        => ResourceManager.GetString("Update_LatestVersion", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Update available: yes".
    /// </summary>
    public static string Update_Available
        => ResourceManager.GetString("Update_Available", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Update available: no — you are up to date".
    /// </summary>
    public static string Update_UpToDate
        => ResourceManager.GetString("Update_UpToDate", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Download URL    : {0}".
    /// </summary>
    public static string Update_DownloadUrl
        => ResourceManager.GetString("Update_DownloadUrl", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Update check failed. Check your network connection.".
    /// </summary>
    public static string Update_CheckFailed
        => ResourceManager.GetString("Update_CheckFailed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "No download URL found in release assets.".
    /// </summary>
    public static string Update_NoDownloadUrl
        => ResourceManager.GetString("Update_NoDownloadUrl", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Downloading update {0}%...".
    /// </summary>
    public static string Update_Downloading
        => ResourceManager.GetString("Update_Downloading", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Download complete. Launching installer...".
    /// </summary>
    public static string Update_DownloadComplete
        => ResourceManager.GetString("Update_DownloadComplete", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Download failed: {0}".
    /// </summary>
    public static string Update_DownloadFailed
        => ResourceManager.GetString("Update_DownloadFailed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Failed to launch installer: {0}".
    /// </summary>
    public static string Update_LaunchFailed
        => ResourceManager.GetString("Update_LaunchFailed", resourceCulture) ?? string.Empty;

    /// <summary>
    ///   Looks up a localized string similar to "Update download canceled.".
    /// </summary>
    public static string Update_Canceled
        => ResourceManager.GetString("Update_Canceled", resourceCulture) ?? string.Empty;

}
