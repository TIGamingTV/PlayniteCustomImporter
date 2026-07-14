using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using PlayniteCustomImporter.Import;
using PlayniteCustomImporter.Settings;

namespace PlayniteCustomImporter
{
    public class PlayniteCustomImporterPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PlayniteCustomImporterSettingsViewModel settingsViewModel;

        public override Guid Id { get; } = Guid.Parse("b7e2f4a9-3c1d-4e8a-9f26-5d0c7a1b8e34");

        public PlayniteCustomImporterPlugin(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new PlayniteCustomImporterSettingsViewModel(this, api);
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PlayniteCustomImporterSettingsView();
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Title = "Import Game",
                Type = SiderbarItemType.Button,
                Icon = BuildPlusIcon(),
                Activated = OpenImportWindow
            };
        }

        /// <summary>
        /// Builds the sidebar icon as a plus (+) glyph. A fresh <see cref="TextBlock"/> is returned on
        /// each call because a WPF element cannot be shared between visual trees.
        /// </summary>
        private static TextBlock BuildPlusIcon()
        {
            return new TextBlock
            {
                Text = "+",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void OpenImportWindow()
        {
            try
            {
                var viewModel = new ImportWizardViewModel(PlayniteApi, settingsViewModel.Settings);
                var window = new ImportWizardWindow { DataContext = viewModel };
                viewModel.CloseRequested += (_, __) => window.Close();
                window.Owner = System.Windows.Application.Current?.MainWindow;
                window.ShowDialog();

                // Opened after the wizard closes so Playnite's editor is a top-level modal rather than
                // nested inside our (closing) dialog. This is the closest the SDK offers to Playnite's
                // automatic metadata download — the editor's "Download Metadata" is one click away.
                if (viewModel.ImportedGame != null && settingsViewModel.Settings.OpenMetadataAfterImport)
                {
                    try
                    {
                        PlayniteApi.MainView.OpenEditDialog(viewModel.ImportedGame.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to open the game editor after import.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to open the import window.");
                PlayniteApi.Dialogs.ShowErrorMessage(ex.Message, "Custom Importer");
            }
        }
    }
}
