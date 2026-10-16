using System;
using System.Text;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Net
{
    /// <summary>Thrown when a packet is truncated or otherwise malformed.</summary>
    public sealed class NetFormatException : Exception
    {
        public NetFormatException(string message) : base(message)
        {
        }
    }

    /// <summary>Little-endian binary reader matching <see cref="NetWriter"/>. Every read is bounds-checked.</summary>
    public sealed class NetReader
    {
        private readonly byte[] _buffer;
        private readonly int _end;
        private int _position;

        public NetReader(byte[] buffer) : this(buffer, 0, buffer.Length)
        {
        }

        public NetReader(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _buffer = buffer;
            _position = offset;
            _end = offset + count;
        }

        public int Remaining => _end - _position;

        public byte ReadByte()
        {
            Require(1);
            return _buffer[_position++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public byte[] ReadBytes(int count)
        {
            Require(count);
            var result = new byte[count];
            Buffer.BlockCopy(_buffer, _position, result, 0, count);
            _position += count;
            return result;
        }

        public ushort ReadUShort()
        {
            Require(2);
            int value = _buffer[_position] | (_buffer[_position + 1] << 8);
            _position += 2;
            return (ushort)value;
        }

        public uint ReadUInt()
        {
            Require(4);
            uint value = (uint)(_buffer[_position]
                | (_buffer[_position + 1] << 8)
                | (_buffer[_position + 2] << 16)
                | (_buffer[_position + 3] << 24));
            _position += 4;
            return value;
        }

        public int ReadInt() => unchecked((int)ReadUInt());

        public long ReadLong()
        {
            ulong low = ReadUInt();
            ulong high = ReadUInt();
            return unchecked((long)(low | (high << 32)));
        }

        public float ReadFloat() => BitConverter.Int32BitsToSingle(ReadInt());

        public Vec3 ReadVec3() => new Vec3(ReadFloat(), ReadFloat(), ReadFloat());

        public string ReadString()
        {
            int length = ReadByte();
            Require(length);
            string value = Encoding.UTF8.GetString(_buffer, _position, length);
            _position += length;
            return value;
        }

        private void Require(int count)
        {
            if (_position + count > _end)
            {
                throw new NetFormatException($"Packet truncated: needed {count} more bytes, {Remaining} remaining.");
            }
        }
    }
}
