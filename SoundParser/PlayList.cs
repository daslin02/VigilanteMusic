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
        private int _CurrentPlaying;

        /// <summary>
        /// Getting Music By index on the playList
        /// </summary>
        /// <param name="index">Number PlayList</param>
        /// <param name="RetString">this the path of track on the system</param>
        /// <returns>the success status of the operation</returns>
        public bool GetMusicByIndex(int index , ref string RetString) 
        {   if (!IsVerified)
            {
                return false;
            }
            RetString =  MusicPaths[MyMath.Clamp(0,MusicPaths.Count-1 , index)];
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
            if (next > MusicPaths.Count - 1) return false;

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
        public bool GetIndexMusicByPath(string path , ref int index) 
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
            if (back < 0) return false;

            RetString = MusicPaths[back];
            _CurrentPlaying = back;
            return true;
        }
        /// <summary>
        /// this the variable checking validate MusicPlayList Paths
        /// </summary>
        public bool IsVerified 
        {
            get { return MusicPaths != null && MusicPaths.Count > 0; }
        }

    }
}
