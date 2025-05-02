using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public class FartSchedule
        {
            [JsonProperty("type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public FartType Type { get; set; }
            [JsonProperty("timestamp")]
            [JsonConverter(typeof(UnixDateTimeConverter))]
            public DateTime Timestamp { get; set; }
        }

        public class FartScheduleList
        {
            //Serialize JSON array to list
            [JsonProperty("farts")]
            public List<FartSchedule> FartSchedules { get; set; } = new List<FartSchedule>();
        }

        public static void ScheduledFart(FartSchedule fartSchedule)
        {
            Console.WriteLine($"## [FartUtil] Scheduled fart: {fartSchedule.Type} at {fartSchedule.Timestamp} UTC");
            // Fart now if the we are within 30 seconds of the scheduled time
            if (fartSchedule.Timestamp <= DateTime.UtcNow && fartSchedule.Timestamp >= DateTime.UtcNow.AddSeconds(-30))
            {
                Console.WriteLine($"## [FartUtil] Farting now! Type: {fartSchedule.Type}");
                Fart(fartSchedule.Type);
            }
            else
            {
                // Schedule the fart for later or handle past timestamps
                var delay = (int)(fartSchedule.Timestamp - DateTime.UtcNow).TotalMilliseconds;
                if (delay > 0)
                {
                    Console.WriteLine($"## [FartUtil] Scheduled to fart in {delay / 1000 / 60:F2}");
                    Task.Delay(delay).ContinueWith(_ =>
                    {
                        Fart(fartSchedule.Type);
                    });
                }
                else
                {
                    Console.WriteLine("## [FartUtil] Expired fart :(");
                }
            }
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
