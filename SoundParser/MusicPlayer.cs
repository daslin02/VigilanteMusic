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

        public PlayList _playList;

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
            _output = new WaveOutEvent();
            _output.Init(_reader);
            _output.Play();
        }
        public void Stop()
        {
            _output?.Stop();
            _output?.Dispose();
            _reader?.Dispose();
            _output = null;
            _reader = null;
        }
        public void Pause() => _output?.Pause();

        public void Resume() => _output?.Play();
        public void Dispose() => Stop();

    }
}
