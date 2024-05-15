 
namespace AnimeWebPage.Models
{
    public class Cart
    {
        public int CartId { get; set; }
        public string CustomerId { get; set;}
        public int ProductId { get; set;}
        public Product? Product { get; set;}
        public decimal Price { get; set;}
        public int Quantity { get; set;}
        public DateTime DateCreated { get; set; }


    }
}
