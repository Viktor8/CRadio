using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CPlayer;
using CPlayer.Logic;
using NAudio;
using NAudio.Wave;
using System.Threading;
using System.IO;


namespace CPlayerStreamer
{
    /// <summary>
    /// For decorating
    /// </summary>
    public class MutePlayer : IPlayerImplementer
    {     
       private Song _song;

 
        private DateTime _playBegin;
        private TimeSpan _progress;
        private Timer _endTimer;
        private bool _nowPlay;

      
        public bool NowPlay
        {
            get
            {
                return _nowPlay;
            }
            private set
            {
                if (_nowPlay == value)
                    return;

                if (value == true)
                {
                    _playBegin = DateTime.Now;
                    _nowPlay = value;
                }
                else
                {
                    _progress += DateTime.Now - _playBegin;
                    _nowPlay = value;
                }
            }
        }

        private TimeSpan duration = TimeSpan.Zero;


        public event EventHandler SongEnded;

        public Song Song
        {
            get
            {
                return _song;
            }

            set
            {
                _song = value;
                duration = TimeSpan.Zero;
            }
        }

        public TimeSpan SongDuration
        {
            get
            {
                if (duration == TimeSpan.Zero)
                    using (var reader = new Mp3FileReader(Song.Path))
                        duration = reader.TotalTime;

                return duration;
            }
        }

        public TimeSpan SongProgress
        {
            get
            {
                if (!_nowPlay)
                    return _progress;
                else
                    return _progress + (DateTime.Now - _playBegin);
            }
            set
            {
                if (TimeSpan.Zero <= value && value <= SongDuration)
                {
                    _progress = value;
                    _playBegin = DateTime.Now;
                    return;
                }
                else
                    throw new ArgumentOutOfRangeException();
            }
        }


        public float Volume
        {
            get
            {
                return 0.0f;
            }

            set
            {
            }
        }

        public void Play()
        {
            if (NowPlay)
                return;

            

            if (_endTimer == null)
            {
                TimerCallback callback = (o) =>
                {
                    _endTimer?.Change(int.MaxValue, int.MaxValue);
                    _endTimer?.Dispose();
                    _endTimer = null;

                    SongEnded?.Invoke(this, EventArgs.Empty);
                };

                _endTimer = new Timer(callback, null, (int)SongDuration.TotalMilliseconds, int.MaxValue);
            }

            int toEnd = (int)(SongDuration - SongProgress).TotalMilliseconds;

            _endTimer.Change(toEnd, int.MaxValue);

            NowPlay = true;

        }
        public void Pause()
        {
            NowPlay = false;
            _endTimer.Change(int.MaxValue, int.MaxValue);
        }
        public void Reset()
        {
            Stop();
        }
        public void Stop()
        {
            NowPlay = false;
            SongProgress = TimeSpan.Zero;
            _endTimer?.Change(int.MaxValue, int.MaxValue);
            _endTimer?.Dispose();
            _endTimer = null;

        }


        public string GetOutputDevicesReport()
        {
            string result = "none";
            return result;
        }
        public void SetOutputDevice(int deviceId = -1)
        {
        }
        public int GetCurrentOutputDeviceId()
        {
            return -1;
        }

        public void Dispose()
        {
            NowPlay = false;
            SongProgress = TimeSpan.Zero;
            _endTimer?.Change(int.MaxValue, int.MaxValue);
            _endTimer?.Dispose();
            _endTimer = null;
            _song = null;
        }
  
    }
}
