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
    public delegate void FramesAvailableEventHandler(object sender, byte[] data);
    

    public class ImmediateStreamer : IPlayerImplementer
    {
        #region Private
        private Timer _endFrameTimer;
        private List<KeyValuePair<TimeSpan, Mp3Frame>> _frames;

        // It's dramatically reduce frame search time, but create some bugs. I was fixed them but not make this more elegant
        private int _nextPartBeginIndex = -1;

        private void TimerCallback(object o)
        {
            if (!Monitor.TryEnter(this))
                Console.WriteLine("timer tacts colision");
            else
                Monitor.Exit(this);

            lock (this)
            {
                if (!NowPlay)
                    return;

                if (_frames == null || _frames.Count == 0)
                    return;

                // Collect all frames for a next second
                TimeSpan begin = _component.SongProgress;
                
                // This is the end
                if (begin >= _frames.Last().Key)
                {
                    _endFrameTimer.Change(int.MaxValue, int.MaxValue);
                    _endFrameTimer.Dispose();
                    _endFrameTimer = null;
                    _frames = null;
                    _nextPartBeginIndex = -1;

                    // SongEnded?.Invoke(this, EventArgs.Empty); 
                    // will be called or already was called by decoratorHandler

                    return;
                }

                if (begin < TimeSpan.FromSeconds(1))
                    begin = TimeSpan.Zero;
                
                
                

                TimeSpan end = begin.Add(TimeSpan.FromSeconds(1));
                if (end > _frames.Last().Key)
                    end = _frames.Last().Key;

                List<Mp3Frame> currentInterval = new List<Mp3Frame>(50); // 50 avarage value of frames per second based on one song

                if (_nextPartBeginIndex != -1 &&
                    _nextPartBeginIndex < _frames.Count &&
                    TimeIsMatch(_frames[_nextPartBeginIndex].Key, begin, 50))
                {

                    // Song was ended. Timer mast be renuwed
                    if (_nextPartBeginIndex >= _frames.Count)
                    {
                        _endFrameTimer.Change(int.MaxValue, int.MaxValue);
                        _endFrameTimer.Dispose();
                        _endFrameTimer = null;
                        _frames = null;
                        _nextPartBeginIndex = -1;

                        // SongEnded?.Invoke(this, EventArgs.Empty); 
                        // will be called or already was called by decoratorHandler

                        return;
                    }

                    int lastMatchIndex = _frames.FindIndex(_nextPartBeginIndex, t => t.Key >= end);
                    lastMatchIndex = lastMatchIndex == -1 ? _frames.Count - 1 : lastMatchIndex;


                    for (int i = _nextPartBeginIndex; i <= lastMatchIndex; i++)
                        currentInterval.Add(_frames[i].Value);

                    _nextPartBeginIndex = lastMatchIndex + 1;
                }
                else
                {
                    int firstMatch = _frames.FindIndex(t => t.Key >= begin);
                    int lastMatch = _frames.FindIndex(firstMatch, t => t.Key >= end);

                    // <= for correct sending last frame ( not good idea, should refactor )
                    for (int i = firstMatch; i <= lastMatch; i++)
                        currentInterval.Add(_frames[i].Value);

                    _nextPartBeginIndex = lastMatch + 1;
                }

                int packetLen = currentInterval.Sum(t => t.RawData.Length);
                byte[] data = new byte[packetLen];

                for (int i = 0, dest = 0; i < currentInterval.Count; i++)
                {
                    Array.Copy(currentInterval[i].RawData, 0, data, dest, currentInterval[i].RawData.Length);
                    dest += currentInterval[i].RawData.Length;
                }
                //Console.Write("Send {0} bytes ",data.Length);
                PushData?.Invoke(this, data);
                //Console.WriteLine("OK");
            }
        }

        private bool TimeIsMatch(TimeSpan a, TimeSpan b, int delta)
        {
            return Math.Abs(a.TotalMilliseconds - b.TotalMilliseconds) < delta;
        }

        private void EndHandler(object sender, EventArgs arg)
        {
            _endFrameTimer?.Change(int.MaxValue, int.MaxValue);
            _endFrameTimer?.Dispose();
            _endFrameTimer = null;

            _frames = null;
            _nextPartBeginIndex = -1;

            SongEnded?.Invoke(this, null);
        }
        #endregion

        private IPlayerImplementer _component;

        public ImmediateStreamer(IPlayerImplementer performer)
        {
            _component = performer;
            _component.SongEnded += EndHandler;
        }
        public ImmediateStreamer() : this(new MutePlayer())
        {

        }

        public bool NowPlay
        {
            get
            {
                return _component.NowPlay;
            }
        }

        public Song Song
        {
            get
            {
                return _component.Song;
            }

            set
            {
                lock (this)
                {
                    _endFrameTimer?.Change(int.MaxValue, int.MaxValue);
                    _endFrameTimer?.Dispose();
                    _endFrameTimer = null;
                    _frames = null;
                    _nextPartBeginIndex = -1;

                    _component.Song = value;
                }
            }
        }

        public TimeSpan SongDuration
        {
            get
            {
                return _component.SongDuration;
            }
        }

        public TimeSpan SongProgress
        {
            get
            {
                return _component.SongProgress;
            }

            set
            {
                lock(this)
                    _component.SongProgress = value;
            }
        }

        public float Volume
        {
            get
            {
                return _component.Volume;
            }

            set
            {
                _component.Volume = value;
            }
        }

        public event EventHandler SongEnded;
        public event FramesAvailableEventHandler PushData;

        public void Dispose()
        {
            _endFrameTimer?.Change(int.MaxValue, int.MaxValue);
            _endFrameTimer?.Dispose();
            _endFrameTimer = null;

            _frames = null;
            _nextPartBeginIndex = -1;

            _component?.Dispose();
            _component = null;
        }

        public int GetCurrentOutputDeviceId()
        {
            return _component.GetCurrentOutputDeviceId();
        }

        public string GetOutputDevicesReport()
        {
            return _component.GetOutputDevicesReport() + "\n+ Immediate stream";
        }

        public void Pause()
        {
            _component.Pause();
        }

        public void Play()
        {
            _component.Play();

            if (_frames == null)
                LoadFrames();

            if (_endFrameTimer == null)
                _endFrameTimer = new Timer(TimerCallback, null, 50, 1000);
        }

        private void LoadFrames()
        {
            _frames = new List<KeyValuePair<TimeSpan, Mp3Frame>>();
            TimeSpan frameBegin = TimeSpan.Zero;
            using (FileStream fs = File.OpenRead(_component.Song.Path))
                while (true)
                {
                    var f = Mp3Frame.LoadFromStream(fs);
                    if (f == null)
                        break;
                    _frames.Add(new KeyValuePair<TimeSpan, Mp3Frame>(frameBegin, f));
                    frameBegin += TimeSpan.FromMilliseconds(f.SampleCount * (1.0 / f.SampleRate) * 1000);
                }
        }

        public void Reset()
        {
            _component.Reset();
            _nextPartBeginIndex = -1;
        }

        public void SetOutputDevice(int id)
        {
            _component.SetOutputDevice(id);
        }

        public void Stop()
        {
            _component.Stop();
            _nextPartBeginIndex = -1;

        }
    }
}
