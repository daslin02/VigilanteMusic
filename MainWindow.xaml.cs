using System;
using System.Collections.Generic;
using System.IO;
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
        private MusicPlayer player = new MusicPlayer();

        private List<Music> _allLocalTracks = new List<Music>();
        private List<Music> _allDbTracks = new List<Music>();

        bool IsPause = false;
        public MainWindow()
        {
            InitializeComponent();

            
            player.Update += OnPlayerTrackChanged;

            LoadDataAsync();
        }

        private async void LoadDataAsync() 
        {
            var localPaths = MusicScanner.GetAllMusic();
            foreach (var path in localPaths)
                _allLocalTracks.Add(new Music(path));

            PlaylistBox.Items.Add("ALL");

            try
            {
                DBConnector connector = new DBConnector();
                string error;
                if (connector.TestConnection(out error))
                {
                    DBReader db = new DBReader(connector);
                    var dbInfos = MusicNet.GetAllInfo(db, withCover: true);
                    foreach (var info in dbInfos)
                        _allDbTracks.Add(new MusicNet(info.Id, db));
                }
                PlaylistBox.Items.Add("DataBase");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Error: {ex.Message}");
            }

        }

        private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistBox.SelectedItem == null) return;

            string playlistName = PlaylistBox.SelectedItem.ToString();
            List<Music> sourceList = null;

            if (string.Equals(playlistName, "DataBase", StringComparison.OrdinalIgnoreCase))
            {
                sourceList = _allDbTracks;
            }
            else if (string.Equals(playlistName, "All", StringComparison.OrdinalIgnoreCase))
            {
                sourceList = new List<Music>(_allLocalTracks);
                sourceList.AddRange(_allDbTracks);
            }

            if (sourceList == null) return;

            TrackQueueList.Items.Clear();
            foreach (var track in sourceList)
                TrackQueueList.Items.Add(track);

            player.PL.Swap(new List<Music>(sourceList));

            TrackQueueList.SelectedIndex = -1;
            LblTitle.Text = "Select a track";
            LblArtist.Text = "";
            CoverImage.Source = null;
        }

        private void TrackQueueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TrackQueueList.SelectedItem is Music selectedTrack)
            {
                int index = TrackQueueList.SelectedIndex;

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
            player.Volume =  MyMath.Clamp ( 0, 1, Convert.ToSingle( VolumeSlider.Value )) /6 ;
        }
    }
}