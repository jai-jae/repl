using Repl.Server.Coordinator.Marketplace.LimitOrderBook.Extensions;

namespace Repl.Server.Coordinator.Marketplace.LimitOrderBook.TestUtils;

public class NormalRandom : Random
{
    private readonly int range_min = 0;
    private readonly int range_max = 0;
    private readonly int average = 0;
    private readonly int step = 0;

    public NormalRandom(int min, int max)
        : base()
    {
        range_min = min;
        range_max = max;
        average = (range_max - range_min) / 2;
        step = average / 5;
    }

    double fi_func(double x)
    {
        return 1.0 / Math.Sqrt(2 * Math.PI) * Math.Pow(Math.E, -0.5 * x * x);
    }

    double ziggurat()
    {
        double max_y = fi_func(0);
        while (true)
        {
            double x = Sample() * (10.0) - 5.0;
            double y = Sample() * max_y;
            double y0 = fi_func(x);
            if (y <= y0)
            {
                return x;
            }
        }
    }

    double box_muller()
    {
        double fi = Sample();
        double r = Sample();
        double z0 = Math.Cos(2 * Math.PI * fi) * Math.Sqrt(-2 * Math.Log(r));
        return z0;
    }

    int normalizeResult(double x)
    {
        return Math.Clamp((int)(x * step + average), range_min, range_max);
    }

    public override int Next()
    {
        return normalizeResult(ziggurat());
    }
}

public class OrderGenerator
{
    private LimitOrderBook book;
    private int orderId = 10001;

    private Random randomGenerator;

    public OrderGenerator(LimitOrderBook book, int seed1, int seed2)
    {
        this.book = book;
        randomGenerator = new Random(seed1);
    }

    public void RandomInitialOrders(StreamWriter writer, int numberOfOrders, int centreOfBook)
    {
        var priceGenerator = new NormalRandom(50, centreOfBook);

        for (int order = 1; order <= numberOfOrders; ++order)
        {
            int shares = randomGenerator.Next(1, 999);
            int limitPrice = priceGenerator.Next();
            bool buyOrSell = limitPrice < centreOfBook;
            writer.WriteLine($"AddLimit {order} {buyOrSell} {shares} {limitPrice}");
        }
            
        Console.WriteLine("Initial orders generated.");
    }

    public void GenerateRandomOrders(StreamWriter writer, int numberOfOrders)
    {
        var probabilities = new List<double> { 0.25, 0.25, 0.25, 0.25 };
        var actions = new List<Action<StreamWriter>>
        {
            CreateMarketOrder,
            CreateLimitOrder,
            CancelLimitOrder,
            CreateLimitInMarket
        };

        // Convert to cumulative probabilities (equivalent to std::partial_sum)
        for (int i = 1; i < probabilities.Count; i++)
        {
            probabilities[i] += probabilities[i - 1];
        }

        // Generate the orders
        int batchSize = numberOfOrders / 1000;

        for (int i = 1; i <= numberOfOrders; i++)
        {
            double randNum = randomGenerator.NextDouble(); // generates [0.0, 1.0)

            // Find first element >= randNum (equivalent to std::lower_bound)
            int selectedIndex = probabilities.FindIndex(p => p >= randNum);

            if (selectedIndex >= 0 && selectedIndex < actions.Count)
            {
                actions[selectedIndex].Invoke(writer);
            }

            if (i % batchSize == 0)
            {
                Console.WriteLine($"Sample generated {i}");
            }
        }
            
        Console.WriteLine("Orders generated.");
    }

    private void CreateMarketOrder(StreamWriter writer)
    {
        int shares = this.randomGenerator.Next(5, 9999);
        bool buyOrSell = this.randomGenerator.Next(2) == 0;

        writer.WriteLine($"Market {orderId} {buyOrSell} {shares}");
        book.AddMarketOrder(orderId, buyOrSell, shares);
        orderId++;
    }

    private void CreateLimitOrder(StreamWriter writer)
    {
        var normalRandom = new NormalRandom(50, 500);

        int shares = this.randomGenerator.Next(5, 9999);
        bool buyOrSell = this.randomGenerator.Next(2) == 0;
        int limitPrice = 0;

        if (buyOrSell)
        {
            int threshold = 0;
            do
            {
                if (threshold == 10000)
                {
                    break;
                }
                limitPrice = normalRandom.Next();
                threshold++;
            } while (book.GetLowestSell() is not null && limitPrice >= book.GetLowestSell().LimitPrice);
        }
        else
        {
            int threshold = 0;
            do
            {
                if (threshold == 10000)
                {
                    break;
                }
                limitPrice = normalRandom.Next();
                threshold++;
            } while (book.GetHighestBuy() is not null && limitPrice <= book.GetHighestBuy().LimitPrice);
        }

        writer.WriteLine($"AddLimit {orderId} {buyOrSell} {shares} {limitPrice}");
        book.AddLimitOrder(orderId, buyOrSell, shares, limitPrice);
        orderId++;
    }

    private void CancelLimitOrder(StreamWriter writer)
    {
        var order = book.GetRandomOrder(this.randomGenerator);

        if (order is null)
        {
            CreateLimitOrder(writer);
        }
        else
        {
            int orderId = order.Id;
            writer.WriteLine($"CancelLimit {orderId}");
            book.CancelLimitOrder(orderId);
        }
    }

    private void CreateLimitInMarket(StreamWriter writer)
    {
        int shares = randomGenerator.Next(1, 9999);
        int limitPrice;
        bool buyOrSell = randomGenerator.Next(2) == 0;

        if (buyOrSell && book.GetLowestSell() is not null)
        {
            limitPrice = book.GetLowestSell()!.LimitPrice + 1;
        }
        else if (!buyOrSell && book.GetHighestBuy() is not null)
        {
            limitPrice = book.GetHighestBuy()!.LimitPrice - 1;
        }
        else
        {
            CreateLimitOrder(writer);
            return;
        }

        writer.WriteLine($"AddLimitInMarket {orderId} {buyOrSell} {shares} {limitPrice}");
        book.AddLimitOrder(orderId, buyOrSell, shares, limitPrice);
        orderId++;
            
    }

    private void ModifyLimitOrder()
    {
        // TODO
    }
};