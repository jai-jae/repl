namespace Repl.Server.Coordinator.Marketplace.LimitOrderBook;

public class Limit
{
    private int limitPrice;
    private bool buyOrSell;
        
    public Limit()
    {
    }

    public Limit Initialize(int limitPrice, bool buyOrSell, int size = 0, int totalVolume = 0)
    {
        this.limitPrice = limitPrice;
        this.BuyOrSell = buyOrSell;
        this.Size = size;
        this.TotalVolume = totalVolume;
        HeadOrder = null;
        TailOrder = null;
        return this;
    }

    public int LimitPrice => this.limitPrice;
    public int Size { get; set; }
    public int TotalVolume { get; set; }
    public bool BuyOrSell { get; private set; }
    public Order? HeadOrder { get; set; }
    public Order? TailOrder { get; set; }

    public Order GetHeadOrder()
    {
        ArgumentNullException.ThrowIfNull(this.HeadOrder, nameof(this.HeadOrder));
        return HeadOrder;
    }

    public Order GetTailOrder()
    {
        ArgumentNullException.ThrowIfNull(this.TailOrder, nameof(this.TailOrder));
        return TailOrder;
    }

    public void AddOrder(Order order)
    {
        if (HeadOrder is null)
        {
            HeadOrder = order;
            TailOrder = order;
        }
        else
        {
            ArgumentNullException.ThrowIfNull(this.TailOrder, nameof(this.TailOrder));  
            TailOrder.NextOrder = order;
            order.PreviousOrder = TailOrder;
            order.NextOrder = null;
            TailOrder = order;
        }
        Size += 1;
        TotalVolume += order.Shares;
        order.ParentLimit = this;
    }

    public void PartiallyFillVolume(int orderShares)
    {
        this.TotalVolume -= orderShares;
    }

    public static string ToLog(Limit limit)
    {
        return string.Empty;
    }
};