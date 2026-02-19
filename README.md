# Classic B-Tree 

A high-performance, disk-persistent B-Tree implementation in C#. This project manages data on a `FileStream` using a fixed-page architecture, recursive divide and conquer bulk loading, and proactive structural maintenance.

## 1. File Architecture
The B-Tree is stored in a single binary file with a dedicated header and a partitioned node structure.

### Physical Layout
| Section | Offset | Size | Purpose |
| :--- | :--- | :--- | :--- |
| **Header** | `0` | 4096 bytes | Stores RootId, NodeCount, and Magic Numbers. |
| **Nodes** | `4096` | Variable | Fixed-size B-Tree nodes (Pages) derived from data partitions. |
| **FreeList** | EOF | Variable | A stack of Disk IDs available for structural re-allocation. |

### Node Serialization
Each node's size is calculated based on the `order` ($k$) of the tree:
`Size = (order * 12) + 16 bytes`.

This fixed-size approach allows $O(1)$ disk seeking by mapping partition IDs to physical offsets:
`Offset = (NodeSize * DiskId) + 4096`.



## 2. Technical Features
* **Divide and Conquer Bulk Load**: Bypasses traditional insertion overhead by recursively partitioning the dataset. It selects optimal separator keys to define the index hierarchy before distributing data into the resulting leaf partitions.
* **Proactive Deletion**: Implements a preventative strategy during descent. If a partition path encounters a node with only $t-1$ keys, the tree redistributes keys from siblings or performs a merge to maintain structural integrity before continuing.
* **ID Reclamation**: To optimize disk usage, IDs from decommissioned nodes are managed via a FreeList. Subsequent partitioning cycles prioritize reclaiming these IDs over extending the file.
* **Integrity Validation**: Includes a `ValidateIntegrity()` method to verify the recursive properties of the tree, detecting circular references, boundary violations, or ordering errors.



## 3. Usage

```csharp
// Initialize a B-Tree and perform operations.
using (var tree = new BTree("data.bin")) 
{
    // Standard insertion (maintains tree properties top-down)
    tree.Insert(42, 100); // Key: 42, Data: 100
    
    if (tree.TrySearch(42, out Element result)) 
    {
        Console.WriteLine($"Found Data: {result.Data}");
    }
    
    tree.Delete(42, 100);
}
