using System;
using System.Collections.Generic;
using System.Windows;
using VigilanteMusic.Helpers;
using VigilanteMusic.SoundParser;

namespace VigilanteMusic
{
    public partial class MainWindow : Window
    {
        private MusicPlayer player;

        private bool paused = false;

        public MainWindow()
        {
            InitializeComponent(); // Загруз. XAML

            // Иниц. плеер 
            player = new MusicPlayer();
            player.PL = new PlayList();


            var store = Config.MusicFolderStore.Load();
            List<string> allTrack =  MusicScanner.GetAllMusic();


            MusicInfo info =  player.PL.CurrentMusicInfo;
            foreach (string track in allTrack)
            {
                player.PL.Push(track);
                Console.WriteLine(track);
                TrackQueueList.Items.Add(track);
            }
            //TrackQueueList.Items.Add(track1);
            player.Volume = 0.1f;   

            string first = null;
            player.PL.GetMusicByIndex(0, ref first);
            player.Play(first);

            //перекл на след трек
            btnNext.Click += (s, e) =>
            {
                if (player.PL.Next(out string p))
                    player.RaiseUpdate();
                CurrentTrackLabel.Content = p;

            };

            //перекл на прошл трек
            btnBack.Click += (s, e) =>
            {
                if (player.PL.Back(out string p))
                    player.RaiseUpdate();
                CurrentTrackLabel.Content = p;
            };

            //повтор очереди
            btnRepeat.Click += (s, e) =>
            {
                player.IsRepeat = !player.IsRepeat;
                btnRepeat.Content = player.IsRepeat ? "Repeat: ON" : "Repeat: OFF";
            };

            //пауза
            btnPause.Click += (s, e) =>
            {
                if (paused)
                    player.Resume();
                else
                    player.Pause();

                paused = !paused;
            };
            btnShuffle.Click += (s, e) =>
            {
                player.IsShuffle = !player.IsShuffle;
                btnShuffle.Content = player.IsShuffle? "Shuffle: ON" : "Shuffle: OFF";
            };
        }
    }
}
