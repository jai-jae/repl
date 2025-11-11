using System.Diagnostics;

namespace Repl.Server.Coordinator.Marketplace.LimitOrderBook.TestUtils;

public class OrderExecutor
{
    private readonly LimitOrderBook book;
    private readonly Dictionary<string, Action<string[]>> orderFunctions;
        
    private void loadMarketOrder(string[] orderInfo)
    {
        int.TryParse(orderInfo[1], out int orderId);
        bool.TryParse(orderInfo[2], out bool buyOrSell);
        int.TryParse(orderInfo[3], out int shares);

        book.AddMarketOrder(orderId, buyOrSell, shares);
    }
    private void loadAddLimitOrder(string[] orderInfo)
    {
        int.TryParse(orderInfo[1], out int orderId);
        bool.TryParse(orderInfo[2], out bool buyOrSell);
        int.TryParse(orderInfo[3], out int shares);
        int.TryParse(orderInfo[4], out int limitPrice);

        book.AddLimitOrder(orderId, buyOrSell, shares, limitPrice);
    }
    private void loadCancelLimitOrder(string[] orderInfo)
    {
        int.TryParse(orderInfo[1], out int orderId);

        book.CancelLimitOrder(orderId);
    }

    private void loadModifyLimitOrder(string[] orderInfo)
    {
        // TODO
    }
        
    public OrderExecutor(LimitOrderBook book)
    {
        this.book = book;
        this.orderFunctions = new Dictionary<string, Action<string[]>>()
        {
            ["Market"] = loadMarketOrder,
            ["AddLimit"] = loadAddLimitOrder,
            ["CancelLimit"] = loadCancelLimitOrder,
            ["AddLimitInMarket"] = loadAddLimitOrder
        };
    }

    public void Run(StreamReader reader, StreamWriter writer)
    {
        string? line;
        var stopwatch = new Stopwatch();

        while ((line = reader.ReadLine()) is not null)
        {
            var orderInfo = line.Split(" ");
            orderFunctions.TryGetValue(orderInfo[0], out var orderFunction);

            stopwatch.Restart();
            orderFunction!.Invoke(orderInfo);
            stopwatch.Stop();

            long nanoseconds = stopwatch.ElapsedTicks * 1000_000_000 / Stopwatch.Frequency;
            writer.WriteLine($"{line}: {nanoseconds} ns, {book.ExecutedOrdersCount}");
        }
    }
}