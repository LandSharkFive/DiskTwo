namespace DiskTwo
{

    /// <summary>
    /// Represents a key-data pair stored in the B-Tree.
    /// </summary>
    public struct Element
    {
        public int Key;   // The key of the element
        public int Data; // That data that each element contains

        public Element() { Key = -1; Data = -1; }
        public Element(int key, int data) { Key = key; Data = data; }
    }

}
