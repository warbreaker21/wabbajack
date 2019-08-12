using K4os.Hash.xxHash;
using System;
using System.IO;

namespace Wabbajack.Common
{
    public class HashingOutputStream : Stream
    {
        private bool called;
        private byte[] _pending;
        private int _offset;
        private XXH64 _hasher;

        public HashingOutputStream()
        {
            _pending = new byte[Consts.HASH_CHUNK_SIZE];
            _offset = 0;

            _hasher = new XXH64();
        }
        public override bool CanRead => throw new System.NotImplementedException();

        public override bool CanSeek => throw new System.NotImplementedException();

        public override bool CanWrite => true;

        public override long Length => throw new System.NotImplementedException();

        public override long Position { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public override void Flush()
        {
           
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            TOP:
            var remain = _pending.Length - _offset;
            if (remain > count)
            {
                Array.Copy(buffer, offset, _pending, _offset, count);
            }
            else if (remain < count)
            {
                Array.Copy(buffer, offset, _pending, _offset, remain);
                _hasher.Update(_pending, 0, _pending.Length);
                offset += remain;
                count -= remain;
                _offset = 0;
                goto TOP;
            }
            else if (remain == count)
            {
                Array.Copy(buffer, offset, _pending, _offset, remain);
                _hasher.Update(_pending, 0, _pending.Length);
                _offset = 0;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _offset != 0)
            {
                _hasher.Update(_pending, 0, _offset);
            }
        }

        public string GetHash()
        {
            return _hasher.DigestBytes().ToBase64();
        }
    }
}