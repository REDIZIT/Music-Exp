using System;
using System.IO;

public class MP3EFile
{
    public MP3EFileHeader header;
    public MP3ESegmentScheme[] scheme;
    public MP3ESegment[] segments;

    [NonSerialized]
    public byte[] pcm;

    public static MP3EFile InitFromStream(Stream stream)
    {
        // MP3EFile file = new()
        // {
        //     header = MP3EFileHeader.FromStream(stream),
        //     scheme = BinarySerializer.Deserialize<MP3ESegmentScheme[]>(stream),
        //     segments = BinarySerializer.Deserialize<MP3ESegment[]>(stream),
        // };
        // return file;

        return BinarySerializer.Deserialize<MP3EFile>(stream);
    }
}

public class MP3EFileHeader
{
    public int sampleRate;
    public int totalSampleCount;
    public byte channelMode;
    public int frameLength;
    public int bitRate;

    public int segmentCount;

    public byte[] ToBytes()
    {
        return BinarySerializer.Serialize(this);
    }

    public static MP3EFileHeader FromStream(Stream stream)
    {
        return BinarySerializer.Deserialize<MP3EFileHeader>(stream);
    }
}

public class MP3ESegmentScheme
{
    public ushort sizeInBytes;
    public ushort deltaTimeInMilliseconds;
    public ushort pcmSizeInBytes;

    [NonSerialized]
    public bool isLoaded;

    [NonSerialized]
    public int pcmAbsBegin;
}

public class MP3ESegment
{
    public byte[] mp3Frame;

    public byte[] ToBytes()
    {
        return BinarySerializer.Serialize(this);
    }

    public static MP3ESegment FromStream(Stream stream)
    {
        return BinarySerializer.Deserialize<MP3ESegment>(stream);
    }
}