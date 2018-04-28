using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Web.Configuration;
using System.Web.Http;
using System.Threading;

namespace CRadio.Controllers
{
    public class MediaController : ApiController
    {
        public const string FILE_OUT = @"C:\Users\Viktor\Desktop\stream.mp3";

        #region Fields

        // This will be used in copying input stream to output stream.
        public const int ReadStreamBufferSize = 1024 * 23;
        // We have a read-only dictionary for mapping file extensions and MIME names. 
        public static readonly IReadOnlyDictionary<string, string> MimeNames;
        // We will discuss this later.
        public static readonly IReadOnlyCollection<char> InvalidFileNameChars;
        // Where are your videos located at? Change the value to any folder you want.
        public static readonly string InitialDirectory;

        #endregion

        #region Constructors

        static MediaController()
        {
            var mimeNames = new Dictionary<string, string>();

            mimeNames.Add(".mp3", "audio/mpeg");    // List all supported media types; 
            mimeNames.Add(".mp4", "video/mp4");
            mimeNames.Add(".ogg", "application/ogg");
            mimeNames.Add(".ogv", "video/ogg");
            mimeNames.Add(".oga", "audio/ogg");
            mimeNames.Add(".wav", "audio/x-wav");
            mimeNames.Add(".webm", "video/webm");

            MimeNames = new ReadOnlyDictionary<string, string>(mimeNames);

            InvalidFileNameChars = Array.AsReadOnly(Path.GetInvalidFileNameChars());
            InitialDirectory = WebConfigurationManager.AppSettings["InitialDirectory"];
        }

        #endregion

        #region Actions

        [HttpGet]
        public HttpResponseMessage Play(string f)
        {
            bool requestToStream = f.Contains("LiveStream");

            Debug.WriteLine("Media.Play: " + f + ", thread: " + Thread.CurrentThread.ManagedThreadId);
            // This can prevent some unnecessary accesses. 
            // These kind of file names won't be existing at all. 
            if (string.IsNullOrWhiteSpace(f) || AnyInvalidFileNameChars(f))
                throw new HttpResponseException(HttpStatusCode.NotFound);

            FileInfo fileInfo = new FileInfo(Path.Combine(InitialDirectory, f));

            if (!requestToStream)
                if (!fileInfo.Exists)
                    throw new HttpResponseException(HttpStatusCode.NotFound);

            long totalLength = 1024 * 1024 * 50;
            if (!requestToStream)
                totalLength = fileInfo.Length;

            RangeHeaderValue rangeHeader = base.Request.Headers.Range;
            HttpResponseMessage response = new HttpResponseMessage();

            response.Headers.AcceptRanges.Add("bytes");

            // The request will be treated as normal request if there is no Range header.
            if (rangeHeader == null || !rangeHeader.Ranges.Any())
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new PushStreamContent((outputStream, httpContent, transpContext)
                =>
                {

                    using (outputStream) // Copy the file to output stream straightforward. 
                    using (Stream inputStream = fileInfo.OpenRead())
                    {
                        Debug.Write("Push callback. No range header\n");
                        try
                        {
                            inputStream.CopyTo(outputStream, ReadStreamBufferSize);
                        }
                        catch (Exception error)
                        {
                            Debug.WriteLine(error);
                        }
                    }
                }, GetMimeNameFromExt(fileInfo.Extension));

                response.Content.Headers.ContentLength = totalLength;
                return response;
            }

            long start = 0, end = 0;

            // 1. If the unit is not 'bytes'.
            // 2. If there are multiple ranges in header value.
            // 3. If start or end position is greater than file length.
            if (rangeHeader.Unit != "bytes" || rangeHeader.Ranges.Count > 1 ||
                !TryReadRangeItem(rangeHeader.Ranges.First(), totalLength, out start, out end))
            {
                response.StatusCode = HttpStatusCode.RequestedRangeNotSatisfiable;
                response.Content = new StreamContent(Stream.Null);  // No content for this status.
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(totalLength);
                response.Content.Headers.ContentType = GetMimeNameFromExt(fileInfo.Extension);

                return response;
            }

            var contentRange = new ContentRangeHeaderValue(start, end, long.MaxValue);

            // We are now ready to produce partial content.
            response.StatusCode = HttpStatusCode.PartialContent;
            response.Content = new PushStreamContent((outputStream, httpContent, transpContext)
            =>
            {
                if (requestToStream)
                {
                    CreatePartialContent(outputStream, start);
                    return;
                }
                using (outputStream) // Copy the file to output stream in indicated range.
                using (Stream inputStream = fileInfo.OpenRead())
                {
                    CreatePartialContentTest(inputStream, outputStream, start, end);
                }

            }, GetMimeNameFromExt(fileInfo.Extension));

            response.Content.Headers.ContentLength = end - start + 1;
            response.Content.Headers.ContentRange = contentRange;

            return response;
        }

        #endregion

        #region Others

        private static bool AnyInvalidFileNameChars(string fileName)
        {
            return InvalidFileNameChars.Intersect(fileName).Any();
        }

        private static MediaTypeHeaderValue GetMimeNameFromExt(string ext)
        {
            string value;

            if (MimeNames.TryGetValue(ext.ToLowerInvariant(), out value))
                return new MediaTypeHeaderValue(value);
            else
                return new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
        }

        private static bool TryReadRangeItem(RangeItemHeaderValue range, long contentLength,
            out long start, out long end)
        {
            if (range.From != null)
            {
                start = range.From.Value;
                if (range.To != null)
                    end = range.To.Value;
                else
                    end = contentLength - 1;
            }
            else
            {
                end = contentLength - 1;
                if (range.To != null)
                    start = contentLength - range.To.Value;
                else
                    start = 0;
            }
            return (start < contentLength && end < contentLength);
        }

        static FileStream fs;
        static AutoResetEvent are = new AutoResetEvent(false);
        static byte[] data;

        /// <summary>
        /// Operatin is syncrous so we can associate client with thread but shood to find way to do it though http context or smth same
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="outputStream"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        private static void CreatePartialContent(Stream outputStream, long position)
        {
            PlayerConnection.Initialize();
            PlayerConnection.Play();


            bool isFirstPacket = position == 0;

            DateTime lastSend = DateTime.MinValue;

            while (PlayerConnection.LastSeconds.Count < 3)
                Thread.Sleep(1000);


            while (true)
            {
                try
                {
                    if (isFirstPacket)
                    {
                        outputStream.Write(PlayerConnection.Header, 0, PlayerConnection.Header.Length);
                        isFirstPacket = false;
                    }
                    Thread.Sleep(950);

                    int nextPacketIndex = PlayerConnection.LastSeconds.FindIndex(t => t.Key > lastSend);

                    // we was created a spare time for the client
                    if (nextPacketIndex == -1)
                        continue;

        

                    outputStream.Write(PlayerConnection.LastSeconds[nextPacketIndex].Value,
                                        0,
                                        PlayerConnection.LastSeconds[nextPacketIndex].Value.Length);
                    lastSend = PlayerConnection.LastSeconds[nextPacketIndex].Key;

                    //Debug.WriteLine("Contetn pushed: " + DateTime.Now.ToString("mm:ss.FFF") + ", thread: " + Thread.CurrentThread.ManagedThreadId);
                }
                catch (System.Web.HttpException e)
                {
                    Debug.WriteLine(e.ToString());
                    fs?.Close();
                    break;
                }
                catch (Exception e)
                {
                    Debug.Write(e);

                    fs?.Close();
                    break;
                }

            }
        }

        private static void sendToFile(byte[] value)
        {
            data = value;
            are.Set();
        }

        private static void CreatePartialContentTest(Stream inputStream, Stream outputStream,
            long start, long end)
        {
            int count = 0;
            long remainingBytes = end - start + 1;
            long position = start;
            byte[] buffer = new byte[ReadStreamBufferSize];
            //inputStream.Read(buffer, 0, (int)remainingBytes);
            //inputStream.Read(buffer, 0, (int)remainingBytes);
            //inputStream.Position = start;
            Random rand = new Random();
            int iteration = 0;
            do
            {
                try
                {

                    if (remainingBytes > ReadStreamBufferSize)
                        count = inputStream.Read(buffer, 0, ReadStreamBufferSize);
                    else
                        count = inputStream.Read(buffer, 0, (int)remainingBytes);

                    Thread.Sleep(500);

                    outputStream.Write(buffer, 0, count);
                    Debug.WriteLine("Contetn pushed: " + DateTime.Now.ToString("mm:ss.FFF") + ", thread: " + Thread.CurrentThread.ManagedThreadId);
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error);
                    break;
                }
                position = inputStream.Position;
                remainingBytes = end - position + 1;
            } while (position <= end);
        }

        #endregion
    }
}