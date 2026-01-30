
namespace DiskTwo
{
    /// <summary>
    /// Represents a B-Tree node.
    /// </summary>
    public class BNode
    {
        public int NumKeys { get; set; }    // Number of keys the node has
        public bool IsLeaf { get; set; }     // Is this a leaf node? 
        public int Id { get; set; }  // Id of the node (record index) in the file

        // Arrays replace C pointers and dynamic allocation.
        public Element[] Keys { get; set; } // Holds the keys of the node (size: order - 1)
        public int[] Kids { get; set; }     // Holds the children's disk positions (size: order)

        public BNode(int order)
        {
            Keys = new Element[order];
            Kids = new int[order + 1];
            for (int i = 0; i < order; i++)
            {
                Keys[i] = new Element(-1, -1); // Unique instances
            }
            Array.Fill(Kids, -1);
        }

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
        public void RemoveKeyAndChildAt(int pos, int order)
        {
            // 1. Shift Keys to the left
            for (int i = pos; i < order - 2; i++)
            {
                Keys[i] = Keys[i + 1];
            }

            // 2. Shift Children to the left (removing the pointer at pos + 1)
            for (int i = pos + 1; i < order - 1; i++)
            {
                Kids[i] = Kids[i + 1];
            }

            // 3. Decrement
            NumKeys--;

            // 4. THE NUCLEAR WIPE: Hard-code the reset of the vacated slots
            // We wipe everything from the new NumKeys to the end of the physical array
            for (int i = NumKeys; i < order - 1; i++)
            {
                Keys[i] = new Element { Key = -1, Data = -1 };
            }

            int startWipingKids = IsLeaf ? 0 : NumKeys + 1;
            for (int i = startWipingKids; i < order; i++)
            {
                Kids[i] = -1;
            }
        }


    }
}
