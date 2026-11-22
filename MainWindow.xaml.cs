using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VigilanteMusic.DataBase;
using VigilanteMusic.Helpers;
using VigilanteMusic.SoundParser;
using VigilanteMusic.Utils;

namespace VigilanteMusic
{
    public partial class MainWindow : Window
    {
        private readonly LibraryManager _library = new LibraryManager();
        private MusicPlayer player = new MusicPlayer();

        bool IsPause = false;

        public MainWindow()
        {
            InitializeComponent();

            player.Update += OnPlayerTrackChanged;

            Closed += (s, e) => player.Dispose();

            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            await System.Threading.Tasks.Task.Run(() => _library.ReloadLocalTracks());

            try
            {
                var connector = new DBConnector();
                string error;
                if (connector.TestConnection(out error))
                {
                    var db = new DBReader(connector);
                    var infos = await System.Threading.Tasks.Task.Run(() => MusicNet.GetAllInfo(db, true));
                    _library.SetDatabaseTracks(infos.Select(i => new MusicNet(i.Id, db)));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Error: {ex.Message}");
            }

            _library.ReloadSavedPlaylists();
            RefreshPlaylistBox();
            RefreshTargetBox();
        }

        private void RefreshPlaylistBox()
        {
            string selected = PlaylistBox.SelectedItem as string;

            PlaylistBox.Items.Clear();
            foreach (string name in _library.GetPlaylistNames())
                PlaylistBox.Items.Add(name);

            PlaylistBox.SelectedItem = selected ?? LibraryManager.AllPlaylistName;
        }

        /// <summary>Fills the combo with playlists that can receive tracks.</summary>
        private void RefreshTargetBox()
        {
            string selected = TargetPlaylistBox.SelectedItem as string;

            TargetPlaylistBox.Items.Clear();
            foreach (string name in _library.GetPlaylistNames())
            {
                if (_library.IsEditable(name))
                    TargetPlaylistBox.Items.Add(name);
            }

            if (selected != null && TargetPlaylistBox.Items.Contains(selected))
                TargetPlaylistBox.SelectedItem = selected;
            else if (TargetPlaylistBox.Items.Count > 0)
                TargetPlaylistBox.SelectedIndex = 0;
        }

        /// <summary>Rebuilds the right list from the current playlist.</summary>
        private void ReloadCurrentPlaylist()
        {
            var tracks = _library.GetTracksOf(_library.CurrentPlaylist);

            TrackQueueList.Items.Clear();
            foreach (var t in tracks) TrackQueueList.Items.Add(t);

            player.PL.Swap(new List<Music>(tracks));

            TrackQueueList.SelectedIndex = -1;
            LblTitle.Text = "Select a track";
            LblArtist.Text = "";
            CoverImage.Source = null;
        }

        // ------------------------------------------------------------------ //
        // Selection
        // ------------------------------------------------------------------ //

        private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string name = PlaylistBox.SelectedItem as string;
            if (name == null) return;

            _library.SwitchTo(name);
            ReloadCurrentPlaylist();
        }

        private void TrackQueueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TrackQueueList.SelectedItem is Music selectedTrack)
            {
                int index = TrackQueueList.SelectedIndex;
                player.PL.SetCurrent(index);

                UpdateUiFromTrack(selectedTrack);
                PauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                  new Uri("/VigilanteMusic;component/Source/Icon/UnPause.png", UriKind.Relative));
                IsPause = false;
                player.Play(selectedTrack);
            }
        }

        private void UpdateUiFromTrack(Music track)
        {
            if (track == null) return;

            LblTitle.Text = track.Title ?? "Unknown Title";
            LblArtist.Text = track.Artist ?? "Unknown Artist";

            if (track.Info?.Picture != null && track.Info.Picture.Length > 0)
            {
                try
                {
                    var bmp = new BitmapImage();
                    using (var ms = new MemoryStream(track.Info.Picture))
                    {
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                    CoverImage.Source = bmp;
                }
                catch { CoverImage.Source = null; }
            }
            else
            {
                CoverImage.Source = null;
            }
        }

        private void OnPlayerTrackChanged()
        {
            Dispatcher.Invoke(() =>
            {
                Music current = player.PL.CurrentMusic;
                UpdateUiFromTrack(current);
                if (current != null)
                {
                    int idx = TrackQueueList.Items.IndexOf(current);
                    if (idx >= 0)
                    {
                        TrackQueueList.SelectedIndex = idx;
                    }
                }
            });
        }

        // ------------------------------------------------------------------ //
        // Playback buttons
        // ------------------------------------------------------------------ //

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (player.PL.Back(out Music prevTrack))
            {
                player.Play(prevTrack);
                UpdateUiFromTrack(prevTrack);
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (player.PL.Next(out Music nextTrack))
            {
                player.Play(nextTrack);
                UpdateUiFromTrack(nextTrack);
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            IsPause = !IsPause;
            if (IsPause)
            {
                player.Resume();
                PauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                   new Uri("/VigilanteMusic;component/Source/Icon/UnPause.png", UriKind.Relative));
            }
            else
            {
                PauseIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
                  new Uri("/VigilanteMusic;component/Source/Icon/Pause.png", UriKind.Relative));

                player.Pause();
            }
        }

        private void BtnShuffle_Click(object sender, RoutedEventArgs e)
        {
            player.IsShuffle = !player.IsShuffle;
            BtnShuffle.Background = player.IsShuffle ? System.Windows.Media.Brushes.Green : null;
        }

        private void BtnRepeat_Click(object sender, RoutedEventArgs e)
        {
            player.IsRepeat = !player.IsRepeat;
            BtnRepeat.Background = player.IsRepeat ? System.Windows.Media.Brushes.Green : null;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (player == null) return;
            player.Volume = MyMath.Clamp(0, 1, Convert.ToSingle(VolumeSlider.Value)) / 6;
        }

        // ------------------------------------------------------------------ //
        // Library management
        // ------------------------------------------------------------------ //

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            // WPF has no folder picker, so the file dialog is used in "pick folder" mode:
            // the user navigates into the target folder and confirms with any file name.
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a folder (enter any name and press Open)",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select this folder",
                Filter = "Folder|*.folder"
            };

            if (dlg.ShowDialog() != true) return;

            string folder = System.IO.Path.GetDirectoryName(dlg.FileName);
            if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder)) return;

            int added = _library.AddScanFolder(folder);
            MessageBox.Show($"Added {added} tracks from:\n{folder}");

            if (string.Equals(_library.CurrentPlaylist, LibraryManager.AllPlaylistName,
                              StringComparison.OrdinalIgnoreCase))
                ReloadCurrentPlaylist();
        }

        private void BtnNewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            string name = PromptText("Playlist name:", "New playlist", "My mix");
            if (string.IsNullOrWhiteSpace(name)) return;

            if (!_library.CreatePlaylist(name))
            {
                MessageBox.Show("Name is reserved or already used.");
                return;
            }

            RefreshPlaylistBox();
            RefreshTargetBox();
            PlaylistBox.SelectedItem = name;
        }

        private void BtnDeletePlaylist_Click(object sender, RoutedEventArgs e)
        {
            string name = PlaylistBox.SelectedItem as string;
            if (!_library.IsEditable(name))
            {
                MessageBox.Show("This playlist cannot be deleted.");
                return;
            }

            if (MessageBox.Show($"Delete '{name}'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            _library.DeletePlaylist(name);
            RefreshPlaylistBox();
            RefreshTargetBox();
        }

        private void BtnAddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var picked = TrackQueueList.SelectedItems.Cast<Music>().ToList();
            if (picked.Count == 0)
            {
                MessageBox.Show("Select tracks first.");
                return;
            }

            string target = TargetPlaylistBox.SelectedItem as string;
            if (string.IsNullOrEmpty(target))
            {
                MessageBox.Show("Create a playlist first.");
                return;
            }

            int added = _library.AddTracks(target, picked);
            MessageBox.Show($"Added {added} tracks to '{target}'.");

            if (string.Equals(_library.CurrentPlaylist, target, StringComparison.OrdinalIgnoreCase))
                ReloadCurrentPlaylist();
        }

        private void BtnRemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            string name = _library.CurrentPlaylist;
            if (!_library.IsEditable(name))
            {
                MessageBox.Show("ALL is generated automatically and cannot be edited.");
                return;
            }

            var indexes = GetSelectedIndices();
            if (indexes.Count == 0)
            {
                MessageBox.Show("Select tracks first.");
                return;
            }

            _library.RemoveTracksAt(name, indexes);
            ReloadCurrentPlaylist();
        }

        /// <summary>WPF ListBox has no SelectedIndices, so map items back to their positions.</summary>
        private List<int> GetSelectedIndices()
        {
            var result = new List<int>();
            foreach (object item in TrackQueueList.SelectedItems)
            {
                int index = TrackQueueList.Items.IndexOf(item);
                if (index >= 0) result.Add(index);
            }
            return result;
        }

        // ------------------------------------------------------------------ //
        // Small input dialog built in code, no extra XAML file needed
        // ------------------------------------------------------------------ //

        private static string PromptText(string label, string title, string defaultValue)
        {
            var window = new Window
            {
                Title = title,
                Width = 340,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White,
                Owner = Application.Current.MainWindow
            };

            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) });

            var input = new TextBox { Text = defaultValue };
            panel.Children.Add(input);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var ok = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
            var cancel = new Button { Content = "Cancel", Width = 70, IsCancel = true };

            ok.Click += (s, e) => { window.DialogResult = true; };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            window.Content = panel;
            input.Focus();
            input.SelectAll();

            return window.ShowDialog() == true ? input.Text : null;
        }
    }
}