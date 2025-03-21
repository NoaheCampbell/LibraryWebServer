using LibraryWebServer.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;

namespace LibraryWebServer.Controllers
{
    public class HomeController : Controller
    {
        // Simple "session" tracking (not secure, but okay for this assignment)
        private static string user = "";
        private static int card = -1;

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        
        /// <summary>
        /// 1) CheckLogin
        /// Logs a user in by verifying (name, cardnum) in Patrons.
        /// Sets static "user" and "card" if valid, otherwise returns { success = false }.
        /// </summary>
        [HttpPost]
        public IActionResult CheckLogin(string name, int cardnum)
        {
            using (Team8LibraryContext db = new Team8LibraryContext())
            {
                var patron = db.Patrons.FirstOrDefault(p => p.Name == name && p.CardNum == cardnum);

                if (patron == null)
                {
                    return Json(new { success = false });
                }
                else
                {
                    user = name;
                    card = cardnum;
                    return Json(new { success = true });
                }
            }
        }

        /// <summary>
        /// 2) LogOut
        /// Logs the user out by resetting static variables.
        /// </summary>
        [HttpPost]
        public ActionResult LogOut()
        {
            user = "";
            card = -1;
            return Json(new { success = true });
        }

        /// <summary>
        /// 3) AllTitles
        /// Returns JSON for *all* Titles in the database, with these fields:
        /// {
        ///   "isbn"   : string,
        ///   "title"  : string,
        ///   "author" : string,
        ///   "serial" : int?  (null if no Inventory row exists),
        ///   "name"   : string (the Patron's name if checked out, "" if available)
        /// }
        /// 
        /// If a Title has multiple copies (Inventory rows), you'll get multiple objects
        /// with the same ISBN but different Serials.
        /// If a Title is not in Inventory, serial = null.
        /// If a Title is in Inventory but not checked out, name = "".
        /// If checked out, name = Patron's Name from Patrons.
        /// </summary>
        [HttpPost]
        public ActionResult AllTitles()
        {
            using (Team8LibraryContext db = new Team8LibraryContext())
            {
                // We'll chain left joins:
                // Titles -> Inventory -> CheckedOut -> Patrons
                var query = 
                    from t in db.Titles
                    join i in db.Inventory on t.Isbn equals i.Isbn into ti
                    from i2 in ti.DefaultIfEmpty()  // left join: might be no Inventory
                    join co in db.CheckedOut on i2.Serial equals co.Serial into ico
                    from co2 in ico.DefaultIfEmpty() // left join: might not be checked out
                    join p in db.Patrons on co2.CardNum equals p.CardNum into cop
                    from p2 in cop.DefaultIfEmpty()  // left join: might be no matching patron
                    select new
                    {
                        isbn   = t.Isbn,
                        title  = t.Title,
                        author = t.Author,
                        serial = (i2 == null ? (int?)null : (int)i2.Serial),
                        name   = (p2 == null ? "" : p2.Name)
                    };

                return Json(query.ToList());
            }
        }

        /// <summary>
        /// 4) ListMyBooks
        /// Returns JSON of all books checked out by the *currently logged-in user*,
        /// with the fields { "title", "author", "serial" }.
        /// </summary>
        [HttpPost]
        public ActionResult ListMyBooks()
        {
            if (card == -1)
            {
                // Not logged in
                return Json(new { success = false, error = "User not logged in" });
            }

            using (Team8LibraryContext db = new Team8LibraryContext())
            {
                // Join CheckedOut -> Inventory -> Titles, for the current user
                var query =
                    from co in db.CheckedOut
                    join i in db.Inventory on co.Serial equals i.Serial
                    join t in db.Titles on i.Isbn equals t.Isbn
                    where co.CardNum == card
                    select new
                    {
                        title = t.Title,
                        author = t.Author,
                        serial = co.Serial
                    };

                return Json(query.ToList());
            }
        }

        /// <summary>
        /// 5) CheckOutBook
        /// Creates a new row in CheckedOut for (logged-in user, given serial),
        /// if not already checked out by someone else.
        /// 
        /// Returns { success = false } if the user isn't logged in or the book is already taken.
        /// Otherwise { success = true }.
        /// </summary>
        [HttpPost]
        public ActionResult CheckOutBook(int serial)
        {
            if (card == -1)
            {
                return Json(new { success = false, error = "User not logged in" });
            }

            using (Team8LibraryContext db = new Team8LibraryContext())
            {
                // Make sure the Inventory item exists
                var invItem = db.Inventory.FirstOrDefault(i => i.Serial == serial);
                if (invItem == null)
                {
                    return Json(new { success = false, error = "Invalid serial number" });
                }

                // Check if it's already checked out
                var already = db.CheckedOut.FirstOrDefault(co => co.Serial == serial);
                if (already != null)
                {
                    return Json(new { success = false, error = "That copy is already checked out" });
                }

                // Otherwise, insert a new row in CheckedOut
                var newRow = new CheckedOut
                {
                    CardNum = (uint)card,
                    Serial = (uint)serial
                };
                db.CheckedOut.Add(newRow);
                db.SaveChanges();

                return Json(new { success = true });
            }
        }

        /// <summary>
        /// 6) ReturnBook
        /// Removes the row from CheckedOut if (serial, card) exists.
        /// 
        /// Returns { success = false } if not logged in or the user doesn't have that book.
        /// Otherwise { success = true }.
        /// </summary>
        [HttpPost]
        public ActionResult ReturnBook(int serial)
        {
            if (card == -1)
            {
                return Json(new { success = false, error = "User not logged in" });
            }

            using (Team8LibraryContext db = new Team8LibraryContext())
            {
                var record = db.CheckedOut.FirstOrDefault(co => co.Serial == serial && co.CardNum == card);
                if (record == null)
                {
                    return Json(new { success = false, error = "This book isn't checked out by you" });
                }

                db.CheckedOut.Remove(record);
                db.SaveChanges();

                return Json(new { success = true });
            }
        }

        /*******************************************/
        /****** Do not modify below this line ******/
        /*******************************************/

        public IActionResult Index()
        {
            if (user == "" && card == -1)
                return View("Login");

            return View();
        }

        public IActionResult Login()
        {
            user = "";
            card = -1;
            ViewData["Message"] = "Please login.";
            return View();
        }

        public IActionResult MyBooks()
        {
            if (user == "" && card == -1)
                return View("Login");

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}