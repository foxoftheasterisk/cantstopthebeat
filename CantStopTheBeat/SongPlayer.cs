using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAudio.Wave;

namespace CantStopTheBeat
{
    class SongPlayer : IWaveProvider
    {

        //reading-type stuff
        //this presumably will need different stuff to handle non-mp3 files
        //but for now, testing
        string[] files;
        int fileNum;
        Mp3FileReader bridge;
        WaveStream inputStream;

        //FFT-type stuff
        double[][] toPassToFFT;
        public bool newFFTData;
        const int fftSize = 8192;

        //output-to-player-type stuff
        const int bufferSize = 16384;  //based on being the lowest power of two above a certain length of time
                                       //still kind of magic number-y
        byte[] buffer;
        IWavePlayer player;
        public WaveFormat WaveFormat
        {
            get
            {
                return waveFormat;
            }
        }
        private WaveFormat waveFormat;



        //??? do i need all this
        /*
        Queue<short[]> dataQueue;
        int posInBuff;
        int read;
        StreamWriter outstream;
        double[][] toPassToFFT;
        public bool newFFTData;
        const int fftSize = 8192;   //preferred sample size for an FFT run
         */

        bool playlistEnded = false;

        //bool ended = false;

        public SongPlayer(string[] filePaths)
        {
            files = filePaths;
            fileNum = 0;
            bridge = new Mp3FileReader(files[fileNum]);  //load up the first file in the list

            inputStream = WaveFormatConversionStream.CreatePcmStream(bridge);  //convert it to PCM

            buffer = new byte[bufferSize];

            player = new DirectSoundOut(100);
            waveFormat = inputStream.WaveFormat;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if(playlistEnded)
            {
                return 0;
            }
            int logged = 0;
            while (logged < count)
            {
                if (posInBuff >= read - 1)
                {
                    read = inputStream.Read(buffer, 0, buffer.Length);
                    posInBuff = 0;
                    if (read == 0)
                    {
                        nextFile();
                        if (playlistEnded)
                            break;
                    }
                }
                int length = Math.Min(count - logged, read - posInBuff);
                Array.Copy(buffer, posInBuff, outBuff, offset + logged, length);
                logged += length;
            }
        }

        public WaveFormat WaveFormat
        {
            get { throw new NotImplementedException(); }
        }

        public void start()
        {
            player.Init(this);
            player.Play();
            player.PlaybackStopped += stopped;
        }

        public void stopped(Object sender, EventArgs e)
        {
            Console.WriteLine("Stopped");  //i think this is just a "let's put this here and see if it ever happens" kind of thing
            //ended = true;
        }

    }
}
