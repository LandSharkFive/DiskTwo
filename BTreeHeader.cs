
namespace DiskTwo
{
    public struct BTreeHeader
    {
        public int Magic;
        public int Order;
        public int RootId;
        public int PageSize;
        public int NodeCount;
        public int FreeListCount;
        public long FreeListOffset;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Magic);
            writer.Write(Order);
            writer.Write(RootId);
            writer.Write(PageSize);
            writer.Write(NodeCount);
            writer.Write(FreeListCount);
            writer.Write(FreeListOffset);
        }

        public static BTreeHeader Read(BinaryReader reader)
        {
            return new BTreeHeader
            {
                Magic = reader.ReadInt32(),
                Order = reader.ReadInt32(),
                RootId = reader.ReadInt32(),
                PageSize = reader.ReadInt32(),
                NodeCount = reader.ReadInt32(),
                FreeListCount = reader.ReadInt32(),
                FreeListOffset = reader.ReadInt64()
            };
        }
    }
}
