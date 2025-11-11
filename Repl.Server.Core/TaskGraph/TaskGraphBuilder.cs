using System.Collections.Concurrent;
using Repl.Server.Core.TaskGraph.ResourceManagement;


namespace Repl.Server.Core.TaskGraph;

public class TaskGraph
{
    public IReadOnlyCollection<TaskNode.TaskNode> Tasks { get; }
    
    internal TaskGraph(TaskNode.TaskNode[] taskNodes)
    {
        this.Tasks = taskNodes; 
    }
}

public class TaskGraphBuilder
{
    private readonly ConcurrentDictionary<string, TaskNode.TaskNode> taskNodes = new();
    private bool buildCompleted = false;
    
    public TaskNode.TaskNode AddTask(string taskId, SharedResource[] requiredResources, Func<CancellationToken, Task<WorkResult>> work, int priority = 100)
    {
        if (this.buildCompleted)
        {
            throw new InvalidOperationException("Cannot add tasks after the graph has been built.");
        }

        var node = new TaskNode.TaskNode(taskId, requiredResources, work, priority);

        if (this.taskNodes.TryAdd(taskId, node) == false)
        {
            throw new ArgumentException($"task[{taskId}] already exists.", nameof(taskId));
        }

        return node;
    }
    
    public TaskGraph Build()
    {
        if (buildCompleted)
        {
            throw new InvalidOperationException("Build() has already been called on this builder.");
        }

        // 1. 리소스 기반으로 의존성 자동 설정
        var resourceLastOwner = new Dictionary<SharedResource, TaskNode.TaskNode>();

        var sortedNodes = taskNodes.Values
            .OrderByDescending(n => n.Priority)
            .ThenBy(n => n.TaskId);

        foreach (var node in sortedNodes)
        {
            foreach (var resource in node.RequiredResources)
            {
                if (resourceLastOwner.TryGetValue(resource, out var predecessor))
                {
                    // node가 predecessor에 의존하도록 설정
                    node.DependsOn(predecessor);
                }
                resourceLastOwner[resource] = node;
            }
        }

        // 2. 순환 참조 검증
        if (this.HasCycle() == true)
        {
            throw new InvalidOperationException("A cycle was detected in the task graph. Check your manual dependencies.");
        }

        buildCompleted = true;
        
        // 3. 완성된 TaskGraph 객체 생성 및 반환
        return new TaskGraph(this.taskNodes.Values.ToArray());
    }

    private bool HasCycle()
    {
        var visited = new HashSet<TaskNode.TaskNode>();
        var recursionStack = new HashSet<TaskNode.TaskNode>();
        foreach (var node in this.taskNodes.Values)
        {
            if (this.HasCycleDfs(node, visited, recursionStack))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasCycleDfs(TaskNode.TaskNode node, HashSet<TaskNode.TaskNode> visited, HashSet<TaskNode.TaskNode> recursionStack)
    {
        if (recursionStack.Contains(node))
        {
            return true; // 순환 참조 발견
        }

        if (visited.Contains(node) == true)
        {
            return false; // 이미 방문 및 검증 완료
        }

        visited.Add(node);
        recursionStack.Add(node);

        // TaskNode 클래스에 'Successors' (또는 'Dependents') 프로퍼티가 있다고 가정합니다.
        // 원본 코드의 HasCycleDfs가 'node.Successors'를 사용했기 때문입니다.
        foreach (var dependent in node.Successors) 
        {
            if (this.HasCycleDfs(dependent, visited, recursionStack))
            {
                return true;
            }
        }

        recursionStack.Remove(node);
        return false;
    }
}