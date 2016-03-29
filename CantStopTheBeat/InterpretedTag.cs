using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CantStopTheBeat
{
    class InterpretedTag
    {

        [Flags]
        enum Id3v2Flags
        {
            FooterPresent = 0x10,
            Experimental = 0x20,
            ExtendedHeader = 0x40,
            Unsynchronization = 0x80
        }

        const int VERSION_BYTE = 3;
        const int ID3_FLAGS_BYTE = 5;
        const int FIRST_NONHEADER_BYTE = 10; //technically, this is where the extended header will start, if there is one
        //but, it's the first byte that isn't fully proscribed what it is
        const int SYNCHSAFE_INT_BYTE_INCREASE = 0x80; //since only seven bits are used, it's 0x80 rather than 0x100

        enum TextEncodings
        {
            ISO88591 = 0x00,
            UTF16 = 0x01,
            UTF16BE = 0x02,
            UTF8 = 0x03
        }

        //fields that have been read
        //these should be public get, only
        //but BLEH
        public string title;
        public string artist;
        public string album;
        public int trackNum;
        public int discNum;


        public InterpretedTag(NAudio.Wave.Id3v2Tag id3tag)
        {
            byte[] tagContent = id3tag.RawData;

            if (tagContent[VERSION_BYTE] > 4)
                throw new NotSupportedException("Id3 versions 2.5 or greater are not supported.");

            Id3v2Flags flags = (Id3v2Flags)tagContent[ID3_FLAGS_BYTE];

            int currentByte = FIRST_NONHEADER_BYTE;
            if((flags & Id3v2Flags.ExtendedHeader) == Id3v2Flags.ExtendedHeader)
            {
                //The extended header contains no data we care about, so this is just to skip it.
                int extHeaderSize = tagContent[currentByte] * SYNCHSAFE_INT_BYTE_INCREASE * SYNCHSAFE_INT_BYTE_INCREASE * SYNCHSAFE_INT_BYTE_INCREASE 
                    + tagContent[currentByte + 1] * SYNCHSAFE_INT_BYTE_INCREASE * SYNCHSAFE_INT_BYTE_INCREASE 
                    + tagContent[currentByte + 2] * SYNCHSAFE_INT_BYTE_INCREASE 
                    + tagContent[currentByte + 3];
                currentByte += extHeaderSize;
            }

            string frameID;
            frameID = ((char)tagContent[currentByte]).ToString()
                + ((char)tagContent[currentByte + 1]).ToString()
                + ((char)tagContent[currentByte + 2]).ToString()
                + ((char)tagContent[currentByte + 3]).ToString();
            currentByte += 4;

            while (frameID[0] != '\0')
            {
                int frameSize = tagContent[currentByte] * SYNCHSAFE_INT_BYTE_INCREASE * SYNCHSAFE_INT_BYTE_INCREASE * SYNCHSAFE_INT_BYTE_INCREASE
                    + tagContent[currentByte + 1] * SYNCHSAFE_INT_BYTE_INCREASE * SYNCHSAFE_INT_BYTE_INCREASE
                    + tagContent[currentByte + 2] * SYNCHSAFE_INT_BYTE_INCREASE
                    + tagContent[currentByte + 3];
                currentByte += 4;

                //skipping frame flags for now
                currentByte += 2;

                if(frameID[0] == 'T')
                {
                    TextEncodings textEncoding = (TextEncodings)tagContent[currentByte];
                    currentByte++;
                    byte[] str = new byte[frameSize];
                    Array.Copy(tagContent, currentByte, str, 0, frameSize - 1);

                    string tag;

                    switch(textEncoding)
                    {
                        case TextEncodings.ISO88591:
                            tag = System.Text.Encoding.ASCII.GetString(str);
                            break;
                        case TextEncodings.UTF8:
                            tag = System.Text.Encoding.UTF8.GetString(str);
                            break;
                        default:
                            throw new NotSupportedException("Non-supported text encoding.");
                    }

                    tag = tag.TrimEnd('\0');

                    switch(frameID)
                    {
                        case "TIT2":
                            title = tag;
                            break;
                        case "TALB":
                            album = tag;
                            break;
                        case "TRCK":
                        case "TPOS":
                            //TODO
                            break;
                        case "TPE1":
                            artist = tag;
                            break;
                        default:
                            //nothing
                            break;
                    }

                    currentByte--;
                }

                currentByte += frameSize;


                frameID = ((char)tagContent[currentByte]).ToString()
                + ((char)tagContent[currentByte + 1]).ToString()
                + ((char)tagContent[currentByte + 2]).ToString()
                + ((char)tagContent[currentByte + 3]).ToString();
                currentByte += 4;
                //dontcha hate loop-and-a-half
            }

            return;
        }

    }
}
