using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using zort.Properties;

namespace zort
{
    public class Zort
    {
        public enum FartType
        {
            Classic,
            Explosive,
            DryLong,
            Wet,
            WetLong,
            MiniExplosion,
            Explosion,
            Puke
        }

        public static void Fart(FartType type)
        {
            // Get fart audio from resources.  
            UnmanagedMemoryStream fartAudio;
            switch (type)
            {
                case FartType.Classic:
                    fartAudio = Resources.classic;
                    break;
                case FartType.Explosive:
                    fartAudio = Resources.explosive;
                    break;
                case FartType.DryLong:
                    fartAudio = Resources.longdry;
                    break;
                case FartType.Wet:
                    fartAudio = Resources.wet;
                    break;
                case FartType.WetLong:
                    fartAudio = Resources.longwet;
                    break;
                case FartType.MiniExplosion:
                    fartAudio = Resources.miniexplosion;
                    break;
                case FartType.Explosion:
                    fartAudio = Resources.explosive;
                    break;
                case FartType.Puke:
                    fartAudio = Resources.puke;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            //Play the fart sound
            using (var memoryStream = new MemoryStream())
            {
                fartAudio.CopyTo(memoryStream);
                byte[] audioData = memoryStream.ToArray();
                // Play the audio data
                using (var soundPlayer = new System.Media.SoundPlayer(new MemoryStream(audioData)))
                {
                    soundPlayer.PlaySync();
                }
            }
        }
    }
}
