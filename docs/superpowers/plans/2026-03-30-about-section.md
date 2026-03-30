# About Section Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a full-width About footer to SettingsView showing app version, data file version tags, and required credits; fix VATSpy.dat to use a GitHub release tag instead of a timestamp.

**Architecture:** Two independent changes — (1) `DataFileUpdateService` is updated to fetch VATSpy.dat from GitHub releases (same pattern as the other two files), storing a `VatSpyReleaseTag` setting; (2) `SettingsViewModel` gains four read-only display properties; `SettingsView.xaml` gains a footer `Border` docked below the existing columns.

**Tech Stack:** C# / WPF / .NET Framework 4.7.2 — no new dependencies.

---

## File Map

| File | Change |
|------|--------|
| `FSTRaK/Properties/Settings.settings` | Add `VatSpyReleaseTag` string setting |
| `FSTRaK/Properties/Settings.Designer.cs` | Add generated property for `VatSpyReleaseTag` (manual edit — mirrors existing pattern) |
| `FSTRaK/BusinessLogic/VatsimService/DataFileUpdateService.cs` | Replace `UpdateVatSpyDatAsync` implementation |
| `FSTRaK/ViewModels/SettingsViewModel.cs` | Add `AppVersion`, `FirBoundaryTag`, `TraconBoundaryTag`, `VatSpyTag` properties |
| `FSTRaK/Views/SettingsView.xaml` | Wrap columns in `DockPanel`, add About footer `Border` |
| `FSTRaK/Views/SettingsView.xaml.cs` | Add `RequestNavigate` handler for GitHub hyperlink |

---

## Task 1: Add VatSpyReleaseTag setting

**Files:**
- Modify: `FSTRaK/Properties/Settings.settings`
- Modify: `FSTRaK/Properties/Settings.Designer.cs`

> There are no automated tests for WPF settings files. Manual verification is in Task 3.

- [ ] **Step 1: Add the setting to Settings.settings**

In `FSTRaK/Properties/Settings.settings`, add after the `VatSpyLastUpdated` entry (line 71–73):

```xml
    <Setting Name="VatSpyReleaseTag" Type="System.String" Scope="User">
      <Value Profile="(Default)" />
    </Setting>
```

- [ ] **Step 2: Add the generated property to Settings.Designer.cs**

In `FSTRaK/Properties/Settings.Designer.cs`, locate the `VatSpyLastUpdated` property block and add the following immediately after it (mirror the exact pattern of `FirBoundaryReleaseTag`):

```csharp
[global::System.Configuration.UserScopedSettingAttribute()]
[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
[global::System.Configuration.DefaultSettingValueAttribute("")]
public string VatSpyReleaseTag {
    get {
        return ((string)(this["VatSpyReleaseTag"]));
    }
    set {
        this["VatSpyReleaseTag"] = value;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/Properties/Settings.settings FSTRaK/Properties/Settings.Designer.cs
git commit -m "chore: add VatSpyReleaseTag setting"
```

---

## Task 2: Fix VATSpy.dat to use GitHub release tag

**Files:**
- Modify: `FSTRaK/BusinessLogic/VatsimService/DataFileUpdateService.cs`

The current `UpdateVatSpyDatAsync` method fetches from `https://api.vatsim.net/api/map_data/` and stores a timestamp. Replace it entirely to use `GetLatestGitHubReleaseAssetAsync` — exactly the same pattern as `UpdateFirBoundariesAsync`.

> No automated tests — this hits GitHub's API at runtime. Manual verification: delete `VATSpy.dat` from `%LOCALAPPDATA%\FSTRaK_DEBUG\Data\` and restart the app; confirm the file reappears and `VatSpyReleaseTag` is stored in settings.

- [ ] **Step 1: Replace UpdateVatSpyDatAsync**

In `FSTRaK/BusinessLogic/VatsimService/DataFileUpdateService.cs`, replace the entire `UpdateVatSpyDatAsync` method (lines 117–158) with:

```csharp
// Returns true if the file was downloaded (new or updated).
private async Task<bool> UpdateVatSpyDatAsync(string dataDir)
{
    const string filename = "VATSpy.dat";
    var localPath = Path.Combine(dataDir, filename);
    var storedTag = Properties.Settings.Default.VatSpyReleaseTag;

    try
    {
        var (latestTag, downloadUrl) = await GetLatestGitHubReleaseAssetAsync(
            "vatsimnetwork", "vatspy-data-project", filename);

        if (latestTag == storedTag && File.Exists(localPath))
        {
            Log.Debug("VATSpy.dat up to date (tag: {Tag})", latestTag);
            return false;
        }

        Log.Information("Downloading VATSpy.dat (tag: {Tag})", latestTag);
        await DownloadFileAtomicAsync(downloadUrl, localPath);
        Properties.Settings.Default.VatSpyReleaseTag = latestTag;
        Properties.Settings.Default.Save();
        Log.Information("VATSpy.dat updated successfully (tag: {Tag})", latestTag);
        return true;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to update VATSpy.dat");
        if (!File.Exists(localPath))
            Log.Error("No local fallback available for VATSpy.dat");
        return false;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add FSTRaK/BusinessLogic/VatsimService/DataFileUpdateService.cs
git commit -m "fix: fetch VATSpy.dat from GitHub release tag instead of VATSIM map_data API"
```

---

## Task 3: Add About properties to SettingsViewModel

**Files:**
- Modify: `FSTRaK/ViewModels/SettingsViewModel.cs`

Add four read-only string properties. `AppVersion` is computed once from `AssemblyInfo`. The three tag properties read from `Properties.Settings` — they are populated in `SettingsView_OnLoaded` (which already loads all other settings on view load), so they will always reflect the values at startup.

> No automated tests — these are simple property getters over `Properties.Settings` and `Assembly`. Manual verification: open Settings view and confirm the About footer shows correct values (Task 4).

- [ ] **Step 1: Add the four properties to SettingsViewModel**

In `FSTRaK/ViewModels/SettingsViewModel.cs`, add the following block after the `StatSimApiKey` property (around line 333), before the constructor:

```csharp
public string AppVersion
{
    get
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return $"v{v.Major}.{v.Minor}.{v.Build}";
    }
}

private string _firBoundaryTag = "—";
public string FirBoundaryTag
{
    get => _firBoundaryTag;
    private set { _firBoundaryTag = value; OnPropertyChanged(); }
}

private string _traconBoundaryTag = "—";
public string TraconBoundaryTag
{
    get => _traconBoundaryTag;
    private set { _traconBoundaryTag = value; OnPropertyChanged(); }
}

private string _vatSpyTag = "—";
public string VatSpyTag
{
    get => _vatSpyTag;
    private set { _vatSpyTag = value; OnPropertyChanged(); }
}
```

- [ ] **Step 2: Populate the tag properties in SettingsView_OnLoaded**

In `SettingsView_OnLoaded` (around line 364), add these three lines after the existing `OpenAipApiKey` assignment:

```csharp
FirBoundaryTag = string.IsNullOrEmpty(Properties.Settings.Default.FirBoundaryReleaseTag)
    ? "—" : Properties.Settings.Default.FirBoundaryReleaseTag;
TraconBoundaryTag = string.IsNullOrEmpty(Properties.Settings.Default.TraconBoundaryReleaseTag)
    ? "—" : Properties.Settings.Default.TraconBoundaryReleaseTag;
VatSpyTag = string.IsNullOrEmpty(Properties.Settings.Default.VatSpyReleaseTag)
    ? "—" : Properties.Settings.Default.VatSpyReleaseTag;
```

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/ViewModels/SettingsViewModel.cs
git commit -m "feat: add About display properties to SettingsViewModel"
```

---

## Task 4: Add About footer to SettingsView.xaml

**Files:**
- Modify: `FSTRaK/Views/SettingsView.xaml`
- Modify: `FSTRaK/Views/SettingsView.xaml.cs`

The outer structure change: wrap the existing `<StackPanel Orientation="Horizontal">` in a `<DockPanel>`, then add a footer `<Border>` docked to the bottom. The footer contains three horizontal groups: App Identity, Data Files, Credits.

Brushes used (defined in both `Theme.xaml` and `DarkTheme.xaml`):
- `{DynamicResource ApplicationHeaderBackgroundColorBrush}` — footer background
- `{DynamicResource TextColor}` — primary text (version values, credit names)
- `{DynamicResource UnselectedTextColor}` — secondary/label text

- [ ] **Step 1: Wrap existing columns in DockPanel and add footer**

In `FSTRaK/Views/SettingsView.xaml`, replace the outermost element — the entire `<StackPanel Orientation="Horizontal">...</StackPanel>` — with the following (the two existing inner `<StackPanel Margin="10">` columns are preserved unchanged inside the inner `<StackPanel Orientation="Horizontal">`):

```xml
<DockPanel>
    <!-- About footer docked to the bottom -->
    <Border DockPanel.Dock="Bottom"
            Background="{DynamicResource ApplicationHeaderBackgroundColorBrush}"
            BorderThickness="0,1,0,0"
            BorderBrush="{DynamicResource ControlBackgroundColorBrush}"
            Padding="20,12">
        <StackPanel Orientation="Horizontal">

            <!-- Group 1: App Identity -->
            <StackPanel Margin="0,0,40,0" VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal">
                    <TextBlock FontFamily="{DynamicResource CurrentFont}"
                               FontSize="{DynamicResource LabelFontSize}"
                               FontWeight="Bold"
                               Foreground="{DynamicResource TextColor}"
                               Text="FSTrAk "/>
                    <TextBlock FontFamily="{DynamicResource CurrentFont}"
                               FontSize="{DynamicResource ControlFontSize}"
                               Foreground="{DynamicResource UnselectedTextColor}"
                               VerticalAlignment="Center"
                               Text="{Binding AppVersion}"/>
                </StackPanel>
                <TextBlock FontFamily="{DynamicResource CurrentFont}"
                           FontSize="{DynamicResource ListFontSize}"
                           Foreground="{DynamicResource UnselectedTextColor}"
                           Text="Modern flight tracker and logbook for MSFS"
                           Margin="0,3,0,5"/>
                <TextBlock FontFamily="{DynamicResource CurrentFont}"
                           FontSize="{DynamicResource ListFontSize}">
                    <Hyperlink NavigateUri="https://github.com/o4oren/FSTRaK"
                               RequestNavigate="Hyperlink_RequestNavigate">
                        github.com/o4oren/FSTRaK
                    </Hyperlink>
                </TextBlock>
            </StackPanel>

            <!-- Group 2: Data Files -->
            <StackPanel Margin="0,0,40,0" VerticalAlignment="Center">
                <TextBlock FontFamily="{DynamicResource CurrentFont}"
                           FontSize="{DynamicResource ListFontSize}"
                           FontWeight="Bold"
                           Foreground="{DynamicResource TextColor}"
                           Margin="0,0,0,5"
                           Text="DATA FILES"/>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="10"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <TextBlock Grid.Row="0" Grid.Column="0" FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource UnselectedTextColor}" Text="FIR Boundaries"/>
                    <TextBlock Grid.Row="0" Grid.Column="2" FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource TextColor}" Text="{Binding FirBoundaryTag}"/>

                    <TextBlock Grid.Row="1" Grid.Column="0" FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource UnselectedTextColor}" Text="TRACON Boundaries"/>
                    <TextBlock Grid.Row="1" Grid.Column="2" FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource TextColor}" Text="{Binding TraconBoundaryTag}"/>

                    <TextBlock Grid.Row="2" Grid.Column="0" FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource UnselectedTextColor}" Text="VATSpy"/>
                    <TextBlock Grid.Row="2" Grid.Column="2" FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource TextColor}" Text="{Binding VatSpyTag}"/>
                </Grid>
            </StackPanel>

            <!-- Group 3: Credits -->
            <StackPanel VerticalAlignment="Center">
                <TextBlock FontFamily="{DynamicResource CurrentFont}"
                           FontSize="{DynamicResource ListFontSize}"
                           FontWeight="Bold"
                           Foreground="{DynamicResource TextColor}"
                           Margin="0,0,0,5"
                           Text="CREDITS"/>
                <TextBlock FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource UnselectedTextColor}">
                    <Run FontWeight="SemiBold" Foreground="{DynamicResource TextColor}">VATSpy Data Project</Run>
                    <Run> by VATSIM Network (CC BY-SA 4.0)</Run>
                </TextBlock>
                <TextBlock FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource UnselectedTextColor}">
                    <Run FontWeight="SemiBold" Foreground="{DynamicResource TextColor}">SimAware TRACON Project</Run>
                    <Run> by VATSIM Network (CC BY-SA 4.0)</Run>
                </TextBlock>
                <TextBlock FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" Foreground="{DynamicResource UnselectedTextColor}">
                    <Run>Airport icons by </Run>
                    <Run FontWeight="SemiBold" Foreground="{DynamicResource TextColor}">Freepik</Run>
                    <Run> — Flaticon</Run>
                </TextBlock>
            </StackPanel>

        </StackPanel>
    </Border>

    <!-- Existing two-column settings area -->
    <StackPanel Orientation="Horizontal">
        <!-- PASTE EXISTING COL 1 StackPanel HERE (unchanged) -->
        <!-- PASTE EXISTING COL 2 StackPanel HERE (unchanged) -->
    </StackPanel>
</DockPanel>
```

> **Note:** The two inner `<StackPanel Margin="10">` blocks (Col 1 and Col 2) from the original are moved as-is into the inner `<StackPanel Orientation="Horizontal">`. Do not modify their content.

- [ ] **Step 2: Add RequestNavigate handler in code-behind**

In `FSTRaK/Views/SettingsView.xaml.cs`, add the following method (add `using System.Diagnostics;` at the top if not already present):

```csharp
private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
{
    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
    e.Handled = true;
}
```

- [ ] **Step 3: Build and visually verify**

Open the solution in Visual Studio and build `Debug|x64`. Run the app, navigate to Settings. Confirm:
- The About footer is visible at the bottom spanning full width
- App name and version (e.g., `v3.2.1`) are shown
- Data file tags show the stored values (or `—` if not yet downloaded)
- Clicking the GitHub hyperlink opens the browser
- Credits show the three required attributions
- Layout looks correct in both Normal and Dark themes

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/Views/SettingsView.xaml FSTRaK/Views/SettingsView.xaml.cs
git commit -m "feat: add About footer to SettingsView"
```

---

## Task 5: Final integration commit

- [ ] **Step 1: Verify all changes together**

Run the app in Debug mode. Navigate to Settings. Then:
1. Delete `%LOCALAPPDATA%\FSTRaK_DEBUG\Data\VATSpy.dat` (to force a re-download on next startup)
2. Restart the app
3. Open Settings — confirm `VATSpy` tag now shows a release tag like `v2602.2` instead of `—`
4. Confirm FIR and TRACON tags also display correctly

- [ ] **Step 2: Final commit**

```bash
git add -A
git commit -m "feat: About section in SettingsView with data file version tags"
```
