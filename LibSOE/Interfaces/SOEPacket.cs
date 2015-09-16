namespace SOE
{
    public class SOEPacket
    {
        public ushort OpCode;
        public byte[] Raw;

        public SOEPacket(ushort opCode, byte[] rawMessage)
        {
            OpCode = opCode;
            Raw = rawMessage;
        }

        public void SetOpCode(ushort opCode)
        {
            OpCode = opCode;
        }

        public ushort GetOpCode()
        {
            return OpCode;
        }
    }
}
