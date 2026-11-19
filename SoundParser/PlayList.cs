using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VigilanteMusic.Utils;



namespace VigilanteMusic.SoundParser
{
    public class PlayList
    {
        private List<string> MusicPaths;
        private Dictionary<string, MusicInfo> _musicCache; 
        private int _CurrentPlaying;
        private readonly List<string> PlayedMusic = new List<string>();
        private readonly Random _random = new Random();

        /// <summary>Pick a random track instead of the next one in order.</summary>
        public bool IsShuffle { get; set; } = false;

        /// <summary>Avoid repeating the current track when shuffling.</summary>
        public bool ShuffleAvoidRepeat { get; set; } = true;

        /// <summary>How many played tracks are kept for Back navigation.</summary>
        public int HistoryLimit { get; set; } = 200;

        public bool CanGoBack => PlayedMusic.Count > 0;

        public PlayList()
        {
            MusicPaths = new List<string>();
            PlayedMusic = new List<string>();
            _musicCache = new Dictionary<string, MusicInfo>();
        }

        public int CurrentPlaying => _CurrentPlaying;

        /// <summary>
        /// Get MetaDate of current track
        /// </summary>
        public MusicInfo CurrentMusicInfo
        {
            get
            {
                if (!IsVerified || _CurrentPlaying < 0 || _CurrentPlaying >= MusicPaths.Count)
                    return null;

                string path = MusicPaths[_CurrentPlaying];
                if (!_musicCache.ContainsKey(path))
                {
                    _musicCache[path] = Music.GetInfo(path);
                }
                return _musicCache[path];
            }
        }

        /// <summary>
        /// this the variable checking validate MusicPlayList Paths
        /// </summary>
        public bool IsVerified
        {
            get { return MusicPaths != null && MusicPaths.Count > 0; }
        }

        public int Length
        {
            get { return MusicPaths.Count; }
        }

        /// <summary>
        /// Getting Music By index on the playList
        /// </summary>
        /// <param name="index">Number PlayList</param>
        /// <param name="RetString">this the path of track on the system</param>
        /// <returns>the success status of the operation</returns>
        public bool GetMusicByIndex(int index, ref string RetString)
        {
            if (!IsVerified)
            {
                return false;
            }
            RetString = MusicPaths[MyMath.Clamp(0, MusicPaths.Count - 1, index)];
            return true;
        }

        /// <summary>
        /// Get next Music in the play list.
        /// When shuffle is on, picks a random track; otherwise moves to the following index.
        /// The track being left is pushed into the history so Back can return to it.
        /// </summary>
        /// <param name="RetString">this the path of track on the system</param>
        /// <returns>the success status of the operation</returns>
        public bool Next(out string RetString)
        {
            RetString = null;

            if (!IsVerified) return false;

            // remember the track we are leaving, but only if it is a valid entry
            if (_CurrentPlaying >= 0 && _CurrentPlaying < MusicPaths.Count)
            {
                PushHistory(MusicPaths[_CurrentPlaying]);
            }

            int next;

            if (IsShuffle)
            {
                next = PickRandomIndex(_CurrentPlaying);
            }
            else
            {
                next = _CurrentPlaying + 1;
                if (next >= MusicPaths.Count) next = 0;   // wrap to the beginning
            }

            if (next < 0 || next >= MusicPaths.Count) return false;

            RetString = MusicPaths[next];
            _CurrentPlaying = next;
            return true;
        }
        /// <summary>
        /// Get the previously played track from the history.
        /// Works correctly with shuffle: returns what actually played, not the neighbouring index.
        /// Falls back to index navigation when the history is empty.
        /// </summary>
        /// <param name="RetString">this the path of track on the system</param>
        /// <returns>the success status of the operation</returns>
        public bool Back(out string RetString)
        {
            RetString = null;

            if (!IsVerified) return false;

            // history mode: pop the last played track
            while (PlayedMusic.Count > 0)
            {
                string previous = PlayedMusic[PlayedMusic.Count - 1];
                PlayedMusic.RemoveAt(PlayedMusic.Count - 1);

                int index = IndexOfPath(previous);

                // skip entries that were removed from the playlist since they played
                if (index < 0) continue;

                _CurrentPlaying = index;
                RetString = MusicPaths[index];
                return true;
            }

            // fallback: no history yet, just step one index back
            int back = _CurrentPlaying - 1;
            if (back < 0) back = MusicPaths.Count - 1;

            RetString = MusicPaths[back];
            _CurrentPlaying = back;
            return true;
        }

        /// <summary>
        /// Get index on the music by folder path
        /// </summary>
        /// <param name="path">path of music on the system</param>
        /// <param name="index">index on the playlist</param>
        /// <returns>the success status of the operation</returns>
        public bool GetIndexMusicByPath(string path, ref int index)
        {
            for (int i = 0; i < MusicPaths.Count; i++)
            {
                if (path == MusicPaths[i])
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Jumps to a specific index, remembering the current track in the history.
        /// </summary>
        public bool SetCurrent(int index)
        {
            if (!IsVerified || index < 0 || index >= MusicPaths.Count) return false;

            if (_CurrentPlaying >= 0 && _CurrentPlaying < MusicPaths.Count && _CurrentPlaying != index)
                PushHistory(MusicPaths[_CurrentPlaying]);

            _CurrentPlaying = index;
            return true;
        }

        /// <summary>Clears the playback history.</summary>
        public void ClearHistory() => PlayedMusic.Clear();

        public void Push(string Path)
        {
            MusicPaths.Add(Path);
            if (!_musicCache.ContainsKey(Path))
            {
                _musicCache[Path] = Music.GetInfo(Path);
            }
        }

        public void Clear()
        {
            MusicPaths.Clear();
            PlayedMusic.Clear();
            _musicCache.Clear();
        }

        /// <summary>
        /// Remove Music by index on the playList
        /// </summary>
        /// <param name="index">index on the playlist</param>
        /// <returns>the success status of the operation</returns>
        public bool RemoveByIndex(int index)
        {
            if (!IsVerified)
            {
                return false;
            }
            if (index < 0 || index >= MusicPaths.Count)
            {
                return false;
            }

            string removedPath = MusicPaths[index];
            MusicPaths.RemoveAt(index);
            _musicCache.Remove(removedPath);

            if (_CurrentPlaying >= MusicPaths.Count)
            {
                _CurrentPlaying = Math.Max(0, MusicPaths.Count - 1);
            }
            return true;
        }

        /// <summary>
        /// Remove last Music on the playList
        /// </summary>
        /// <returns>the success status of the operation</returns>
        public bool Pop()
        {
            return RemoveByIndex(MusicPaths.Count - 1);
        }
        /// <summary>Adds a path to the history and trims it to HistoryLimit.</summary>
        private void PushHistory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            PlayedMusic.Add(path);

            if (HistoryLimit > 0 && PlayedMusic.Count > HistoryLimit)
                PlayedMusic.RemoveAt(0);
        }

        /// <summary>
        /// Returns a random index. With a single track it returns that track.
        /// When ShuffleAvoidRepeat is on, never returns the current index.
        /// </summary>
        private int PickRandomIndex(int currentIndex)
        {
            int count = MusicPaths.Count;
            if (count == 1) return 0;

            if (!ShuffleAvoidRepeat) return _random.Next(count);

            // pick from count-1 slots and shift past the current index
            int pick = _random.Next(count - 1);
            if (pick >= currentIndex) pick++;

            return pick;
        }

        /// <summary>Finds a path ignoring case and slash direction.</summary>
        private int IndexOfPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return -1;

            for (int i = 0; i < MusicPaths.Count; i++)
            {
                if (string.Equals(MusicPaths[i], path, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}