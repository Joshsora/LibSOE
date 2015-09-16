using System.Collections.Generic;

namespace SOE
{
    public class SOEMessage
    {
        public ushort OpCode;
        public byte[] Raw;

        public List<byte[]> Fragments;
        public bool IsFragmented;

        public SOEMessage(ushort opCode, byte[] rawMessage)
        {
            OpCode = opCode;
            Raw = rawMessage;

            Fragments = new List<byte[]>();
            IsFragmented = false;
        }

        public void SetOpCode(ushort opCode)
        {
            OpCode = opCode;
        }

        public uint GetOpCode()
        {
            return OpCode;
        }

        public void AddFragment(byte[] fragment)
        {
            if (!IsFragmented)
            {
                IsFragmented = true;
            }

            Fragments.Add(fragment);
        }
    }
}
