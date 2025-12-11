using AccountsManager.Data;
using AccountsManager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AccountsManager.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly Sql _sql;

        public UserController(Sql sql)
        {
            _sql = sql;
        }
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            using SqlConnection conn = await _sql.GetOpenConnectionAsync();
            using var cmd = new SqlCommand(
                "SELECT UserID, Username, Email, PasswordHash, CreationDate FROM Users;", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<Users>();

            while (await reader.ReadAsync())
            {
                list.Add(new Users
                {
                    UserID = reader.GetInt32(0),
                    UserName = reader.GetString(1),
                    Email = reader.GetString(2),
                    Password = reader.GetString(3),
                    CreationDate = reader.GetDateTime(4),

                });
            }

            return Ok(list);
        }
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Users u)
        {
            if (u == null || string.IsNullOrWhiteSpace(u.UserName) || string.IsNullOrWhiteSpace(u.Email) || string.IsNullOrWhiteSpace(u.Password))
            {

                return BadRequest("Invalid User data");
            }
            using SqlConnection conn = await _sql.GetOpenConnectionAsync();
            using var cmd = new SqlCommand(@"
            INSERT INTO Users (Username, Email, PasswordHash)
            OUTPUT INSERTED.UserID
            VALUES (@Username, @Email, @PasswordHash);
        ", conn);

            cmd.Parameters.AddWithValue("@Username", u.UserName);
            cmd.Parameters.AddWithValue("@Email", u.Email);
            cmd.Parameters.AddWithValue("@PasswordHash", u.Password);

            var result = await cmd.ExecuteScalarAsync();
            if (result is int newId)
            {
                u.UserID = newId;
                return Created($"/api/users/{newId}", u);
            }
            else
            {
                return StatusCode(500, "Failed to create student.");
            }
        }





        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Email and password are required.");

            using SqlConnection conn = await _sql.GetOpenConnectionAsync();
            using var cmd = new SqlCommand(
                "SELECT UserID, Username, Email, PasswordHash FROM Users WHERE Email = @Email;", conn);
            cmd.Parameters.AddWithValue("@Email", request.Email);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return Unauthorized("Invalid email or password.");

            var userId = reader.GetInt32(0);
            var username = reader.GetString(1);
            var email = reader.GetString(2);
            var passwordHash = reader.GetString(3);

           
            bool passwordMatches = passwordHash == request.Password;

            if (!passwordMatches)
                return Unauthorized("Invalid email or password.");

            var user = new
            {
                UserID = userId,
                Username = username,
                Email = email
            };

            return Ok(user);
        }
    }

    
    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}

