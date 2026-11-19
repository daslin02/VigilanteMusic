using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

using VigilanteMusic.Utils;

namespace VigilanteMusic.SoundParser
{
    internal class MusicPlayer : IDisposable
    {
        private WaveOutEvent _output;
        private AudioFileReader _reader;
        private float _volume = 1.0f;

        public PlayList PL;

        public event Action Update;

        public bool IsRepeat = false;

        public MusicPlayer()
        {
            PL = new PlayList();
        }
        public bool IsShuffle  {get => PL.IsShuffle; set => PL.IsShuffle = value;} 
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = MyMath.Clamp( 0f, 1f, value);
                if (_reader != null)
                    _reader.Volume = _volume;
            }
        }
        
        public void Play(string filePath)
        {
            Stop();

            _reader = new AudioFileReader(filePath);
            _reader.Volume = _volume;
            _output = new WaveOutEvent();
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_reader);
            _output.Play();
        }
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null || _output == null) return;

            if (IsRepeat)
            {
                _reader.Position = 0;
                _output.Play();
                return;
            }

            string path = null;

            if (PL != null && PL.Next(out path))
            {
                Play(path);
            }
            else if (PL != null && PL.IsVerified && PL.GetMusicByIndex(0, ref path))
            {
                Play(path);
            }
        }
        public void Stop()
        {
            if (_output != null)
            {
                _output.PlaybackStopped -= OnPlaybackStopped;
                _output.Stop();
            }
            _output?.Dispose();
            _reader?.Dispose();
            _output = null;
            _reader = null;
        }
        /// <summary>
        /// Call this after PL.Next / PL.Back to start the new current track
        /// </summary>
        public void RaiseUpdate()
        {
            if (PL == null) return;

            string path = null;
            if (PL.GetMusicByIndex(PL.CurrentPlaying, ref path))
            {
                Play(path);
            }
        }
        public void Pause() => _output?.Pause();

        public void Resume() => _output?.Play();
        public void Dispose() => Stop();

    }
}
