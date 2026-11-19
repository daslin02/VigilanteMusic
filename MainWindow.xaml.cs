using System;
using System.Collections.Generic;
using System.Windows;
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
            // Станет:
            string track1 = "DrivingForceNeonFog.mp3";
            player.PL.Push("D:/Music/" + track1);

            // Первый альбом
            List<string> trackNames = new List<string>
            {
                "DrivingForceNeonFog.mp3",
                "Infiltrator.mp3",
                "LetThemKnow.mp3",
                "TheOrb.mp3",
                "TruthToPower.mp3",
                "Air(Cybervoid).mp3",
                "AlreadyDead.mp3",
                "HumanLikeFeatures.mp3",
                "YesDucking.mp3",
                "SystemOverload.mp3",
                "SneakyDriver.mp3",
                "HittheFloor.mp3",
                "TheMark.mp3",
                "Razormind.mp3",
                "EvilEye.mp3",
                "Horizon.mp3",
                "IsThantPhonkInAnOriginalSoundtrack.mp3",
                "PetNursery.mp3",
                "WelcomingFolks.mp3",
                "WhatAreYouDoingInMySwamp.mp3",
                "WhatFaithGivesYou.mp3"
            };

            // 2. Проходим по списку циклом и делаем сразу два дела:
            
            string folderPath = "D:/Music/";

            foreach (string track in trackNames)
            {
                player.PL.Push(folderPath + track);
                TrackQueueList.Items.Add(track);
            }
            TrackQueueList.Items.Add(track1);
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
        }
    }
}
