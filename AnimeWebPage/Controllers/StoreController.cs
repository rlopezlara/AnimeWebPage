using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AnimeWebPage.Data;
using AnimeWebPage.Models;
using Microsoft.AspNetCore.Authorization;
using AnimeWebPage.Extension;
using Stripe.Checkout;
using Stripe;

namespace AnimeWebPage.Controllers
{
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext _context;
        // use the configuration object to access values from appsetting.Json
        private readonly IConfiguration _configuration;
        //user Id to request the aplicationDbContext and Iconfiguration objects on Controller Creation

        public StoreController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: Store
        public async Task<IActionResult> Index()
        {
            return View(await _context.Categories.ToListAsync());
        }

        // GET: Store/Browse/3
        public async Task<IActionResult> Browse(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var products = _context.Products
                .Where(p => p.CategoryId == id)
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.CategoryName = _context.Categories.Find(id).Name;

            return View(products);
        }
        // post store /AddToCart
        [HttpPost]
        public IActionResult AddToCart([FromForm] int ProductId, [FromForm] int Quantity)
        {
            var customerId = GetCustomerId();
            var price = _context.Products.Find(ProductId).Price;

            var cart = new Cart
            {
                CustomerId = customerId,
                ProductId = ProductId,
                Quantity = Quantity,
                DateCreated = DateTime.UtcNow,
                Price = price
            };
            _context.Carts.Add(cart);
            _context.SaveChanges();

            return Redirect("Cart");
            
        }
        public IActionResult Cart()
        {
            var customerId = GetCustomerId();

                var carts = _context.Carts
                .Include(c => c.Product)
                .Where(c => c.CustomerId == customerId)
                .OrderByDescending(c => c.DateCreated)
                .ToList();

            var totalAmount = carts.Sum(c => (c.Price * c.Quantity));
            ViewBag.TotalAmount = totalAmount.ToString("C");

            return View(carts);
        }

        public IActionResult RemoveFromCart(int? id) {

            var cart = _context.Carts.Find(id);
            _context.Carts.Remove(cart);

            _context.SaveChanges();

            return RedirectToAction("Cart");
        }
        //GET Store/Checkout
        public IActionResult Checkout()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult Checkout([FromForm] Order order)
        {
            order.DateCreated = DateTime.UtcNow;
            order.CustomerId = GetCustomerId();
            order.Total = _context.Carts
                        .Where(c => c.CustomerId == order.CustomerId)
                        .Sum(c => (c.Price * c.Quantity));

            HttpContext.Session.SetObject("Order", order);

            return RedirectToAction("Payment");
        }

        // GET store/Payment
        public IActionResult Payment()
        {
            var order = HttpContext.Session.GetObject<Order>("Order");

            ViewBag.TotalAmount = order.Total.ToString("C");

            ViewBag.PublishableKey = _configuration["Payments:Stripe:Publishable_Key"];
      
            return View();
        }
        //Implement a Post payment action method to handle the payment response
        [HttpPost]
        public IActionResult Payment(string? stripeToken)
        {
            var order = HttpContext.Session.GetObject<Order>("Order");

            StripeConfiguration.ApiKey = _configuration["Payments:Stripe:Secret_Key"];

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(order.Total * 100),
                            Currency = "cad",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "AnimeWebPage Purchase"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = $"https://{Request.Host}/Store/SaveOrder",
                CancelUrl = $"https://{Request.Host}/Store/Cart"
            };
            // create a service object
            var service = new SessionService();
            // user the service object to create the session
            var session = service.Create(options);
            // return the session id to the view as Json response
            return Json(new { id = session.Id });
        }
        // GET : Store/SaveOrder
        [Authorize]
        public IActionResult SaveOrder()
        {
            // Retrieve order Object from session
            var order = HttpContext.Session.GetObject<Order>("Order");
            // and create a new order record in the db
            _context.Orders.Add(order);
            _context.SaveChanges();
            //get customer id
            var customerId = GetCustomerId();
            // get carts for customer
            var carts = _context.Carts.Where(c => c.CustomerId == customerId);
            // iterate over carts
            foreach (var cart in carts)
            {

                var orderItem = new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = cart.ProductId,
                    Quantity = cart.Quantity,
                    Price = cart.Price,
                };
                _context.OrderItems.Add(orderItem);
                _context.Carts.Remove(cart);
            }
            // for each cart create and order detail object and remove the cart
            // save changes
            _context.SaveChanges();
            // redirect to orders history page

            return RedirectToAction("Details", "Orders", new { @id = order.OrderId });

        }

        private string GetCustomerId()
        {
            var customerId = string.Empty;

            if (String.IsNullOrEmpty(HttpContext.Session.GetString("CustomerId")))
            {
                if (User.Identity.IsAuthenticated)
                {
                    customerId = User.Identity.Name;
                }
                else
                {
                    customerId = Guid.NewGuid().ToString();
                }

                HttpContext.Session.SetString("CustomerId", customerId);
            }
            else
            {
                customerId = HttpContext.Session.GetString("CustomerId");
            }
            return customerId;
        }
    }
}
