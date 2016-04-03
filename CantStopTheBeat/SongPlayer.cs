using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAudio.Wave;
using Microsoft.Xna.Framework.Graphics;
using CSTBLibrary;
using System.Numerics;

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

        Mp3FileReader nextBridge;
        WaveStream nextStream;
        bool inNextFile = false;
        bool noNextFile = false;
        bool inRead;

        //FFT-type stuff
        CircularBuffer<Complex>[] FFTDataCollector;
        const int fftDataSize = 8192;   //preferred sample size for an FFT run
        double[][] FFTResults;

        //TEMP
        double highestFFTMagnitude;

        //output-to-player-type stuff
        const int bufferSize = 16384;  //based on being the lowest power of two above a certain length of time
                                       //still kind of magic number-y
        IWavePlayer player;
        public WaveFormat WaveFormat
        {
            get
            {
                return waveFormat;
            }
        }
        private WaveFormat waveFormat;

        //tracking-type stuff
        byte[] buffer;
        int posInBuff;
        int read;

        //other
        InterpretedTag tag;

        //bool playlistEnded = false;

        //bool ended = false;

        public SongPlayer(string[] filePaths)
        {
            files = filePaths;
            fileNum = 0;
            bridge = new Mp3FileReader(files[fileNum]);  //load up the first file in the list
            tag = new InterpretedTag(bridge.Id3v2Tag); //read its tag

            inputStream = WaveFormatConversionStream.CreatePcmStream(bridge);  //convert it to PCM

            buffer = new byte[bufferSize];

            player = new DirectSoundOut(100);
            waveFormat = inputStream.WaveFormat;

            FFTDataCollector = new CircularBuffer<Complex>[waveFormat.Channels];
            for (int i = 0; i < FFTDataCollector.Length; i++)
            {
                FFTDataCollector[i] = new CircularBuffer<Complex>(fftDataSize);
            }

            if (fileNum + 1 < files.Length)
            {
                nextBridge = new Mp3FileReader(files[fileNum + 1]);

                nextStream = WaveFormatConversionStream.CreatePcmStream(nextBridge);
            }
        }

        public int Read(byte[] outBuff, int offset, int count)
        {
            if(inNextFile && noNextFile)
            {
                return 0;
            }
            inRead = true;
            int logged = 0;
            while (logged < count)
            {
                if (!inNextFile)
                {
                    if (posInBuff >= read - 1)
                    {
                        read = inputStream.Read(buffer, 0, buffer.Length);
                        posInBuff = 0;
                        if (read == 0)
                        {
                            if (noNextFile)
                                break;
                            waveFormat = nextStream.WaveFormat;
                            if (FFTDataCollector.Length != waveFormat.Channels)
                            {
                                //FFT format needs to keep up with stream format
                                FFTDataCollector = new CircularBuffer<Complex>[waveFormat.Channels];
                                for (int i = 0; i < FFTDataCollector.Length; i++)
                                {
                                    FFTDataCollector[i] = new CircularBuffer<Complex>(fftDataSize);
                                }
                            }
                            inNextFile = true;
                            continue;
                        }
                    }
                }
                else
                {
                    if (posInBuff >= read - 1)
                    {
                        read = nextStream.Read(buffer, 0, buffer.Length);
                        posInBuff = 0;
                        if (read == 0)
                        {
                            nextFile();
                            inNextFile = true;
                            if (noNextFile)
                                break;
                            waveFormat = nextStream.WaveFormat;
                            if (FFTDataCollector.Length != waveFormat.Channels)
                            {
                                //FFT format needs to keep up with stream format
                                FFTDataCollector = new CircularBuffer<Complex>[waveFormat.Channels];
                                for (int i = 0; i < FFTDataCollector.Length; i++)
                                {
                                    FFTDataCollector[i] = new CircularBuffer<Complex>(fftDataSize);
                                }
                            }
                            continue;
                        }
                    }
                }
                int length = Math.Min(count - logged, read - posInBuff);
                Array.Copy(buffer, posInBuff, outBuff, offset + logged, length);
                logged += length;

                //this part does not play audio,
                //but instead pipes the same data to the FFT
                //has to be in here because the stream can't be read without advancing
                //(alternatively, we could read to the FFT first and then pass from there to this, but this seems more sensible.)
                for (int end = posInBuff + length; posInBuff < end; posInBuff += 2 * WaveFormat.Channels)
                {
                    for (int i = 0; i < WaveFormat.Channels; i++)
                    {
                        Complex c = new Complex(buffer[posInBuff + i * 2] + (short)(buffer[posInBuff + (i * 2) + 1] << 8), 0);
                        //compose the two bytes together, and add an imaginary (of 0) to make a complex number
                        //because fouriers take complex numbers

                        FFTDataCollector[i].Add(c);
                    }
                }

            }
            inRead = false;
            return logged;
        }

        public void Update()
        {
            if (inNextFile && !noNextFile && !inRead)
            {
                nextFile();
            }

            if(FFTDataCollector[0].IsFull)
            {
                //then all should be full, as they're supposed to have the same amount of data.

                highestFFTMagnitude = 0;
                FFTResults = new double[WaveFormat.Channels][];

                for(int i = 0; i < WaveFormat.Channels; i++)
                {
                    if (!FFTDataCollector[i].IsFull)
                        throw new InvalidOperationException("Some FFTs are full but not all????");

                    FFTResults[i] = new double[fftDataSize];

                    Complex[] FFTData = FFTDataCollector[i].getArray();
                    MathNet.Numerics.IntegralTransforms.Fourier.Forward(FFTData);

                    for(int j=0; j < fftDataSize; j++)
                    {
                        FFTResults[i][j] = FFTData[j].Magnitude;
                        if (FFTResults[i][j] > highestFFTMagnitude)
                            highestFFTMagnitude = FFTResults[i][j];
                    }
                }
            }


        }

        private void nextFile()
        {
            if (noNextFile)
                throw new InvalidOperationException("Tried to advance while no next file!");

            //newFFTData = false;
            bridge.Close();
            bridge.Dispose();
            inputStream.Close();
            inputStream.Dispose();

            bridge = nextBridge;
            inputStream = nextStream;
            tag = new InterpretedTag(bridge.Id3v2Tag);

            inNextFile = false;

            fileNum++;
            if (fileNum + 1 >= files.Length)
            {
                noNextFile = true;
                return;
            }
            
            nextBridge = new Mp3FileReader(files[fileNum + 1]);
            nextStream = new WaveFormatConversionStream(WaveFormat, nextBridge);
        }

        public void start()
        {
            player.Init(this);
            player.Play();
            player.PlaybackStopped += stopped;
        }

        public void stopped(Object sender, EventArgs e)
        {
            //this does in fact happen when the playlist is concluded.
            //ended = true;
        }

        public void draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            XingHeader head = bridge.XingHeader;
            if(head != null)
                spriteBatch.DrawString(font, head.ToString(), new Microsoft.Xna.Framework.Vector2(5, 5), Microsoft.Xna.Framework.Color.Black);
            else
                spriteBatch.DrawString(font, "No Xing header!", new Microsoft.Xna.Framework.Vector2(5, 5), Microsoft.Xna.Framework.Color.Black);

            if (tag.title != null)
                spriteBatch.DrawString(font, tag.title, new Microsoft.Xna.Framework.Vector2(5, 25), Microsoft.Xna.Framework.Color.Black);
            else
                spriteBatch.DrawString(font, "No title tag!", new Microsoft.Xna.Framework.Vector2(5, 25), Microsoft.Xna.Framework.Color.Black);

            if (tag.artist != null)
                spriteBatch.DrawString(font, tag.artist, new Microsoft.Xna.Framework.Vector2(5, 45), Microsoft.Xna.Framework.Color.Black);
            else
                spriteBatch.DrawString(font, "No artist tag!", new Microsoft.Xna.Framework.Vector2(5, 45), Microsoft.Xna.Framework.Color.Black);

            if (tag.album != null)
                spriteBatch.DrawString(font, tag.album, new Microsoft.Xna.Framework.Vector2(5, 65), Microsoft.Xna.Framework.Color.Black);
            else
                spriteBatch.DrawString(font, "No album tag!", new Microsoft.Xna.Framework.Vector2(5, 65), Microsoft.Xna.Framework.Color.Black);

            spriteBatch.DrawString(font, highestFFTMagnitude.ToString(), new Microsoft.Xna.Framework.Vector2(5, 85), Microsoft.Xna.Framework.Color.Black);


        }

    }
}
