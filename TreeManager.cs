namespace DiskTwo
{
    /// <summary>
    /// Manages the low-level disk I/O for a B-Tree structure, 
    /// handling node serialization, header management, and file stream lifecycle.
    /// </summary>
    public class TreeManager : IDisposable
    {
        public BTreeHeader Header;

        private FileStream MyFileStream; // Keep open during the build process
        private BinaryWriter MyWriter;
        private int Order = 0;

        private const int HeaderSize = 4096;  // Reserved space at top of the file.
        private const int MagicConstant = BTreeHeader.MagicConstant;


        /// <summary>
        /// Initializes a new B-Tree file. Sets up the initial header and validates page sizes.
        /// </summary>
        public TreeManager(string path, int order = 64)
        {
            Order = order;

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("FileName cannot be empty.");
            }

            if (Order < 4)
            {
                throw new ArgumentException("Order must be at least 4.");
            }


            // 1. Open File Stream for persistent storage.
            MyFileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
            MyWriter = new BinaryWriter(MyFileStream);

            // 2. Initialize the B-Tree Header with default metadata.
            Header = new BTreeHeader();
            Header.RootId = -1;    // -1 indicates an empty tree.
            Header.NodeCount = 0;
            Header.Order = Order;
            Header.Magic = MagicConstant;
            Header.FreeListCount = 0;
            Header.FreeListOffset = 0;
            Header.PageSize = BNode.CalculateNodeSize(Order);
            SaveHeader();

            // 3. Structural Validation: Ensure the calculated PageSize can actually hold the node data.
            int metadataSize = 12; // Leaf (4), NumKeys (4), Id (4)
            int keysSize = Order * 8; // Each Element is Key(4) + Data(4)
            int childrenSize = (Order + 1) * 4; // Int pointers
            int required = metadataSize + keysSize + childrenSize;
            if (Header.PageSize < required)
            {
                throw new Exception($"PageSize {Header.PageSize} is too small for Order {Order}. Needs {required}.");
            }
        }

        /// <summary>
        /// Explicitly closes the file stream and releases resources.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Safely persists data and releases the file stream while preventing redundant cleanup.
        /// </summary>
        public void Dispose()
        {
            // Dispose the writer first, then the stream
            MyWriter?.Dispose();
            MyFileStream?.Dispose();
            GC.SuppressFinalize(this);
        }


        public void SaveToDisk(BNode node)
        {
            if (node.Id < 0)
            {
                throw new ArgumentOutOfRangeException("CRITICAL: Writing negative disk id.");
            }

            long offset = CalculateOffset(node.Id);
            MyFileStream.Seek(offset, SeekOrigin.Begin);
            node.Write(MyWriter);
        }

        /// <summary>
        /// Calculates the exact byte offset in the file for a given Node ID.
        /// </summary>
        private long CalculateOffset(int disk)
        {
            if (disk < 0)
                throw new ArgumentOutOfRangeException(nameof(disk), "Node ID cannot be negative.");

            if (Header.PageSize < 64)
                throw new ArgumentException("Invalid Page Size.");

            return ((long)Header.PageSize * disk) + HeaderSize;
        }

        /// <summary>
        /// Updates the file's header (first 4096 bytes) with current tree metadata.
        /// </summary>
        public void SaveHeader()
        {
            byte[] buffer = new byte[4096];
            using (var ms = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(ms))
            {
                Header.Write(writer);
            }

            MyFileStream.Seek(0, SeekOrigin.Begin);
            MyWriter.Write(buffer);
        }


        /// <summary>
        /// Instantiates a new node, assigning it the next available ID in the file sequence.
        /// </summary>
        public BNode CreateNode(bool isLeaf)
        {
            BNode node = new BNode(Header.Order, isLeaf);
            node.Id = Header.NodeCount++;
            return node;
        }
    }
}