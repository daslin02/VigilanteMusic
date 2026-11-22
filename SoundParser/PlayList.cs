using System;
using System.Collections.Generic;
using VigilanteMusic.Utils;

namespace VigilanteMusic.SoundParser
{
    public class PlayList
    {
        private  List<Music> _tracks;
        private readonly List<int> _playedIndexes;
        private readonly Random _random = new Random();
        private int _CurrentPlaying;

        /// <summary>Pick a random track instead of the next one in order.</summary>
        public bool IsShuffle { get; set; } = false;

        /// <summary>Avoid repeating the current track when shuffling.</summary>
        public bool ShuffleAvoidRepeat { get; set; } = true;

        /// <summary>How many played tracks are kept for Back navigation.</summary>
        public int HistoryLimit { get; set; } = 200;

        public bool CanGoBack => _playedIndexes.Count > 0;

        public PlayList()
        {
            _tracks = new List<Music>();
            _playedIndexes = new List<int>();
            _CurrentPlaying = -1;
        }

        public int CurrentPlaying => _CurrentPlaying;

        /// <summary>
        /// Metadata of the current track. Comes from the Music instance, no cache needed:
        /// local tracks read tags lazily, database tracks already hold their row.
        /// </summary>
        public MusicInfo CurrentMusicInfo
        {
            get
            {
                Music current = CurrentMusic;
                return current != null ? current.Info : null;
            }
        }

        /// <summary>The track object at the current position, or null.</summary>
        public Music CurrentMusic
        {
            get
            {
                if (_CurrentPlaying < 0 || _CurrentPlaying >= _tracks.Count) return null;
                return _tracks[_CurrentPlaying];
            }
        }

        /// <summary>
        /// this the variable checking validate MusicPlayList Paths
        /// </summary>
        public bool IsVerified
        {
            get { return _tracks != null && _tracks.Count > 0; }
        }

        public int Length
        {
            get { return _tracks.Count; }
        }

        /// <summary>
        /// Getting Music By index on the playList
        /// </summary>
        /// <param name="index">Number PlayList</param>
        /// <param name="RetMusic">the track object stored at that position</param>
        /// <returns>the success status of the operation</returns>
        public bool GetMusicByIndex(int index, ref Music RetMusic)
        {
            if (!IsVerified) return false;

            RetMusic = _tracks[MyMath.Clamp(0, _tracks.Count - 1, index)];
            return true;
        }

        /// <summary>
        /// Overload that returns the playable path, calling Load on the track.
        /// For database tracks this materializes the blob into the cache.
        /// </summary>
        public bool GetPathByIndex(int index, ref string RetString)
        {
            Music track = null;
            if (!GetMusicByIndex(index, ref track) || track == null) return false;

            RetString = track.Load();
            return !string.IsNullOrEmpty(RetString);
        }

        /// <summary>
        /// Get next Music in the play list.
        /// When shuffle is on, picks a random track; otherwise moves to the following index.
        /// The track being left is pushed into the history so Back can return to it.
        /// </summary>
        /// <param name="RetMusic">the track that should be played next</param>
        /// <returns>the success status of the operation</returns>
        public bool Next(out Music RetMusic)
        {
            RetMusic = null;

            if (!IsVerified) return false;

            // remember the index we are leaving
            if (_CurrentPlaying >= 0 && _CurrentPlaying < _tracks.Count)
                PushHistory(_CurrentPlaying);

            int next;

            if (IsShuffle)
            {
                next = PickRandomIndex(_CurrentPlaying);
            }
            else
            {
                next = _CurrentPlaying + 1;
                if (next >= _tracks.Count) next = 0;   // wrap to the beginning
            }

            if (next < 0 || next >= _tracks.Count) return false;

            RetMusic = _tracks[next];
            _CurrentPlaying = next;
            return true;
        }

        /// <summary>
        /// Get the previously played track from the history.
        /// Works correctly with shuffle: returns what actually played, not the neighbouring index.
        /// Falls back to index navigation when the history is empty.
        /// </summary>
        /// <param name="RetMusic">the track that should be played</param>
        /// <returns>the success status of the operation</returns>
        public bool Back(out Music RetMusic)
        {
            RetMusic = null;

            if (!IsVerified) return false;

            // history mode: pop the last played index
            while (_playedIndexes.Count > 0)
            {
                int index = _playedIndexes[_playedIndexes.Count - 1];
                _playedIndexes.RemoveAt(_playedIndexes.Count - 1);

                // skip entries that were removed from the playlist since they played
                if (index < 0 || index >= _tracks.Count) continue;

                _CurrentPlaying = index;
                RetMusic = _tracks[index];
                return true;
            }

            // fallback: no history yet, just step one index back
            int back = _CurrentPlaying - 1;
            if (back < 0) back = _tracks.Count - 1;

            RetMusic = _tracks[back];
            _CurrentPlaying = back;
            return true;
        }

        /// <summary>
        /// Get index of a track by its source path
        /// </summary>
        /// <param name="path">path of music on the system</param>
        /// <param name="index">index on the playlist</param>
        /// <returns>the success status of the operation</returns>
        public bool GetIndexMusicByPath(string path, ref int index)
        {
            int found = IndexOfPath(path);
            if (found < 0) return false;

            index = found;
            return true;
        }

        /// <summary>
        /// Jumps to a specific index, remembering the current track in the history.
        /// </summary>
        public bool SetCurrent(int index)
        {
            if (!IsVerified || index < 0 || index >= _tracks.Count) return false;

            if (_CurrentPlaying >= 0 && _CurrentPlaying < _tracks.Count && _CurrentPlaying != index)
                PushHistory(_CurrentPlaying);

            _CurrentPlaying = index;
            return true;
        }

        /// <summary>Clears the playback history.</summary>
        public void ClearHistory() => _playedIndexes.Clear();

        /// <summary>Adds an existing track object, local or database backed.</summary>
        public void Push(Music track)
        {
            if (track == null) return;
            _tracks.Add(track);

            if (_CurrentPlaying < 0) _CurrentPlaying = 0;
        }

        /// <summary>Convenience overload for local files.</summary>
        public void Push(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            Push(new Music(path));
        }

        public void Clear()
        {
            _tracks.Clear();
            _playedIndexes.Clear();
            _CurrentPlaying = -1;
        }

        /// <summary>
        /// Remove Music by index on the playList
        /// </summary>
        /// <param name="index">index on the playlist</param>
        /// <returns>the success status of the operation</returns>
        public bool RemoveByIndex(int index)
        {
            if (!IsVerified) return false;
            if (index < 0 || index >= _tracks.Count) return false;

            _tracks.RemoveAt(index);

            _playedIndexes.RemoveAll(i => i == index);
            for (int i = 0; i < _playedIndexes.Count; i++)
                if (_playedIndexes[i] > index) _playedIndexes[i]--;

            if (_tracks.Count == 0)
            {
                _CurrentPlaying = -1;
            }
            else if (_CurrentPlaying > index)
            {
                _CurrentPlaying--;
            }
            else if (_CurrentPlaying >= _tracks.Count)
            {
                _CurrentPlaying = _tracks.Count - 1;
            }

            return true;
        }

        /// <summary>
        /// Remove last Music on the playList
        /// </summary>
        /// <returns>the success status of the operation</returns>
        public bool Pop()
        {
            return RemoveByIndex(_tracks.Count - 1);
        }

        /// <summary>Adds an index to the history and trims it to HistoryLimit.</summary>
        private void PushHistory(int index)
        {
            if (index < 0) return;

            _playedIndexes.Add(index);

            if (HistoryLimit > 0 && _playedIndexes.Count > HistoryLimit)
                _playedIndexes.RemoveAt(0);
        }

        /// <summary>
        /// Returns a random index. With a single track it returns that track.
        /// When ShuffleAvoidRepeat is on, never returns the current index.
        /// </summary>
        private int PickRandomIndex(int currentIndex)
        {
            int count = _tracks.Count;
            if (count == 1) return 0;

            if (!ShuffleAvoidRepeat || currentIndex < 0 || currentIndex >= count)
                return _random.Next(count);

            // pick from count-1 slots and shift past the current index
            int pick = _random.Next(count - 1);
            if (pick >= currentIndex) pick++;

            return pick;
        }

        /// <summary>Finds a track by its source path, ignoring case.</summary>
        private int IndexOfPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return -1;

            for (int i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i] == null) continue;
                if (string.Equals(_tracks[i].FilePath, path, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
        /// <summary>
        /// Update Play List track
        /// </summary>
        /// <param name="tracks"> New Play List track</param>
        public void Swap(List<Music> tracks) 
        {
            this._CurrentPlaying = 0;
            this._tracks = tracks;

        }
    }
}