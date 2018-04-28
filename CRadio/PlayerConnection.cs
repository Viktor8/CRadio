using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;

using CPlayer;
using CPlayer.Control;
using CPlayer.LoadSave;
using CPlayer.Logic;

using System.Threading;

namespace CRadio
{

    public static class PlayerConnection
    {
        private static byte[] ID3V2_Header;
        public static EventWaitHandle DataRecived
        {
            get;
            private set;
        }
        public static EventWaitHandle TextRecieved
        {
            get;
            private set;
        }
        public static List<KeyValuePair<DateTime, byte[]>> LastSeconds
        {

            get
            {
                return _buffer.LastSeconds;
            }
        }


        public static byte[] LastDataPacket
        {
            get;
            private set;
        }
        public static string LastOutput
        {
            get;
            private set;
        }
        public static byte[] Header
        {
            get { return ID3V2_Header; }
        }

        public static IReadOnlyList<string> Inputs
        {
            get
            {
                return _inputs;
            }
        }
        public static bool Initialized
        {
            get
            {
                return initialized;
            }
        }


        private static List<string> _inputs = new List<string>();
        private static AsyncControl _control;

        public static StringBuilder _sb = new StringBuilder();
        private static ReceiveBuffer _buffer;

        static PlayerConnection()
        {
            DataRecived = new EventWaitHandle(false, EventResetMode.ManualReset);
            TextRecieved = new EventWaitHandle(false, EventResetMode.ManualReset);

        }

        static volatile bool initialized;
        public static void Initialize()
        {
            lock (typeof(PlayerConnection))
            {
                if (initialized)
                    return;

                string headerFile = System.Web.Configuration.WebConfigurationManager.AppSettings["HeaderFile"];

                ID3V2_Header = System.IO.File.ReadAllBytes(headerFile);


                Initializer.Initialize(Program.INTEGRATION_INITIALIZATION);

                _control = Initializer.Control;
                _control.PushCommand("ldasm CPlayerStreamer.dll");
                _control.PushCommand("setp 2");
                _control.PushCommand("starthkm");
                _control.WaitQueueExecution();

                CPlayerStreamer.ImmediateStreamer streamer = Player.GetInstance().GetImplementer() as CPlayerStreamer.ImmediateStreamer;
                streamer.PushData += (o, d) =>
{
    LastDataPacket = d;
    DataRecived.Set();
    DataRecived.Reset();
};

                CPlayer.Output.Display.Print += (sender, text, color, newLine) =>
                {
                    if (newLine)
                        _sb.AppendLine(text);
                    else
                        _sb.Append(text);



                    LastOutput = text;
                    TextRecieved.Set();
                    TextRecieved.Reset();
                };
                _buffer = new ReceiveBuffer();
                initialized = true;
            }
        }




        public static void Play()
        {
            _control.PushCommand("play");
        }

        public static void Pause()
        {
            _control.PushCommand("pause");
        }

        public static void PushCommand(string cmd)
        {
            _inputs.Add(cmd);
            _control.PushCommand(cmd);
        }


    }
}