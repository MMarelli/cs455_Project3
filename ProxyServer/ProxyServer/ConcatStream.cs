using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ProxyServer
{
    internal enum Consturctor
    {
        withoutLen = 0,
        withLen
    }

    public class ConcatStream : Stream
    {
        private Stream _First, _Second;
        private long _Length;
        private long _Position;
        Consturctor _C;

        public ConcatStream(Stream first, Stream second)
        {
            _C = Consturctor.withoutLen;
            _First = first;
            _Second = second;
            _Position = 0;
            try
            {
                var temp = _First.Length; //we cannot support streams with no lenght test for exception
            }
            catch
            {
                throw new ArgumentException();
            }
            if (second.CanSeek)
            {
                second.Position = 0;
            }

            _First.Position = 0;
        }

        public ConcatStream(Stream first, Stream second, long fixedLength)
        {
            _C = Consturctor.withLen;
            _First = first;
            _Second = second;
            _Position = 0;
            var temp = _First.Length;

            _Length = fixedLength;
        }

        public override bool CanRead
        {
            get { return _First.CanRead && _Second.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _First.CanSeek && _Second.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _First.CanWrite && _Second.CanWrite; }
        }

        public override long Length
        {
            get
            {
                if (_C == Consturctor.withoutLen)
                {
                    return _First.Length + _Second.Length;
                }
                else
                {
                    return _Length;
                }
            }
        }

        /// <summary>
        /// Position in concat stream. Throws exception on cant seek
        /// </summary>
        public override long Position
        {
            get
            {
                if (!CanSeek)
                {
                    throw new NotSupportedException();
                }
                return _Position;
            }
            set
            {
                if (!CanSeek)
                {
                    throw new NotSupportedException();
                }
                if (value < 0)
                {
                    _Position = value;
                }
                _Position = value;
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Read up to count bytes from the stream into the buffer starting at offset
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
            {
                throw new NotSupportedException();
            }
            int i = 0;
            while (i < count)
            {
                if (_Position >= _First.Length)
                {
                    if (_Second.CanSeek)
                    {
                        _Second.Position = Position - _First.Length;
                    }
                    int read;

                    NetworkStream s = _Second as NetworkStream; //had to add this to prevent reading an empty Network stream
                    if (s != null && !s.DataAvailable && Consturctor.withoutLen == _C || Consturctor.withLen == _C && _Position >= Length)
                    {
                        return i;
                    }
                    try
                    {
                        read = _Second.Read(buffer, i + offset, count - i);
                        _Position += read;
                        return i + read; //return the count read from the first and the second
                    }
                    catch { return i; };
                    
                }
                else
                {
                    _First.Position = _Position;
                    i = _First.Read(buffer, offset, Math.Min((int)(_First.Length - _Position), count));
                    _Position += i;
                }
            }
            return i;
        }

        /// <summary>
        /// Sets _Position to SeekOrigin + offset. Throws exception on cant seek
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!CanSeek)
            {
                throw new NotSupportedException();
            }
            if (origin == SeekOrigin.Begin)
            {
                _Position = offset;
            }
            else if (origin == SeekOrigin.Current)
            {
                _Position += offset;
            }
            else //origin == end
            {
                _Position = _First.Length + _Second.Length + offset;
            }
            return _Position;
        }

        /// <summary>
        /// If Supported sets length
        /// </summary>
        /// <param name="value"></param>
        public override void SetLength(long value)
        {
            if (_C == Consturctor.withLen)
            {
                _Length = value;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Writes count bytes for buffer starting at the offset
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new NotSupportedException();
            }
            int i = 0;
            while (i < count)
            {
                if (_Position >= _First.Length)
                {
                    if (_Second.CanSeek)
                    {
                        _Second.Position = Position - _First.Length;
                    }
                    _Position += count - i;
                    _Second.Write(buffer, i + offset, count - i);
                    return;
                }
                else
                {
                    _First.Position = _Position;
                    i = Math.Min((int)(_First.Length - _Position), count);
                    _Position += i;
                    _First.Write(buffer, offset, i);
                }
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return base.CanTimeout;
            }
        }
    }
}
