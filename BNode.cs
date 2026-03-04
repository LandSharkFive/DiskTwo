using System.Text;

namespace DiskTwo
{
    /// <summary>
    /// Represents a B-Tree node.
    /// </summary>
    public class BNode
    {
        public int Order { get; set; }   // The maximum number of keys allowed.
        public int NumKeys { get; set; }    // Number of keys the node has
        public bool Leaf { get; set; }     // Is this a leaf node? 
        public int Id { get; set; }  // Id of the node (record index) in the file

        // Arrays replace C pointers and dynamic allocation.
        public Element[] Keys { get; set; } // Holds the keys of the node (size: order - 1)
        public int[] Kids { get; set; }     // Holds the children's disk positions (size: order)

        /// <summary>
        /// Initializes a new instance of a B-Tree node with specified capacity.
        /// </summary>
        public BNode(int order, bool leaf)
        {
            Order = order;
            Leaf = leaf;
            Keys = new Element[order];
            Kids = new int[order + 1];
            for (int i = 0; i < order; i++)
            {
                Keys[i] = new Element(-1, -1); // Unique instances
            }
            Array.Fill(Kids, -1);
        }

        /// <summary>
        /// Initializes a new internal (non-leaf) B-Tree node.
        /// </summary>
        public BNode(int order) : this(order, false) { }


        /// <summary>
        /// Calculates the fixed size of a B-Tree node record in bytes.
        /// </summary>
        public static int CalculateNodeSize(int order)
        {
            // Three int32 = 12 bytes.
            // Keys are (order) * two int32 = order * 8.
            // Indexes are (order + 1) * int32 = (order * 4) + 4.
            // Total is 16 + order * 8 + order * 4 = (order * 12) + 16.
            // Simplify the math.
            return (order * 12) + 16;
        }

        /// <summary>
        /// Remove Key and Child shrinks the node by shifting keys and child pointers left to overwrite the element. 
        /// Decrements the key count and performs a nuclear wipe of all trailing slots to prevent data ghosting. 
        /// Safely handles both leaf and internal node layouts by nullifying unused pointer indices.
        /// </summary>
        public void RemoveKeyAndChildAt(int pos)
        {
            // 1. Shift Keys to the left
            for (int i = pos; i < Order - 2; i++)
            {
                Keys[i] = Keys[i + 1];
            }

            // 2. Shift Children to the left (removing the pointer at pos + 1)
            for (int i = pos + 1; i < Order - 1; i++)
            {
                Kids[i] = Kids[i + 1];
            }

            // 3. Decrement
            NumKeys--;

            // 4. THE NUCLEAR WIPE: Hard-code the reset of the vacated slots
            // We wipe everything from the new NumKeys to the end of the physical array
            for (int i = NumKeys; i < Order; i++)  // order?
            {
                Keys[i] = new Element { Key = -1, Data = -1 };
            }

            int startWipingKids = Leaf ? 0 : NumKeys + 1;
            for (int i = startWipingKids; i <= Order; i++)  
            {
                Kids[i] = -1;
            }
        }

        /// <summary>
        /// Returns a human-readable string representation of the node's internal state.
        /// </summary>
        /// <returns>A formatted string containing the Node ID, Leaf status, Key values, and Child pointers.</returns>
        /// <remarks>
        /// Primarily used for debugging and logging. It only iterates through NumKeys 
        /// to ensure the output reflects the logical contents of the node rather than the raw physical arrays.
        /// </remarks>
        /// <summary>
        /// Returns a multi-line string representing the node's current state, 
        /// showing metadata, keys, and child pointers.
        /// </summary>
        public override string ToString()
        {
            string keysStr = string.Join(" ", Keys.Take(NumKeys).Select(k => k.Key));
            string kidsStr = Leaf ? "None (Leaf)" : string.Join(" ", Kids.Take(NumKeys + 1));

            return $"Node {Id} (Leaf: {Leaf}, Keys: {NumKeys})\n" +
                   $"Keys: [{keysStr}]\n" +
                   $"Kids: [{kidsStr}]";
        }

        // <summary>
        /// Populates the node's properties and arrays by reading binary data from the provided stream.
        /// </summary>
        public void Read(BinaryReader reader)
        {
            // 1. Read Metadata
            Leaf = reader.ReadInt32() == 1;
            NumKeys = reader.ReadInt32();
            Id = reader.ReadInt32();

            // 2. Read Keys
            for (int i = 0; i < Order; i++)
            {
                int key = reader.ReadInt32();
                int data = reader.ReadInt32();
                Keys[i] = new Element(key, data);
            }

            // 3. Read Children
            for (int i = 0; i <= Order; i++)
            {
                Kids[i] = reader.ReadInt32();
            }
        }

        /// <summary>
        /// Serializes the node's current state into a binary format for persistent storage.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> used to commit the node data to the file.</param>
        /// <remarks>
        public void Write(BinaryWriter writer)
        {
            writer.Write(Leaf ? 1 : 0);
            writer.Write(NumKeys);
            writer.Write(Id);

            // Pack Keys
            for (int i = 0; i < Order; i++)
            {
                if (i < NumKeys)
                {
                    writer.Write(Keys[i].Key);
                    writer.Write(Keys[i].Data);
                }
                else
                {
                    writer.Write(-1); // Padding
                    writer.Write(-1);
                }
            }

            // Pack Children
            for (int i = 0; i <= Order; i++)
            {
                writer.Write(i <= NumKeys ? Kids[i] : -1);
            }
        }

        /// <summary>
        /// Performs a logical health check on the node to ensure key counts and child pointers are consistent.
        /// </summary>
        public bool Validate()
        {
            // 1. Check Key Count bounds.
            if (NumKeys < 0 || NumKeys > Order) return false;

            // 2. Check logical consistency: Internal nodes MUST have NumKeys + 1 children.
            if (!Leaf)
            {
                for (int i = 0; i <= NumKeys; i++)
                {
                    if (Kids[i] < 0) return false; // Every active key needs a valid child path.
                }
            }
            return true;
        }

        /// <summary>
        /// Validates the node's integrity and throws an exception if any structural corruption is detected.
        /// </summary>
        public void ValidateAndThrow()
        {
            // 1. Check Key Count bounds.
            if (NumKeys < 0 || NumKeys > Order)
            {
                throw new ArgumentException(nameof(NumKeys));
            }

            // 2. Check logical consistency: Internal nodes MUST have NumKeys + 1 children.
            if (!Leaf)
            {
                for (int i = 0; i <= NumKeys; i++)
                {
                    if (Kids[i] < 0)
                    {
                        throw new Exception("Child Id cannot be negative."); 
                    }
                }
            }
        }

    }
}
