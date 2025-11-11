using System.Diagnostics.CodeAnalysis;
using Repl.Server.Core.Pooling;

namespace Repl.Server.Coordinator.Marketplace.LimitOrderBook;

public class LimitOrderBook
{
    private readonly Pool<Limit> limitPool = new Pool<Limit>(10000, () => new Limit(), LoadingMode.Lazy, AccessMode.FIFO);
    private readonly Pool<Order> orderPool = new Pool<Order>(1000_000, () => new Order(), LoadingMode.Lazy, AccessMode.LIFO);

    private readonly SortedDictionary<int, Limit> buyTree;
    private readonly SortedDictionary<int, Limit> sellTree;
    private readonly Dictionary<int, Order> orderMap;
        
    public int OrderCount => orderMap.Count;
    public List<Order> Orders => orderMap.Values.ToList();
    public int ExecutedOrdersCount { get; private set; }

    public LimitOrderBook()
    {
        buyTree = new SortedDictionary<int, Limit>();
        sellTree = new SortedDictionary<int, Limit>();
        orderMap = new Dictionary<int, Order>();
    }

    public Limit? GetLowestSell()
    {
        if (sellTree.Count == 0)
        {
            return null;
        }
        var price = sellTree.Keys.First();
        return this.sellTree[price];
    }
    public Limit? GetHighestBuy()
    {
        if (buyTree.Count == 0)
        {
            return null;
        }
        var price = buyTree.Keys.Last();
        return this.buyTree[price];
    }

    public void AddMarketOrder(int orderId, bool buyOrSell, int shares)
    {
        ExecutedOrdersCount = 0;
        // var tree = (buyOrSell) ? buyTree : sellTree;
        // tree.RebalanceCount = 0;
        ProcessMarketOrder(orderId, buyOrSell, shares);
    }

    public void AddLimitOrder(int orderId, bool buyOrSell, int shares, int limitPrice)
    {
        var remainShares = this.ProcessLimitOrderInMarket(orderId, buyOrSell, shares, limitPrice);

        if (remainShares != 0)
        {
            var newOrder = this.orderPool.Rent().Initialize(orderId, buyOrSell, remainShares, limitPrice, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            this.orderMap[orderId] = newOrder;

            InsertLimit(limitPrice, buyOrSell);

            var tree = buyOrSell ? this.buyTree : this.sellTree;
            tree.TryGetValue(limitPrice, out var limit);
                 
            // insertLimit has inserted limitPrice. tree.TryGetValue must exist.
            limit!.AddOrder(newOrder);
        }
    }

    public bool ModifyLimitOrder(int orderId, int shares, int limitPrice)
    {
        if (SearchOrderMap(orderId, out var order) == true)
        {
            bool buyOrSell = order.BuyOrSell;

            CancelLimitOrder(orderId);
            AddLimitOrder(orderId, buyOrSell, shares, limitPrice);
            return true;
        }
        return false;
    }

    public bool CancelLimitOrder(int orderId)
    {
        Console.WriteLine($"CancelLimit called. OrderId:{orderId}");
        ExecutedOrdersCount = 0;
        SearchOrderMap(orderId, out var order);
        if (order is not null)
        {
            var tree = order.BuyOrSell ? buyTree : sellTree;
            // tree.RBTreeRebalanceCount = 0;

            order.Remove();
            if (order.ParentLimit!.Size == 0)
            {
                DeleteLimit(order.LimitPrice, order.BuyOrSell);
            }

            orderMap.Remove(orderId);
            this.orderPool.Return(order);
            return true;
        }
        return false;
    }

    public bool SearchOrderMap(int orderId, [NotNullWhen(true)] out Order? order)
    {
        return this.orderMap.TryGetValue(orderId, out order);
    }

    public bool SearchLimitMaps(int limitPrice, bool buyOrSell, [NotNullWhen(true)] out Limit? limit)
    {
        var tree = buyOrSell ? buyTree : sellTree;
        return tree.TryGetValue(limitPrice, out limit);
    }

    private void InsertLimit(int limitPrice, bool buyOrSell)
    {
        var tree = buyOrSell ? buyTree : sellTree;
        tree.TryAdd(limitPrice, this.limitPool.Rent().Initialize(limitPrice, buyOrSell, 0, 0));
    }

    private void DeleteLimit(int limitPrice, bool buyOrSell)
    {
        var tree = buyOrSell ? buyTree : sellTree;
        tree.Remove(limitPrice, out var limit);
        if (limit is not null)
        {
            this.limitPool.Return(limit);
        }
    }

    private void ProcessMarketOrder(int orderId, bool buyOrSell, int shares)
    {
        Limit? edgeLimit = buyOrSell ? GetLowestSell() : GetHighestBuy();
        while (edgeLimit is not null && edgeLimit.GetHeadOrder().Shares <= shares)
        {
            Order? headOrder = edgeLimit.GetHeadOrder();
            shares -= headOrder.Shares;
            headOrder.Remove();

            if (edgeLimit.Size == 0)
            {
                DeleteLimit(edgeLimit.LimitPrice, edgeLimit.BuyOrSell);
            }


            if (orderMap.Remove(headOrder.Id) == false)
            {
                Console.WriteLine("id does not exist.");
            }
            ExecutedOrdersCount++;
            edgeLimit = buyOrSell ? GetLowestSell() : GetHighestBuy();
        }

        if (edgeLimit is not null && shares != 0)
        {
            edgeLimit.GetHeadOrder().PartiallyFillOrder(shares);
            ExecutedOrdersCount++;
        }
    }

    private int ProcessLimitOrderInMarket(int orderId, bool buyOrSell, int shares, int limitPrice)
    {
        if (buyOrSell)
        {
            var lowestSell = GetLowestSell();
            while (lowestSell is not null && shares != 0 && lowestSell.LimitPrice <= limitPrice)
            {
                if (shares <= lowestSell.TotalVolume)
                {
                    ProcessMarketOrder(orderId, buyOrSell, shares);
                    return 0;
                }
                else
                {
                    shares -= lowestSell.TotalVolume;
                    ProcessMarketOrder(orderId, buyOrSell, lowestSell.TotalVolume);
                }
                lowestSell = GetLowestSell();
            }
            return shares;
        }
        else
        {
            var highestBuy = GetHighestBuy();
            while (highestBuy is not null && shares != 0 && highestBuy.LimitPrice >= limitPrice)
            {
                if (shares <= highestBuy.LimitPrice)
                {
                    ProcessMarketOrder(orderId, buyOrSell, shares);
                    return 0;
                }
                else
                {
                    shares -= highestBuy.LimitPrice;
                    ProcessMarketOrder(orderId, buyOrSell, highestBuy.LimitPrice);
                }
            }
            return shares;
        }
    }
};