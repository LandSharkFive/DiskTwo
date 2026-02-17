# Classic B-Tree 

A classic, disk-persistent B-Tree implementation in C#. This project manages data on a FileStream using fixed-page architecture, disk-space recovery (via a FreeList) and preventative deletion logic.

## 1. File Architecture
The B-Tree is stored in a single binary file with a dedicated header and paged node structure.

### Physical Layout
| Section | Offset | Size | Purpose |
| :--- | :--- | :--- | :--- |
| **Header** | `0` | 4096 bytes | Stores RootId, NodeCount, and Magic Numbers. |
| **Nodes** | `4096` | Variable | The fixed-size B-Tree nodes (Pages). |
| **FreeList** | EOF | Variable | A stack of Disk IDs, appended during Close. |

### Node Serialization
Each node's size is calculated based on the `order` of the tree:
`Size = (order * 12) + 16 bytes`.

This fixed-size approach allows O(1) disk seeking using the formula:
`Offset = (NodeSize * DiskId) + 4096`.

## 2. Technical Features
* **Preventative Deletion**: Implements a preventative strategy during descent. If a child node has only t-1 keys, the tree borrows from siblings or merges before continuing the search.
* **FreeList Recovery**: To prevent file bloat, deleted nodes are added to a FreeList. New insertions prioritize reusing these IDs over appending to the file.
* **Integrity Validation**: Includes ValidateIntegrity() method to detect circular references, underflows (thin nodes), or ordering errors.
* **Magic Constant**: The file begins with a magic number to ensure file type compatibility.

## 3. Usage

```csharp
// Initialize a new B-Tree.
using (var tree = new BTree("data.bin")) 
{
    tree.Insert(42, 100); // Key: 42, Data: 100
    
    if (tree.TrySearch(42, out Element result)) 
    {
        Console.WriteLine($"Found Data: {result.Data}");
    }
    
    tree.Delete(42, 100);
}
```
