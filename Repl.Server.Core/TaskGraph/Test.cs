namespace GraphSearch
{
    /// <summary>
    /// Represents a node in a graph.
    /// </summary>
    /// <typeparam name="T">The type of the value stored in the node.</typeparam>
    public class Node<T>
    {
        public T Value { get; set; }
        public List<Node<T>> Adjacencies { get; private set; }
        public bool IsVisited { get; set; }

        public Node(T value)
        {
            Value = value;
            Adjacencies = new List<Node<T>>();
            IsVisited = false;
        }

        public void AddAdjacency(Node<T> node)
        {
            Adjacencies.Add(node);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    /// <summary>
    /// Represents a graph and contains search algorithms.
    /// </summary>
    public class GraphAlgorithms<T>
    {
        /// <summary>
        /// Performs a recursive Depth-First Search (DFS) starting from a given node.
        /// Traverses as far as possible down each branch before backtracking.
        /// </summary>
        /// <param name="startNode">The node to start the search from.</param>
        public void DFSRecursive(Node<T> startNode)
        {
            if (startNode == null || startNode.IsVisited)
            {
                return;
            }

            // Process the node
            Console.Write(startNode.Value + " ");
            startNode.IsVisited = true;

            // Recursively visit all adjacent nodes
            foreach (var node in startNode.Adjacencies)
            {
                DFSRecursive(node);
            }
        }

        /// <summary>
        /// Performs an iterative search using a stack. This is functionally a Depth-First Search (DFS).
        /// A stack (LIFO) ensures that the most recently discovered node is explored next.
        /// </summary>
        /// <param name="startNode">The node to start the search from.</param>
        public void SearchWithStack(Node<T> startNode)
        {
            if (startNode == null) return;

            var stack = new Stack<Node<T>>();
            stack.Push(startNode);

            while (stack.Count > 0)
            {
                var currentNode = stack.Pop();

                if (currentNode.IsVisited)
                {
                    continue;
                }

                // Process the node
                Console.Write(currentNode.Value + " ");
                currentNode.IsVisited = true;

                // Push adjacent nodes onto the stack.
                // We reverse the adjacency list to visit them in the same order as the recursive DFS,
                // which is often left-to-right in examples. This is optional.
                var reversedAdjacencies = currentNode.Adjacencies.AsEnumerable().Reverse();
                foreach (var neighbor in reversedAdjacencies)
                {
                    if (!neighbor.IsVisited)
                    {
                        stack.Push(neighbor);
                    }
                }
            }
        }
        
        /// <summary>
        /// Performs a standard Breadth-First Search (BFS) using a queue.
        /// Explores all neighbor nodes at the present depth prior to moving on to the nodes at the next depth level.
        /// </summary>
        /// <param name="startNode">The node to start the search from.</param>
        public void BFSIterativeWithQueue(Node<T> startNode)
        {
            if (startNode == null) return;

            var queue = new Queue<Node<T>>();
            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();

                if(currentNode.IsVisited)
                {
                    continue;
                }

                // Process the node
                Console.Write(currentNode.Value + " ");
                currentNode.IsVisited = true;

                // Enqueue all unvisited neighbors
                foreach (var neighbor in currentNode.Adjacencies)
                {
                    if (!neighbor.IsVisited)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        /// <summary>
        /// Resets the 'IsVisited' flag for all nodes in a list.
        /// </summary>
        /// <param name="nodes">The list of nodes to reset.</param>
        public static void ResetVisited(List<Node<T>> nodes)
        {
            foreach (var node in nodes)
            {
                node.IsVisited = false;
            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            // --- Graph Setup ---
            //   A --- B --- D
            //   | \   |
            //   |  \  |
            //   C --- E
            var nodes = new List<Node<string>>
            {
                new Node<string>("A"), new Node<string>("B"), new Node<string>("C"),
                new Node<string>("D"), new Node<string>("E")
            };

            var nodeA = nodes[0];
            var nodeB = nodes[1];
            var nodeC = nodes[2];
            var nodeD = nodes[3];
            var nodeE = nodes[4];

            nodeA.AddAdjacency(nodeB);
            nodeA.AddAdjacency(nodeC);
            nodeA.AddAdjacency(nodeE);

            nodeB.AddAdjacency(nodeA);
            nodeB.AddAdjacency(nodeD);
            nodeB.AddAdjacency(nodeE);
            
            nodeC.AddAdjacency(nodeA);
            nodeC.AddAdjacency(nodeE);

            nodeD.AddAdjacency(nodeB);

            nodeE.AddAdjacency(nodeA);
            nodeE.AddAdjacency(nodeB);
            nodeE.AddAdjacency(nodeC);

            var algorithms = new GraphAlgorithms<string>();

            // --- Recursive DFS ---
            Console.WriteLine("Recursive DFS Traversal:");
            algorithms.DFSRecursive(nodeA); // Example output: A B D E C
            Console.WriteLine("\n");
            GraphAlgorithms<string>.ResetVisited(nodes); // Reset for next search

            // --- Iterative DFS with Stack ---
            // This fulfills the "BFS with stack" request, which is actually a DFS.
            Console.WriteLine("Iterative Search with Stack (DFS):");
            algorithms.SearchWithStack(nodeA); // Example output: A B D E C
            Console.WriteLine("\n");
            GraphAlgorithms<string>.ResetVisited(nodes); // Reset for next search
            
            // --- Iterative BFS with Queue (Standard BFS) ---
            Console.WriteLine("Iterative BFS with Queue:");
            algorithms.BFSIterativeWithQueue(nodeA); // Example output: A B C E D
            Console.WriteLine("\n");
            GraphAlgorithms<string>.ResetVisited(nodes); // Reset
        }
    }
}
