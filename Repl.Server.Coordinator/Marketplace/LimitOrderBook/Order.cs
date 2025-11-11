using System.Text;

namespace Repl.Server.Coordinator.Marketplace.LimitOrderBook;

public class Order
{

    private int id;
    private bool buyOrSell;
    private int shares;
    private int limitPrice;
    private long entryTime;
    private long eventTime;

    public Order? NextOrder { get; set; }
    public Order? PreviousOrder { get; set; }
    public Limit? ParentLimit { get; set; }

    public Order()
    {
    }

    public Order Initialize(int idNumber, bool buyOrSell, int shares, int limitPrice, long entryTime = 0, long eventTime = 0)
    {
        this.id = idNumber;
        this.buyOrSell = buyOrSell;
        this.shares = shares;
        this.limitPrice = limitPrice;
        this.entryTime = entryTime;
        this.eventTime = eventTime;
        NextOrder = null;
        PreviousOrder = null;
        ParentLimit = null;
        return this;
    }

    public int Id => this.id;
    public int Shares => this.shares;
    public int LimitPrice => this.limitPrice;
    public long EntryTime => this.entryTime;
    public long EventTime => this.eventTime;
    public bool BuyOrSell => this.buyOrSell;
        
    public void Remove()
    {
        ArgumentNullException.ThrowIfNull(this.ParentLimit, nameof(ParentLimit));

        if (PreviousOrder is null)
        {
            ParentLimit.HeadOrder = NextOrder;
        }
        else
        {
            PreviousOrder.NextOrder = NextOrder;
        }
        if (NextOrder is null)
        {
            ParentLimit.TailOrder = PreviousOrder;
        }
        else
        {
            NextOrder.PreviousOrder = PreviousOrder;
        }
        if (ParentLimit.Size <= 0)
        {
            Console.WriteLine("size is subzero. wtf");
        }
        ParentLimit.Size--;
        ParentLimit.TotalVolume -= shares;
    }

    public void PartiallyFillOrder(int orderShares)
    {
        ArgumentNullException.ThrowIfNull(this.ParentLimit, nameof(ParentLimit));

        shares -= orderShares;
        ParentLimit.PartiallyFillVolume(orderShares);
    }

    public static string ToLog(in Order order)
    {
        var sb = new StringBuilder();
        sb.Append($"OrderID: {order.Id}, ");
        sb.Append($"Shares: {order.Shares}, ");
        sb.Append($"LimitPrice: {order.LimitPrice}, ");
        sb.Append($"EntryTime: {order.EntryTime}, ");
        sb.Append($"EventTime: {order.EventTime}\n");

        return sb.ToString();
    }
};