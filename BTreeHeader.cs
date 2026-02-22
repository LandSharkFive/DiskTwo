
namespace DiskTwo
{
    public struct BTreeHeader
    {
        public const int MagicConstant = 0x42542145;

        /// <summary>
        /// Represents the persistent metadata header stored at the beginning of the B-Tree file.
        /// </summary>
        /// 
        public int Magic;           // Unique constant to verify file format integrity
        public int Order;           // Maximum branching factor of the tree
        public int RootId;          // ID of the current root node (-1 if empty)
        public int PageSize;        // Physical size of a single node on disk in bytes
        public int NodeCount;       // Total number of allocated node slots in the file
        public int FreeListCount;   // Number of deleted nodes currently available for reuse
        public long FreeListOffset; // Byte position where the FreeList data begins

        /// <summary> Serializes the header fields to the current file stream. </summary>
        public void Write(BinaryWriter writer)
        {
            writer.Write(Magic);
            writer.Write(Order);
            writer.Write(RootId);
            writer.Write(PageSize);
            writer.Write(NodeCount);
            writer.Write(FreeListCount);
            writer.Write((long)FreeListOffset);
        }

        /// <summary> Deserializes the header fields from the current file stream. </summary>
        public static BTreeHeader Read(BinaryReader reader)
        {
            int magic = reader.ReadInt32();
            if (magic != MagicConstant)
            {
                throw new InvalidDataException("Invalid File Format");
            }

            return new BTreeHeader
            {
                Magic = magic,
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
