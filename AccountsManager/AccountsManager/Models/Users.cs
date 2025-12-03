namespace AccountsManager.Models
{
    public class Users
    {

        public int UserID { get; set; }

        public string? UserName { get; set; }

        public string? Email { get; set; }

        public string? Password { get; set; }

        public DateTime? CreationDate { get; set; } = DateTime.UtcNow;

        public override string ToString()
        {
            return $"{UserName} {Email} {CreationDate}";
        }
    }


}