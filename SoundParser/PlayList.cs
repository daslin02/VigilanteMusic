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

        public PlayList()
        {
            MusicPaths = new List<string>();
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
        /// Get Next Music in the play list
        /// </summary>
        /// <param name="RetString">this the path of track on the system</param>
        /// <returns>the success status of the operation</returns>
        public bool Next(out string RetString)
        {
            RetString = null;

            if (!IsVerified) return false;

            int next = _CurrentPlaying + 1;
            if (next >= MusicPaths.Count)
            {
                next = 0;
            }

            RetString = MusicPaths[next];
            _CurrentPlaying = next;
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
        /// Get last Music on the playList
        /// </summary>
        /// <param name="RetString">this the path of track on the system</param>
        /// <returns>the success status of the operation</returns>
        public bool Back(out string RetString)
        {
            RetString = null;

            if (!IsVerified) return false;

            int back = _CurrentPlaying - 1;
            if (back < 0)
            {
                back = MusicPaths.Count - 1;
            }

            RetString = MusicPaths[back];
            _CurrentPlaying = back;
            return true;
        }

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
    }
}