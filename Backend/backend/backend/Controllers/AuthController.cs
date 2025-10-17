using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using The_Charity.AppDBContext;
using The_Charity.Models;
using The_Charity.Models.DTOs;
using The_Charity.Services;

namespace The_Charity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly StripeService _stripeService;

        public AuthController(AppDbContext db, StripeService stripeService)
        {
            _db = db;
            _stripeService = stripeService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return BadRequest(new { error = "First name and last name are required" });
            }

            if (!request.termsAccepted)
            {
                return BadRequest(new { error = "You must accept the terms and conditions" });
            }

            var username = $"{request.FirstName} {request.LastName}";

            // Check if user already exists (case-insensitive)
            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
            {
                return BadRequest(new { error = "User already exists with this email" });
            }

            if (await _db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            {
                return BadRequest(new { error = "Username already taken" });
            }

            try
            {
                // Create Stripe customer - accepts any email format
                var stripeCustomer = await _stripeService.CreateCustomerAsync(request.Email, username);

                // Create user
                var user = new User
                {
                    Username = username,
                    firstName = request.FirstName,
                    lastName = request.LastName,
                    Email = request.Email.Trim().ToLower(),
                    PasswordHash = HashPassword(request.Password),
                    StripeCustomerId = stripeCustomer.Id,
                    PlaidUserId = Guid.NewGuid().ToString(),
                    phoneNumber = request.PhoneNumber,
                    authProvider = "local",
                    AuthSubject = "",
                    isActive = true,
                    isDeleted = false,
                    updatedAt = false,
                    termsAccepted = request.termsAccepted,
                    CreatedAt = DateTime.UtcNow
                };


                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                // Create default preferences
                var defaultCharity = await _db.Charities.FirstOrDefaultAsync();
                if (defaultCharity != null)
                {
                    var preferences = new UserPrefernce
                    {
                        UserId = user.Id,
                        DefaultCharityId = defaultCharity.Id,
                        AutoRoundUp = true,
                        RoundUpThreshold = 0.10m,
                        MonthlyDonationLimit = 50.00m,
                        NotifyOnDonation = true
                    };
                    _db.UserPreferences.Add(preferences);
                    await _db.SaveChangesAsync();
                }

                return Ok(new
                {
                    message = "User registered successfully",
                    user = new
                    {
                        id = user.Id,
                        username = user.Username,
                        email = user.Email,
                        stripeCustomerId = user.StripeCustomerId
                    }
                });
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, new { error = "Registration failed", details = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Email and password are required" });
            }

            // Case-insensitive email lookup
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower());

            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { error = "Invalid email or password" });
            }

            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Login successful",
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    stripeCustomerId = user.StripeCustomerId
                }
            });
        }

        // Optional: Check email availability
        [HttpGet("check-email/{email}")]
        public async Task<IActionResult> CheckEmailAvailability(string email)
        {
            var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
            return Ok(new { available = !exists });
        }

        // Optional: Check username availability
        [HttpGet("check-username/{username}")]
        public async Task<IActionResult> CheckUsernameAvailability(string username)
        {
            var exists = await _db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
            return Ok(new { available = !exists });
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            return HashPassword(password) == storedHash;
        }
    }
}