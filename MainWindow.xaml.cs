using System;
using System.Collections.Generic;
using System.Windows;
using TagLib.Id3v2;
using VigilanteMusic.Config;
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
            // Иниц. плеер 
            player = new MusicPlayer();
            player.PL = new PlayList();

            MusicInfo info = player.PL.CurrentMusicInfo;
            InitializeComponent(); // Загруз. XAML

            MusicFolderStore store = Config.MusicFolderStore.Load();
            store.Folders.Add("D:/SteamLibrary/steamapps/music");
            store.Save();
            //store.save - чтобы сохранить, tostring для переделки в строку
            //store.folder - получение всех папок где может лежать музыку
            MusicScanner.GetAllMusic(); //возврат указанной папки и базовой папки музыки винды
            List<string> SteamMusic = MusicScanner.GetAllMusic(); //SteamMusic хранит всю музыку из этой папки


            //CurrentTrackLabel.Content = (SteamMusic);

            foreach (string track in SteamMusic)
            {
                player.PL.Push(track);
                MusicInfo infothing = Music.GetInfo(track);
                TrackQueueList.Items.Add(infothing.Title);

            }
            //TrackQueueList.Items.Add(track1);


            player.Volume = 0.05f;


            string first = null;
            player.PL.GetMusicByIndex(0, ref first);
            player.Play(first);

            //перекл на след трек
            btnNext.Click += (s, e) =>
            {
                if (player.PL.Next(out string p))
                    player.RaiseUpdate();
                MusicInfo MusicInformation = player.PL.CurrentMusicInfo;
                CurrentTrackLabel.Content = MusicInformation.ToString();


            };

            //перекл на прошл трек
            btnBack.Click += (s, e) =>
            {
                if (player.PL.Back(out string p))
                    player.RaiseUpdate();
                MusicInfo MusicInformation = player.PL.CurrentMusicInfo;
                CurrentTrackLabel.Content = MusicInformation.ToString();

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
