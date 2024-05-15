using System.ComponentModel.DataAnnotations;

namespace AnimeWebPage.Models
{
    public class Product
    {
        public int ProductId { get; set; }
        [Required]
        [MaxLength(255)]
        public string Name { get; set; }
        public string Description { get; set; }
        [DisplayFormat(DataFormatString = "{0:c}")]
        public decimal Price { get; set; }
        public int Rating { get; set; }
        public string? Photo { get; set; }

        public int CategoryId { get; set; }

        public Category? Category { get; set; }
    }
}
