using System.Diagnostics;

namespace Repl.Server.Coordinator.Marketplace.LimitOrderBook.TestUtils;

public static class LimitOrderBookTest
{
    public static void Execute(int numberOfOrders)
    {
        LimitOrderBook book = new LimitOrderBook();
        OrderGenerator orderGenerator = new OrderGenerator(book, 1, 1);
        OrderExecutor orderExecutor = new OrderExecutor(book);


        using (StreamWriter initialOrderWriter = new StreamWriter("LimitOrderBookTest/initial-random-orders.txt"))
        {
            orderGenerator.RandomInitialOrders(initialOrderWriter, 10000, 500);
        }

        using StreamReader reader1 = new StreamReader("LimitOrderBookTest/initial-random-orders.txt");
        using StreamWriter writer1 = new StreamWriter("LimitOrderBookTest/initial-performance-metrics.txt");
        orderExecutor.Run(reader1, writer1);


        using (StreamWriter orderWriter = new StreamWriter("LimitOrderBookTest/random-orders.txt"))
        {
            orderGenerator.GenerateRandomOrders(orderWriter, numberOfOrders);
        }

        using StreamReader reader2 = new StreamReader("LimitOrderBookTest/random-orders.txt");
        using StreamWriter writer2 = new StreamWriter("LimitOrderBookTest/performance-metrics.txt");

        var stopwatch = Stopwatch.StartNew();
        orderExecutor.Run(reader2, writer2);
        Console.WriteLine($"Total time to prcoess {numberOfOrders} orders: {stopwatch.ElapsedMilliseconds} ms");
    }
}