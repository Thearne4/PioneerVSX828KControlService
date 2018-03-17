namespace PioneerController
{
    public class ReceivedData
    {
        public byte[] Data { get; private set; }
        public ReceivedData(byte[] data)
        {
            Data = data;
        }
    }
}