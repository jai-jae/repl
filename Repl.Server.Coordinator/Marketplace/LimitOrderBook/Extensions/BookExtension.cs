namespace Repl.Server.Coordinator.Marketplace.LimitOrderBook.Extensions;

public static class BookExtension
{
    public static Order? GetRandomOrder(this LimitOrderBook book, Random random)
    {
        if (book.OrderCount > 0)
        {
            var randomIndex = random.Next(book.OrderCount);
            return book.Orders.ElementAt(randomIndex);
        }
        return null;
    }

    public static List<int> InOrderTreeTraversal(this LimitOrderBook book, bool buyOrSell)
    {
        return new List<int>();
    }

    public static List<int> PreOrderTreeTraversal(this LimitOrderBook book, bool buyOrSell)
    {
        return new List<int>();
    }

    public static List<int> PostOrderTreeTraversal(this LimitOrderBook book, bool buyOrSell)
    {
        return new List<int>();
    }
}